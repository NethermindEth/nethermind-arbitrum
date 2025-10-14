// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Core.Test;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;
using Nethermind.Arbitrum.Sequencer;

namespace Nethermind.Arbitrum.Test.Sequencer;

[TestFixture]
public class TransactionQueueTests
{
    private TransactionQueue _queue = null!;
    private SequencerConfig _config = null!;
    private InterfaceLogger _logger = null!;

    [SetUp]
    public void Setup()
    {
        _config = new SequencerConfig
        {
            MaxQueueSize = 10,
            QueueTimeoutSeconds = 2,
            MaxTxDataSize = 1000 // 1KB limit for testing
        };

        _logger = new TestLogger();
        _queue = new TransactionQueue(_config, _logger, TxDecoder.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _queue.Dispose();
    }

    #region Basic Tests

    [Test]
    public void Constructor_WithValidParameters_CreatesEmptyQueue()
    {
        _queue.Should().NotBeNull();
        _queue.QueueLength.Should().Be(0);
    }

    [Test]
    public async Task EnqueueTransaction_WithValidTransaction_CompletesWithExpectedHash()
    {
        Transaction tx = Build.A.Transaction.TestObject;
        Hash256 expectedHash = TestItem.KeccakA;

        Task<Hash256> enqueueTask = _queue.EnqueueTransaction(tx);

        enqueueTask.Should().NotBeNull();
        _queue.QueueLength.Should().Be(1);

        await _queue.DequeueTransaction();
        _queue.CompleteTransaction(tx, expectedHash);

        Hash256 result = await enqueueTask;
        result.Should().Be(expectedHash);
    }

    [Test]
    public async Task EnqueueDequeue_WithBasicFlow_ReturnsQueuedTransaction()
    {
        Transaction tx = Build.A.Transaction.TestObject;
        Hash256 expectedHash = TestItem.KeccakB;

        Task<Hash256> enqueueTask = _queue.EnqueueTransaction(tx);
        QueuedTransaction? dequeuedTx = await _queue.DequeueTransaction();

        dequeuedTx.Should().NotBeNull();
        dequeuedTx.Transaction.Should().Be(tx);

        _queue.CompleteTransaction(tx, expectedHash);
        Hash256 result = await enqueueTask;
        result.Should().Be(expectedHash);
    }

    [Test]
    public void QueuedTransaction_ReturnResult_CompletesTaskSuccessfully()
    {
        Transaction tx = Build.A.Transaction.TestObject;
        Hash256 expectedHash = TestItem.KeccakC;
        QueuedTransaction queuedTx = new QueuedTransaction
        {
            Transaction = tx,
            TxSize = 100
        };

        queuedTx.ReturnResult(expectedHash);

        queuedTx.ResultSource.Task.IsCompletedSuccessfully.Should().BeTrue();
        queuedTx.ResultSource.Task.Result.Should().Be(expectedHash);
    }

    [Test]
    public void QueuedTransaction_IsTimedOut_ReturnsCorrectTimeoutStatus()
    {
        Transaction tx = Build.A.Transaction.TestObject;
        DateTime oldTime = DateTime.UtcNow.AddSeconds(-10);
        QueuedTransaction queuedTx = new QueuedTransaction
        {
            Transaction = tx,
            TxSize = 100,
            FirstAppearance = oldTime
        };

        queuedTx.IsTimedOut(5).Should().BeTrue();   // 10 seconds > 5 seconds timeout
        queuedTx.IsTimedOut(15).Should().BeFalse(); // 10 seconds < 15 seconds timeout
    }

    #endregion

    #region Size Validation Tests

    [Test]
    public void EnqueueTransaction_WithTxExceedingSizeLimit_ThrowsInvalidOperationException()
    {
        // Create a transaction with large data to exceed the 1 KB limit
        byte[] largeData = new byte[2000]; // 2KB
        Transaction largeTx = Build.A.Transaction.WithData(largeData).TestObject;

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _queue.EnqueueTransaction(largeTx));
    }

    [Test]
    public async Task EnqueueTransaction_WithValidSize_Succeeds()
    {
        byte[] smallData = new byte[100]; // 100 bytes
        Transaction smallTx = Build.A.Transaction.WithData(smallData).TestObject;

        Task<Hash256> enqueueTask = _queue.EnqueueTransaction(smallTx);
        enqueueTask.Should().NotBeNull();

        await _queue.DequeueTransaction();
        _queue.CompleteTransaction(smallTx, TestItem.KeccakA);

        Hash256 result = await enqueueTask;
        result.Should().Be(TestItem.KeccakA);
    }

    [Test]
    public async Task EnqueueTransaction_WithZeroSizeLimit_AcceptsAnySize()
    {
        _queue.Dispose();
        _config.MaxTxDataSize = 0; // Disable size limit
        _queue = new TransactionQueue(_config, _logger, TxDecoder.Instance);

        byte[] largeData = new byte[10000]; // 10KB
        Transaction largeTx = Build.A.Transaction.WithData(largeData).TestObject;

        Task<Hash256> enqueueTask = _queue.EnqueueTransaction(largeTx);
        enqueueTask.Should().NotBeNull();

        await _queue.DequeueTransaction();
        _queue.CompleteTransaction(largeTx, TestItem.KeccakA);

        Hash256 result = await enqueueTask;
        result.Should().Be(TestItem.KeccakA);
    }

    #endregion

