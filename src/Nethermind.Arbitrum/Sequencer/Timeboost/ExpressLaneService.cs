// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Sequencer.Queues;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Crypto;
using Nethermind.JsonRpc;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.State;

namespace Nethermind.Arbitrum.Sequencer.Timeboost;

public sealed class ExpressLaneService(
    IRoundTimingInfo roundTimingInfo,
    IExpressLaneTracker tracker,
    IArbitrumConfig arbitrumConfig,
    TransactionQueue transactionQueue,
    IEthereumEcdsa ethereumEcdsa,
    ChainSpec chainSpec,
    IBlockTree blockTree,
    IStateReader stateReader,
    ILogManager logManager) : IExpressLaneService
{
    public const uint MaxFutureSequenceDistance = 1000;
    public const ulong DontCareSequenceNumber = ulong.MaxValue;

    private readonly ILogger _logger = logManager.GetClassLogger<ExpressLaneService>();
    private readonly int _maxTxDataSize = arbitrumConfig.SequencerMaxTxDataSize;
    private readonly TimeSpan _earlySubmissionGrace = TimeSpan.FromMilliseconds(arbitrumConfig.TimeboostEarlySubmissionGraceMs);
    private readonly TimeSpan _queueTimeout = TimeSpan.FromMilliseconds(arbitrumConfig.SequencerQueueTimeoutMs);

    private readonly Lock _roundLock = new();
    private readonly Dictionary<ulong, RoundInfo> _roundInfos = new();

    public async Task<ResultWrapper<EmptyResponse>> SequenceAsync(ExpressLaneSubmission submission, ulong currentBlockNumber)
    {
        ResultWrapper<EmptyResponse> validationResult = await ValidateSubmissionAsync(submission);
        if (validationResult.Result != Result.Success)
            return validationResult;

        if (submission.Options is not null)
        {
            BlockHeader? head = blockTree.Head?.Header;
            if (head is null)
                return ResultWrapper<EmptyResponse>.Fail("Cannot validate conditional options: block tree head is not available");

            ulong l1BlockNumber = ArbitrumBlockHeaderInfo.Deserialize(head, _logger).L1BlockNumber;
            Result checkResult = submission.Options.Check(l1BlockNumber, head.Timestamp, stateReader, head);
            if (checkResult != Result.Success)
                return ResultWrapper<EmptyResponse>.Fail(checkResult.Error!);
        }

        if (submission.SequenceNumber == DontCareSequenceNumber)
        {
            // DontCareSequence: publish with timeout = min(TimeTilNextRound, QueueTimeout)
            if (roundTimingInfo.RoundNumber() != submission.Round)
                return ResultWrapper<EmptyResponse>.Fail($"Express lane tx round {submission.Round} does not match current round {roundTimingInfo.RoundNumber()}");

            TimeSpan timeout = TimeSpanMin(roundTimingInfo.TimeTilNextRound(), _queueTimeout);
            ResultWrapper<Hash256> result = await PublishAsync(submission.Transaction, submission.Options, currentBlockNumber, timeout);
            if (result.Result != Result.Success)
                return ResultWrapper<EmptyResponse>.Fail(result.Result.Error ?? "Failed to publish DontCareSequence tx");

            return ResultWrapper.EmptySuccess;
        }

        List<ExpressLaneSubmission>? toPublish = null;
        Address sender = submission.RecoverSender(ethereumEcdsa);

        lock (_roundLock)
        {
            // Re-validate sender inside the lock to prevent stale control after round transfer
            ulong round = submission.Round;
            Address? controller = tracker.GetController(round);
            if (controller is null)
                return ResultWrapper<EmptyResponse>.Fail($"No controller for round {round}");

            if (sender != controller)
                return ResultWrapper<EmptyResponse>.Fail("Sender is not the express lane controller");

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
                    return ResultWrapper.EmptySuccess; // exact duplicate (same sig) → idempotent no-op

                if (seqNum < nextSeq)
                    return ResultWrapper<EmptyResponse>.Fail($"Sequence number {seqNum} too low; expected >= {nextSeq}");

                return ResultWrapper<EmptyResponse>.Fail($"Conflicting submission for sequence number {seqNum}");
            }

            if (seqNum < nextSeq)
                return ResultWrapper<EmptyResponse>.Fail($"Sequence number {seqNum} too low; expected >= {nextSeq}");

            if (seqNum > nextSeq + MaxFutureSequenceDistance)
                return ResultWrapper<EmptyResponse>.Fail($"Sequence number {seqNum} too far in the future");

            roundInfo.AllSeen[seqNum] = submission;

            while (roundInfo.AllSeen.TryGetValue(roundInfo.NextSequence, out ExpressLaneSubmission? next))
            {
                (toPublish ??= new()).Add(next);
                roundInfo.NextSequence++;
            }
        }

        if (toPublish is not null)
        {
            ResultWrapper<EmptyResponse>? retErr = null;

            foreach (ExpressLaneSubmission queued in toPublish)
            {
                if (roundTimingInfo.RoundNumber() != submission.Round)
                    break;

                TimeSpan timeout = TimeSpanMin(roundTimingInfo.TimeTilNextRound(), _queueTimeout);
                ResultWrapper<Hash256> result = await PublishAsync(queued.Transaction, queued.Options, currentBlockNumber, timeout);
                if (result.Result != Result.Success)
                {
                    bool isNearRoundBoundary = timeout < TimeSpan.FromSeconds(1);
                    if (isNearRoundBoundary)
                    {
                        if (_logger.IsWarn)
                            _logger.Warn($"Express lane tx seqNum={queued.SequenceNumber} timed out near round boundary");
                    }
                    else
                    {
                        if (_logger.IsError)
                            _logger.Error($"Error queuing express lane tx seqNum={queued.SequenceNumber} txHash={queued.Transaction.Hash}: {result.Result.Error}");
                    }

                    if (queued.SequenceNumber == submission.SequenceNumber)
                        retErr = ResultWrapper<EmptyResponse>.Fail(result.Result.Error ?? $"Failed to publish express lane tx seqNum={queued.SequenceNumber}");
                }
            }

            if (retErr is not null)
                return retErr;
        }

        return ResultWrapper.EmptySuccess;
    }

    private async Task<ResultWrapper<EmptyResponse>> ValidateSubmissionAsync(ExpressLaneSubmission submission)
    {
        if (submission.Transaction is null || submission.Signature is null)
            return ResultWrapper<EmptyResponse>.Fail("Malformed express lane submission");

        int txSize = Rlp.Encode(submission.Transaction).Bytes.Length;
        if (txSize > _maxTxDataSize)
            return ResultWrapper<EmptyResponse>.Fail($"Express lane tx size {txSize} exceeds maximum allowed size {_maxTxDataSize}");

        if (submission.ChainId != chainSpec.ChainId)
            return ResultWrapper<EmptyResponse>.Fail($"Express lane tx chain ID {submission.ChainId} does not match current chain ID {chainSpec.ChainId}");

        if (submission.AuctionContractAddress != tracker.AuctionContractAddress)
            return ResultWrapper<EmptyResponse>.Fail($"Wrong auction contract address: {submission.AuctionContractAddress}");

        ulong currentRound = roundTimingInfo.RoundNumber();
        if (submission.Round == currentRound)
            return ResultWrapper.EmptySuccess;

        if (submission.Round != currentRound + 1)
            return ResultWrapper<EmptyResponse>.Fail($"Express lane tx round {submission.Round} does not match the current {currentRound} " +
                                                    $"and next {currentRound + 1} rounds");

        TimeSpan timeTilNext = roundTimingInfo.TimeTilNextRound();
        if (timeTilNext > _earlySubmissionGrace)
            return ResultWrapper<EmptyResponse>.Fail($"Express lane tx round {submission.Round} does not match current round {currentRound} " +
                                                    $"and time til next round {timeTilNext} exceeds early submission grace {_earlySubmissionGrace}");

        if (timeTilNext > TimeSpan.Zero)
            await Task.Delay(timeTilNext, roundTimingInfo.TimeProvider);

        ulong currentRoundAfterWait = roundTimingInfo.RoundNumber();
        if (currentRoundAfterWait > submission.Round)
            return ResultWrapper<EmptyResponse>.Fail($"Express lane tx round {submission.Round} does not match current round {currentRoundAfterWait} after waiting");

        return ResultWrapper.EmptySuccess;
    }

    private async Task<ResultWrapper<Hash256>> PublishAsync(Transaction tx, ConditionalOptions? options, ulong currentBlockNumber, TimeSpan timeout)
    {
        TxQueueItem item = TxQueueItem.CreateTimeboosted(tx, currentBlockNumber, timeout, options);
        return await transactionQueue.EnqueueAsync(item, awaitForCompletion: false);
    }

    private static TimeSpan TimeSpanMin(TimeSpan a, TimeSpan b) => a < b ? a : b;

    private sealed class RoundInfo
    {
        public ulong NextSequence { get; set; }

        public Dictionary<ulong, ExpressLaneSubmission> AllSeen { get; } = new();
    }
}
