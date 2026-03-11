// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Sequencer.Queues;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Crypto;

namespace Nethermind.Arbitrum.Sequencer.Timeboost;

public sealed class ExpressLaneService(
    RoundTimingInfo roundTimingInfo,
    IExpressLaneTracker tracker,
    IArbitrumConfig arbitrumConfig,
    TransactionQueue transactionQueue,
    IEthereumEcdsa ethereumEcdsa) : IExpressLaneService
{
    private const uint MaxFutureSequenceDistance = 1000;

    private readonly TimeSpan _earlySubmissionGrace = TimeSpan.FromMilliseconds(arbitrumConfig.TimeboostEarlySubmissionGraceMs);

    private readonly Lock _roundLock = new();
    private readonly Dictionary<ulong, RoundInfo> _roundInfos = new();

    public async Task SequenceAsync(ExpressLaneSubmission submission, ulong currentBlockNumber)
    {
        await ValidateSubmissionAsync(submission);

        if (submission.SequenceNumber == ulong.MaxValue)
        {
            // DontCareSequence: fire-and-forget, same as the drain loop
            _ = PublishAsync(submission.Transaction, currentBlockNumber);
            return;
        }

        lock (_roundLock)
        {
            // Re-validate sender inside the lock to prevent stale control after round transfer
            ulong round = submission.Round;
            Address? controller = tracker.GetController(round);
            if (controller is null)
                throw new InvalidOperationException($"No controller for round {round}");

            Address sender = submission.RecoverSender(ethereumEcdsa);
            if (sender != controller)
                throw new InvalidOperationException("Sender is not the express lane controller");

            if (!_roundInfos.TryGetValue(round, out RoundInfo? roundInfo))
            {
                roundInfo = new RoundInfo();
                _roundInfos[round] = roundInfo;

                // Evict round infos older than 2 behind current
                ulong currentRound = roundTimingInfo.RoundNumber();
                if (currentRound > 2)
                {
                    ulong oldest = currentRound - 2;
                    List<ulong>? toRemove = null;
                    foreach (ulong key in _roundInfos.Keys)
                    {
                        if (key < oldest)
                            (toRemove ??= new()).Add(key);
                    }
                    if (toRemove is not null)
                    {
                        foreach (ulong key in toRemove)
                            _roundInfos.Remove(key);
                    }
                }
            }

            ulong seqNum = submission.SequenceNumber;
            ulong nextSeq = roundInfo.NextSequence;

            // Unified duplicate / conflict detection across all submissions ever seen for this round.
            // AllSeen retains entries even after they are drained, mirroring Go's msgBySequenceNumber.
            if (roundInfo.AllSeen.TryGetValue(seqNum, out ExpressLaneSubmission? existing))
            {
                if (Bytes.AreEqual(existing.Signature, submission.Signature))
                    return; // exact duplicate (same sig) → idempotent no-op

                if (seqNum < nextSeq)
                    throw new InvalidOperationException($"Sequence number {seqNum} too low; expected >= {nextSeq}");

                throw new InvalidOperationException($"Conflicting submission for sequence number {seqNum}");
            }

            if (seqNum < nextSeq)
                throw new InvalidOperationException($"Sequence number {seqNum} too low; expected >= {nextSeq}");

            if (seqNum > nextSeq + MaxFutureSequenceDistance)
                throw new InvalidOperationException($"Sequence number {seqNum} too far in the future");

            roundInfo.AllSeen[seqNum] = submission;

            while (roundInfo.AllSeen.TryGetValue(roundInfo.NextSequence, out ExpressLaneSubmission? next))
            {
                roundInfo.NextSequence++;
                _ = PublishAsync(next.Transaction, currentBlockNumber);
            }
        }
    }

    private async Task ValidateSubmissionAsync(ExpressLaneSubmission submission)
    {
        if (submission.Transaction is null || submission.Signature is null)
            throw new InvalidOperationException("Malformed express lane submission");

        if (submission.AuctionContractAddress != tracker.AuctionContractAddress)
            throw new InvalidOperationException($"Wrong auction contract address: {submission.AuctionContractAddress}");

        ulong currentRound = roundTimingInfo.RoundNumber();
        if (submission.Round == currentRound)
            return;

        if (submission.Round != currentRound + 1)
            throw new InvalidOperationException($"Express lane tx round {submission.Round} does not match current round {currentRound}");

        TimeSpan timeTilNext = roundTimingInfo.TimeTilNextRound();
        if (timeTilNext > _earlySubmissionGrace)
            throw new InvalidOperationException($"Express lane tx round {submission.Round} does not match current round {currentRound}");

        // Within early submission window — yield until the round starts
        if (timeTilNext > TimeSpan.Zero)
            await Task.Delay(timeTilNext);

        // Verify the round has not advanced past the expected round (mitigates late-wake edge case)
        if (roundTimingInfo.RoundNumber() > submission.Round)
            throw new InvalidOperationException($"Express lane tx round {submission.Round} does not match current round {roundTimingInfo.RoundNumber()}");
    }

    private async Task PublishAsync(Transaction tx, ulong currentBlockNumber)
    {
        TxQueueItem item = TxQueueItem.CreateTimeboosted(tx, CancellationToken.None, currentBlockNumber);
        await transactionQueue.EnqueueAsync(item);
    }

    private sealed class RoundInfo
    {
        public ulong NextSequence { get; set; }

        public Dictionary<ulong, ExpressLaneSubmission> AllSeen { get; } = new();
    }
}