    #region Nonce Validation Tests

    [Test]
    public void ValidateTransactionNonce_WithValidNonce_ReturnsTrue()
    {
        Transaction tx = Build.A.Transaction.WithNonce(100).TestObject;

        bool isValid = TransactionQueue.ValidateTransactionNonce(tx);

        isValid.Should().BeTrue();
    }

    [Test]
    public void ValidateTransactionNonce_WithMaxNonce_ReturnsFalse()
    {
        Transaction tx = Build.A.Transaction.WithNonce(ulong.MaxValue).TestObject;

        bool isValid = TransactionQueue.ValidateTransactionNonce(tx);

        isValid.Should().BeFalse();
    }

    [Test]
    public void MoveToRetryQueue_WithReason_LogsReason()
    {
        Transaction tx = Build.A.Transaction.TestObject;
        QueuedTransaction queuedTx = new()
        {
            Transaction = tx,
            TxSize = 100
        };

        _queue.MoveToRetryQueue(queuedTx, "nonce too high");

        _queue.RetryQueueLength.Should().Be(1);
    }

    #endregion

    #region Queue Full Scenarios

    [Test]
    public Task EnqueueTransaction_WhenQueueFull_ThrowsInvalidOperationException()
    {
        for (int i = 0; i < _config.MaxQueueSize; i++)
        {
            Transaction tx = Build.A.Transaction.WithNonce((ulong)i).TestObject;
            _ = _queue.EnqueueTransaction(tx);
        }

        // Try to add one more - should timeout and throw
        Transaction extraTx = Build.A.Transaction.WithNonce(99999).TestObject;

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _queue.EnqueueTransaction(extraTx));
        return Task.CompletedTask;
    }

    [Test]
    public void MoveToRetryQueue_MultipleTransactions_HandlesCorrectly()
    {
        // Add multiple transactions to retry queue
        for (int i = 0; i < 5; i++)
        {
            Transaction tx = Build.A.Transaction.WithNonce((ulong)i).TestObject;
            QueuedTransaction queuedTx = new QueuedTransaction
            {
                Transaction = tx,
                TxSize = 100
            };
            _queue.MoveToRetryQueue(queuedTx);
        }

        _queue.RetryQueueLength.Should().Be(5);
    }

    #endregion

    #region Timeout Tests

    [Test]
    public void QueuedTransaction_IsTimedOut_WorksCorrectly()
    {
        Transaction tx = Build.A.Transaction.TestObject;
        QueuedTransaction queuedTx = new QueuedTransaction
        {
            Transaction = tx,
            TxSize = 100,
            FirstAppearance = DateTime.UtcNow.AddSeconds(-10) // 10 seconds ago
        };

        // Transaction created 10 seconds ago should be timed out with a 2-second timeout
        queuedTx.IsTimedOut(2).Should().BeTrue();

        // But not timed out with a 15-second timeout
        queuedTx.IsTimedOut(15).Should().BeFalse();
    }

    #endregion

    #region Retry Queue Priority Tests

    [Test]
    public async Task DequeueTransaction_PrioritizesRetryQueue()
    {
        // Add transaction to the main queue
        Transaction mainTx = Build.A.Transaction.WithNonce(1).TestObject;
        _ = _queue.EnqueueTransaction(mainTx);

        // Add transaction to retry queue
        Transaction retryTx = Build.A.Transaction.WithNonce(2).TestObject;
        QueuedTransaction retryQueuedTx = new()
        {
            Transaction = retryTx,
            TxSize = 100
        };
        _queue.MoveToRetryQueue(retryQueuedTx);

        // Dequeue should return retry transaction first
        QueuedTransaction? dequeuedTx = await _queue.DequeueTransaction();

        dequeuedTx.Should().NotBeNull();
        dequeuedTx.Transaction.Should().Be(retryTx);
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public void QueuedTransaction_ReturnResult_WithException_CompletesWithException()
    {
        Transaction tx = Build.A.Transaction.TestObject;
        QueuedTransaction queuedTx = new()
        {
            Transaction = tx,
            TxSize = 100
        };

        Exception testException = new InvalidOperationException("Test error");
        queuedTx.ReturnResult(null, testException);

        queuedTx.ResultSource.Task.IsFaulted.Should().BeTrue();
        queuedTx.ResultSource.Task.Exception!.InnerException.Should().Be(testException);
    }

    [Test]
    public void QueuedTransaction_ReturnResult_CalledTwice_IgnoresSecondCall()
    {
        Transaction tx = Build.A.Transaction.TestObject;
        QueuedTransaction queuedTx = new()
        {
            Transaction = tx,
            TxSize = 100
        };

        queuedTx.ReturnResult(TestItem.KeccakA);
        queuedTx.ReturnResult(TestItem.KeccakB); // Should be ignored

        queuedTx.ResultSource.Task.IsCompletedSuccessfully.Should().BeTrue();
        queuedTx.ResultSource.Task.Result.Should().Be(TestItem.KeccakA);
    }

    [Test]
    public void QueuedTransaction_Cancel_SetsTaskAsCanceled()
    {
        Transaction tx = Build.A.Transaction.TestObject;
        QueuedTransaction queuedTx = new()
        {
            Transaction = tx,
            TxSize = 100
        };

        queuedTx.Cancel();

        queuedTx.ResultSource.Task.IsCanceled.Should().BeTrue();
    }

    #endregion
}
