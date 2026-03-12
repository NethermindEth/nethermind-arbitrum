// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Sequencer.Queues;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Crypto;
using Nethermind.JsonRpc;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Sequencer.Timeboost;

public sealed class ExpressLaneService(
    IRoundTimingInfo roundTimingInfo,
    IExpressLaneTracker tracker,
    IArbitrumConfig arbitrumConfig,
    TransactionQueue transactionQueue,
    IEthereumEcdsa ethereumEcdsa,
    ulong chainId,
    ILogManager logManager) : IExpressLaneService
{
    private const uint MaxFutureSequenceDistance = 1000;
    private const ulong DontCareSequenceNumber = ulong.MaxValue;

    private readonly ILogger _logger = logManager.GetClassLogger<ExpressLaneService>();
    private readonly int _maxTxDataSize = arbitrumConfig.SequencerMaxTxDataSize;
    private readonly TimeSpan _earlySubmissionGrace = TimeSpan.FromMilliseconds(arbitrumConfig.TimeboostEarlySubmissionGraceMs);
    private readonly TimeSpan _queueTimeout = TimeSpan.FromMilliseconds(arbitrumConfig.SequencerQueueTimeoutMs);

    private readonly Lock _roundLock = new();
    private readonly Dictionary<ulong, RoundInfo> _roundInfos = new();

    public async Task SequenceAsync(ExpressLaneSubmission submission, ulong currentBlockNumber)
    {
        await ValidateSubmissionAsync(submission);

        if (submission.SequenceNumber == DontCareSequenceNumber)
        {
            // DontCareSequence: publish with timeout = min(TimeTilNextRound, QueueTimeout)
            if (roundTimingInfo.RoundNumber() != submission.Round)
                throw new InvalidOperationException($"Express lane tx round {submission.Round} does not match current round {roundTimingInfo.RoundNumber()}");

            TimeSpan timeout = TimeSpanMin(roundTimingInfo.TimeTilNextRound(), _queueTimeout);
            using CancellationTokenSource cts = new(timeout, roundTimingInfo.TimeProvider);
            ResultWrapper<Hash256> result = await PublishAsync(submission.Transaction, currentBlockNumber, cts.Token);
            if (result.Result != Result.Success)
                throw new InvalidOperationException(result.Result.Error ?? "Failed to publish DontCareSequence tx");

            return;
        }

        List<(ulong SeqNum, Transaction Tx)>? toPublish = null;

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

                // Evict round infos older than 1 behind current
                ulong currentRound = roundTimingInfo.RoundNumber();
                if (currentRound > 1)
                {
                    ulong oldest = currentRound - 1;
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

            // AllSeen retains entries even after they are drained for duplicate detection
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
                (toPublish ??= new()).Add((roundInfo.NextSequence, next.Transaction));
                roundInfo.NextSequence++;
            }
        }

        if (toPublish is not null)
        {
            InvalidOperationException? retErr = null;

            foreach (var (seqNum, tx) in toPublish)
            {
                if (roundTimingInfo.RoundNumber() != submission.Round)
                    break;

                TimeSpan timeout = TimeSpanMin(roundTimingInfo.TimeTilNextRound(), _queueTimeout);
                using CancellationTokenSource cts = new(timeout, roundTimingInfo.TimeProvider);

                ResultWrapper<Hash256> result = await PublishAsync(tx, currentBlockNumber, cts.Token);
                if (result.Result != Result.Success)
                {
                    bool isNearRoundBoundary = timeout < TimeSpan.FromSeconds(1);
                    if (isNearRoundBoundary)
                    {
                        if (_logger.IsWarn)
                            _logger.Warn($"Express lane tx seqNum={seqNum} timed out near round boundary");
                    }
                    else
                    {
                        if (_logger.IsError)
                            _logger.Error($"Error queuing express lane tx seqNum={seqNum} txHash={tx.Hash}: {result.Result.Error}");
                    }

                    if (seqNum == submission.SequenceNumber)
                        retErr = new InvalidOperationException(result.Result.Error ?? $"Failed to publish express lane tx seqNum={seqNum}");
                }
            }

            if (retErr is not null)
                throw retErr;
        }
    }

    private async Task ValidateSubmissionAsync(ExpressLaneSubmission submission)
    {
        if (submission.Transaction is null || submission.Signature is null)
            throw new InvalidOperationException("Malformed express lane submission");

        int txSize = Rlp.Encode(submission.Transaction).Bytes.Length;
        if (txSize > _maxTxDataSize)
            throw new InvalidOperationException($"Express lane tx size {txSize} exceeds maximum allowed size {_maxTxDataSize}");

        if (submission.ChainId != chainId)
            throw new InvalidOperationException($"Express lane tx chain ID {submission.ChainId} does not match current chain ID {chainId}");

        if (submission.AuctionContractAddress != tracker.AuctionContractAddress)
            throw new InvalidOperationException($"Wrong auction contract address: {submission.AuctionContractAddress}");

        ulong currentRound = roundTimingInfo.RoundNumber();
        if (submission.Round == currentRound)
            return;

        if (submission.Round != currentRound + 1)
            throw new InvalidOperationException($"Express lane tx round {submission.Round} does not match the current {currentRound} " +
                                                $"and next {currentRound + 1} rounds");

        TimeSpan timeTilNext = roundTimingInfo.TimeTilNextRound();
        if (timeTilNext > _earlySubmissionGrace)
            throw new InvalidOperationException($"Express lane tx round {submission.Round} does not match current round {currentRound} " +
                                                $"and time til next round {timeTilNext} exceeds early submission grace {_earlySubmissionGrace}");

        if (timeTilNext > TimeSpan.Zero)
            await Task.Delay(timeTilNext, roundTimingInfo.TimeProvider);

        ulong currentRoundAfterWait = roundTimingInfo.RoundNumber();
        if (currentRoundAfterWait > submission.Round)
            throw new InvalidOperationException($"Express lane tx round {submission.Round} does not match current round {currentRoundAfterWait} after waiting");
    }

    private async Task<ResultWrapper<Hash256>> PublishAsync(Transaction tx, ulong currentBlockNumber, CancellationToken ct)
    {
        TxQueueItem item = TxQueueItem.CreateTimeboosted(tx, ct, currentBlockNumber);
        return await transactionQueue.WriteChannelAsync(item);
    }

    private static TimeSpan TimeSpanMin(TimeSpan a, TimeSpan b) => a < b ? a : b;

    private sealed class RoundInfo
    {
        public ulong NextSequence { get; set; }

        public Dictionary<ulong, ExpressLaneSubmission> AllSeen { get; } = new();
    }
}
