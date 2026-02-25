// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Buffers.Binary;
using Nethermind.Core;
using Nethermind.Facade;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Sequencer.Timeboost;

public sealed class ExpressLaneService : IExpressLaneService, IDisposable
{
    // resolvedRounds() function selector = keccak256("resolvedRounds()")[:4] = 0x0d253fbe
    private static readonly byte[] ResolvedRoundsSelector = [0x0d, 0x25, 0x3f, 0xbe];

    private const uint MaxFutureSequenceDistance = 4096;

    private readonly RoundTimingInfo _roundTimingInfo;
    private readonly Address _auctionContractAddress;
    private readonly TransactionQueue _transactionQueue;
    private readonly Func<BlockHeader?> _getHead;
    private readonly Func<BlockHeader, Transaction, CallOutput> _callContract;
    private readonly TimeSpan _earlySubmissionGrace;
    private readonly ILogger _logger;

    private readonly Lock _roundLock = new();

    private readonly Dictionary<ulong, Address> _roundControllers = new();

    private readonly Dictionary<ulong, RoundInfo> _roundInfos = new();

    private readonly TimeSpan _pollInterval;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _pollingTask;

    public Address AuctionContractAddress => _auctionContractAddress;

    public ExpressLaneService(
        RoundTimingInfo roundTimingInfo,
        Address auctionContractAddress,
        TransactionQueue transactionQueue,
        Func<BlockHeader?> getHead,
        Func<BlockHeader, Transaction, CallOutput> callContract,
        TimeSpan earlySubmissionGrace,
        ILogManager logManager,
        TimeSpan? pollInterval = null)
    {
        _roundTimingInfo = roundTimingInfo;
        _auctionContractAddress = auctionContractAddress;
        _transactionQueue = transactionQueue;
        _getHead = getHead;
        _callContract = callContract;
        _earlySubmissionGrace = earlySubmissionGrace;
        _logger = logManager.GetClassLogger<ExpressLaneService>();
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);

        _pollingTask = Task.Run(PollContractLoopAsync);
    }

    public bool CurrentRoundHasController()
    {
        ulong round = _roundTimingInfo.RoundNumber();
        lock (_roundLock)
            return _roundControllers.ContainsKey(round);
    }

    public bool IsWithinAuctionCloseWindow(DateTime t)
        => _roundTimingInfo.IsWithinAuctionCloseWindow(t);

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
            if (!_roundControllers.TryGetValue(round, out Address? controller))
                throw new InvalidOperationException($"No controller for round {round}");

            Address sender = submission.RecoverSender();
            if (sender != controller)
                throw new InvalidOperationException("Sender is not the express lane controller");

            if (!_roundInfos.TryGetValue(round, out RoundInfo? roundInfo))
            {
                roundInfo = new RoundInfo();
                _roundInfos[round] = roundInfo;
            }

            ulong seqNum = submission.SequenceNumber;
            ulong nextSeq = roundInfo.NextSequence;

            // Unified duplicate / conflict detection across all submissions ever seen for this round.
            // AllSeen retains entries even after they are drained, mirroring Go's msgBySequenceNumber.
            if (roundInfo.AllSeen.TryGetValue(seqNum, out ExpressLaneSubmission? existing))
            {
                if (existing.Signature.AsSpan().SequenceEqual(submission.Signature))
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

        if (submission.AuctionContractAddress != _auctionContractAddress)
            throw new InvalidOperationException($"Wrong auction contract address: {submission.AuctionContractAddress}");

        ulong currentRound = _roundTimingInfo.RoundNumber();
        if (submission.Round == currentRound)
            return;

        if (submission.Round != currentRound + 1)
            throw new InvalidOperationException($"Express lane tx round {submission.Round} does not match current round {currentRound}");

        TimeSpan timeTilNext = _roundTimingInfo.TimeTilNextRound();
        if (timeTilNext > _earlySubmissionGrace)
            throw new InvalidOperationException($"Express lane tx round {submission.Round} does not match current round {currentRound}");

        // Within early submission window — yield until the round starts
        if (timeTilNext > TimeSpan.Zero)
            await Task.Delay(timeTilNext);

        // Verify the round has not advanced past the expected round (mitigates late-wake edge case)
        if (_roundTimingInfo.RoundNumber() > submission.Round)
            throw new InvalidOperationException($"Express lane tx round {submission.Round} does not match current round {_roundTimingInfo.RoundNumber()}");
    }

    private async Task PublishAsync(Transaction tx, ulong currentBlockNumber)
    {
        TxQueueItem item = TxQueueItem.CreateTimeboosted(tx, CancellationToken.None, currentBlockNumber);
        await _transactionQueue.EnqueueAsync(item);
    }

    private async Task PollContractLoopAsync()
    {
        using PeriodicTimer timer = new(_pollInterval);
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(_cts.Token);
                PollResolvedRounds();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                if (_logger.IsDebug)
                    _logger.Debug($"ExpressLaneService: poll failed: {ex.Message}");
            }
        }
    }

    private void PollResolvedRounds()
    {
        BlockHeader? head = _getHead();
        if (head is null)
            return;

        Transaction callTx = new()
        {
            To = _auctionContractAddress,
            Data = ResolvedRoundsSelector,
            GasLimit = 200_000,
            SenderAddress = Address.Zero,
        };

        CallOutput result = _callContract(head, callTx);
        if (result.Error is not null || result.ExecutionReverted)
            return;

        byte[] output = result.OutputData;
        // resolvedRounds() returns (ELCRound current, ELCRound upcoming).
        // ABI encodes each ELCRound = (address controller, uint64 round) as two 32-byte slots:
        //   [0..31]  = controller (address right-justified in [12..31])
        //   [32..63] = round      (uint64 right-justified in [56..63])
        // Nitro only uses the first (current) resolved round.
        if (output.Length < 128)
            return;

        Address controller = new(output.AsSpan(12, 20));
        ulong round = BinaryPrimitives.ReadUInt64BigEndian(output.AsSpan(56, 8));

        if (controller == Address.Zero || round == 0)
            return;

        ulong currentRound = _roundTimingInfo.RoundNumber();
        lock (_roundLock)
        {
            _roundControllers[round] = controller;

            // Clean up rounds older than 2 behind current
            if (currentRound > 2)
            {
                ulong oldest = currentRound - 2;
                foreach (ulong key in _roundControllers.Keys.Where(k => k < oldest).ToList())
                    _roundControllers.Remove(key);
                foreach (ulong key in _roundInfos.Keys.Where(k => k < oldest).ToList())
                    _roundInfos.Remove(key);
            }
        }

        if (_logger.IsDebug)
            _logger.Debug($"ExpressLaneService: round {round} controller = {controller}");
    }

    public void Dispose()
    {
        _cts.Cancel();
        _pollingTask.GetAwaiter().GetResult();
        _cts.Dispose();
    }

    internal void ForceSetController(ulong round, Address controller)
    {
        lock (_roundLock)
            _roundControllers[round] = controller;
    }

    internal bool HasControllerForRound(ulong round)
    {
        lock (_roundLock)
            return _roundControllers.ContainsKey(round);
    }

    internal void TriggerPoll() => PollResolvedRounds();

    private sealed class RoundInfo
    {
        public ulong NextSequence { get; set; }

        public Dictionary<ulong, ExpressLaneSubmission> AllSeen { get; } = new();
    }
}
