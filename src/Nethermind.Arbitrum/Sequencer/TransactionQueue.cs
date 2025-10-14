// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Concurrent;
using System.Threading.Channels;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Sequencer;

/// <summary>
/// Thread-safe transaction queue for the Arbitrum sequencer.
/// Manages transaction ordering, timeout handling, and retry logic.
///
/// FUTURE ENHANCEMENTS
/// - L1 surplus threshold validation (Nitro: expectedSurplusHardThreshold)
/// - Sender allowlist authorization (Nitro: senderWhitelist)
/// - Transaction type validation (Nitro: reject ArbitrumDepositTxType, BlobTxType)
/// - Time-boost express lane functionality (Nitro: ExpressLaneService integration)
/// - Advanced sequencer backlog metrics (Nitro: sequencerBacklogGauge)
/// - Block-based timeout handling for time-boosted transactions
/// </summary>
public sealed class TransactionQueue : IDisposable
{
    private readonly ChannelReader<QueuedTransaction> _reader;
    private readonly ChannelWriter<QueuedTransaction> _writer;
    private readonly SynchronizedTxQueue _txRetryQueue = new();
    private readonly ConcurrentDictionary<Transaction, QueuedTransaction> _activeTransactions = new();
    private readonly ConcurrentQueue<double> _recentQueueTimes = new();
    private readonly object _timeoutLock = new();
    private readonly Timer _timeoutTimer;

    // Performance tracking
    private long _totalEnqueued;
    private long _totalDequeued;
    private long _totalTimedOut;
    private long _totalRejected;
    private long _totalTimeBoost;
    private readonly ISequencerConfig _config;
    private readonly InterfaceLogger _logger;
    private readonly TxDecoder _txDecoder;

    // TODO: IExpressLaneService _expressLaneService - Time-boost functionality
    // TODO: IL1Reader _l1Reader - L1 surplus threshold validation
    // TODO: Dictionary<Address, bool> _senderWhitelist - Transaction sender authorization
    // TODO: ISequencerBacklogGauge _backlogGauge - Nitro-style backlog metrics

    public TransactionQueue(ISequencerConfig config, InterfaceLogger logger, TxDecoder txDecoder)
    {
        _config = config;
        _logger = logger;
        _txDecoder = txDecoder;

        Channel<QueuedTransaction> txQueue = Channel.CreateBounded<QueuedTransaction>(new BoundedChannelOptions(config.MaxQueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

        _reader = txQueue.Reader;
        _writer = txQueue.Writer;
        _timeoutTimer = new Timer(ProcessTimeouts, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Current number of transactions in the main queue
    /// </summary>
    public int QueueLength
        => _reader.CanCount ? _reader.Count : 0;

    /// <summary>
    /// Current number of transactions in the retry queue (matches Nitro's txRetryQueue.Len())
    /// </summary>
    public int RetryQueueLength
        => _txRetryQueue.Len();


    /// <summary>
    /// Enqueues a transaction for processing by the sequencer.
    /// Returns a task that completes when the transaction is processed.
    /// </summary>
    /// <param name="tx">Transaction to enqueue</param>
    /// <param name="options">Optional conditional execution options</param>
    /// <param name="isTimeBoosted">Whether this transaction was submitted via Time-boost</param>
    /// <param name="blockStamp">Block number for Time-boost ordering</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Task that resolves to transaction hash when processed</returns>
    public async Task<Hash256> EnqueueTransaction(
        Transaction tx,
        ConditionalOptions? options = null,
        bool isTimeBoosted = false,
        ulong blockStamp = 0,
        CancellationToken cancellationToken = default)
    {
        // TODO: Add L1 surplus threshold check (Nitro: expectedSurplusHardThreshold validation)
        // TODO: Add sender whitelist validation (Nitro: senderWhitelist check)
        // TODO: Add transaction type validation (Nitro: reject ArbitrumDepositTxType, BlobTxType)
        // TODO: Add sequencer backlog metrics (Nitro: sequencerBacklogGauge.Inc/Dec)

        int txSize = CalculateTransactionSize(tx);

        // Validate transaction size if limit is configured
        if (_config.MaxTxDataSize > 0 && txSize > _config.MaxTxDataSize)
            throw new InvalidOperationException($"Transaction too large: {txSize} bytes > {_config.MaxTxDataSize} bytes limit");

        // TODO: Add Time-boost delay logic (Nitro: time.Sleep for non-express lane transactions)

        QueuedTransaction queuedTx = new()
        {
            Transaction = tx,
            TxSize = txSize,
            Options = options,
            IsTimeBoosted = isTimeBoosted,
            BlockStamp = blockStamp,
            CancellationToken = cancellationToken
        };

        // Track active transaction
        _activeTransactions.TryAdd(tx, queuedTx);

        try
        {
            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            TimeSpan queueTimeout = TimeSpan.FromSeconds(_config.QueueTimeoutSeconds);
            timeoutCts.CancelAfter(queueTimeout);

            await _writer.WriteAsync(queuedTx, timeoutCts.Token);

            Interlocked.Increment(ref _totalEnqueued);
            if (isTimeBoosted) Interlocked.Increment(ref _totalTimeBoost);

            UpdateMetrics();

            if (_logger.IsDebug)
                _logger.Debug($"Enqueued transaction {tx.Hash}, size={txSize}, time-boosted={isTimeBoosted}");

            // Wait for a result with abort timeout (matches Nitro's abortCtx = queueTimeout * 2)
            using CancellationTokenSource abortCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            abortCts.CancelAfter(queueTimeout.Add(queueTimeout)); // queueTimeout * 2

            return await queuedTx.ResultSource.Task.WaitAsync(abortCts.Token);
        }
        catch (OperationCanceledException)
        {
            _activeTransactions.TryRemove(tx, out _);
            Interlocked.Increment(ref _totalRejected);
            UpdateMetrics();
            throw new InvalidOperationException("Transaction queue is full or timed out");
        }
        catch (Exception ex)
        {
            _activeTransactions.TryRemove(tx, out _);
            if (_logger.IsError)
                _logger.Error($"Failed to enqueue transaction {tx.Hash}", ex);
            throw;
        }
    }

    /// <summary>
    /// Dequeues the next transaction for processing.
    /// Prioritizes retry queue over main queue.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Next transaction to process, or null if canceled</returns>
    public async Task<QueuedTransaction?> DequeueTransaction(CancellationToken cancellationToken = default)
    {
        if (_txRetryQueue.HasItems())
        {
            QueuedTransaction retryTx = _txRetryQueue.Pop();
            Interlocked.Increment(ref _totalDequeued);
            UpdateMetrics();

            if (_logger.IsDebug)
                _logger.Debug($"Dequeued retry transaction {retryTx.Transaction.Hash}, retry_length={RetryQueueLength}");
            return retryTx;
        }

        // Then check the main queue
        try
        {
            QueuedTransaction queuedTx = await _reader.ReadAsync(cancellationToken);

            Interlocked.Increment(ref _totalDequeued);

            // Track queue time for metrics
            double queueTime = (DateTime.UtcNow - queuedTx.FirstAppearance).TotalMilliseconds;
            TrackQueueTime(queueTime);

            UpdateMetrics();

            if (_logger.IsDebug)
                _logger.Debug($"Dequeued transaction {queuedTx.Transaction.Hash} from main, queue_length={QueueLength}");

            return queuedTx;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Basic nonce validation for retry queue functionality
    /// </summary>
    /// <param name="tx">Transaction to validate</param>
    /// <returns>True if nonce appears valid for queuing</returns>
    public static bool ValidateTransactionNonce(Transaction tx)
    {
        // Basic nonce validation - in full implementation this would check against the current state
        // For now, just ensure nonce is not obviously invalid (not max value)
        return tx.Nonce < ulong.MaxValue;
    }

    // TODO: ValidateSenderWhitelist(Transaction tx, Dictionary<Address, bool> whitelist)
    // TODO: ValidateTransactionType(Transaction tx) - reject ArbitrumDepositTxType, BlobTxType
    // TODO: ValidateL1Surplus(long expectedSurplus, long threshold)
    // TODO: ApplyTimeBoostDelay(bool isExpressLaneController, IExpressLaneService service)

    /// <summary>
    /// Moves a transaction to the retry queue for later processing (matches Nitro's txRetryQueue.Push)
    /// </summary>
    /// <param name="queuedTx">Transaction to retry</param>
    /// <param name="reason">Reason for retry (e.g., "nonce too high")</param>
    public void MoveToRetryQueue(QueuedTransaction queuedTx, string reason = "retry")
    {
        _txRetryQueue.Push(queuedTx);

        if (_logger.IsDebug)
            _logger.Debug($"Moved transaction {queuedTx.Transaction.Hash} to retry queue, reason: {reason}");
    }

    /// <summary>
    /// Completes a transaction with success or failure
    /// </summary>
    /// <param name="tx">Transaction that was processed</param>
    /// <param name="result">Transaction hash if successful</param>
    /// <param name="exception">Error if failed</param>
    public void CompleteTransaction(Transaction tx, Hash256? result = null, Exception? exception = null)
    {
        if (!_activeTransactions.TryRemove(tx, out QueuedTransaction? queuedTx)) return;
        // Return result to caller
        queuedTx.ReturnResult(result, exception);

        UpdateMetrics();

        if (_logger.IsDebug)
            _logger.Debug($"Completed transaction {tx.Hash} with result={result}, error={exception?.Message}");
    }

    /// <summary>
    /// Process timed out transactions
    /// </summary>
    private void ProcessTimeouts(object? state)
    {
        if (!Monitor.TryEnter(_timeoutLock, 100))
            return;

        try
        {
            List<QueuedTransaction> timedOutTransactions = new List<QueuedTransaction>();

            // Find timed out transactions
            foreach (KeyValuePair<Transaction, QueuedTransaction> kvp in _activeTransactions)
            {
                QueuedTransaction queuedTx = kvp.Value;
                if (queuedTx.IsTimedOut(_config.QueueTimeoutSeconds))
                    timedOutTransactions.Add(queuedTx);
            }

            // Remove and cancel timed out transactions
            foreach (QueuedTransaction queuedTx in timedOutTransactions)
                if (_activeTransactions.TryRemove(queuedTx.Transaction, out _))
                {
                    queuedTx.ReturnResult(null, new TimeoutException($"Transaction timed out after {_config.QueueTimeoutSeconds} seconds"));
                    Interlocked.Increment(ref _totalTimedOut);

                    if (_logger.IsWarn)
                        _logger.Warn($"Transaction {queuedTx.Transaction.Hash} timed out after {_config.QueueTimeoutSeconds}s");
                }

            if (timedOutTransactions.Count > 0)
                UpdateMetrics();
        }
        finally
        {
            Monitor.Exit(_timeoutLock);
        }
    }

    /// <summary>
    /// Track queue time for metrics calculation
    /// </summary>
    private void TrackQueueTime(double queueTimeMs)
    {
        _recentQueueTimes.Enqueue(queueTimeMs);

        // Keep only recent 1000 samples
        while (_recentQueueTimes.Count > 1000)
            _recentQueueTimes.TryDequeue(out _);
    }

    /// <summary>
    /// Updates internal metrics for monitoring
    /// </summary>
    private void UpdateMetrics()
    {
        // Always update metrics (Nitro doesn't have EnableMetrics toggle)
        Metrics.TransactionQueueLength = QueueLength;
        Metrics.RetryQueueLength = RetryQueueLength;
        Metrics.TransactionsEnqueued = _totalEnqueued;
        Metrics.TransactionsDequeued = _totalDequeued;
        Metrics.TransactionsTimedOut = _totalTimedOut;
        Metrics.TransactionsRejectedQueueFull = _totalRejected;
        Metrics.TimeboostTransactions = _totalTimeBoost;

        // Calculate average queue time
        if (!_recentQueueTimes.IsEmpty)
            Metrics.AverageQueueTimeMs = _recentQueueTimes.Average();
    }

    /// <summary>
    /// Calculate transaction size using RLP encoding
    /// </summary>
    private int CalculateTransactionSize(Transaction tx)
    {
        int exactSize = _txDecoder.GetLength(tx, RlpBehaviors.SkipTypedWrapping);

        if (_logger.IsTrace)
            _logger.Trace($"Calculated exact transaction size: {exactSize} bytes for tx {tx.Hash}");

        return exactSize;
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (!disposing) return;
        _timeoutTimer.Dispose();
        _writer.TryComplete();

        // Cancel all remaining transactions
        foreach (KeyValuePair<Transaction, QueuedTransaction> kvp in _activeTransactions) kvp.Value.Cancel();

        // Clean up retry queue before disposing it
        try
        {
            QueuedTransaction[] retryItems = _txRetryQueue.Clear();
            foreach (QueuedTransaction retryTx in retryItems)
                retryTx.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed, ignore
        }

        _txRetryQueue.Dispose();
    }
}
