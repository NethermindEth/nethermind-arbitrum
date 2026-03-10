// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Sequencer.Queues;
using Nethermind.Core;
using Nethermind.Core.Test.Builders;

namespace Nethermind.Arbitrum.Test.Sequencer.Queues;

[TestFixture]
public class TransactionQueueTests
{
    [Test]
    public async Task DrainBatch_AfterSingleEnqueue_ReturnsThatItem()
    {
        TransactionQueue queue = new(capacity: 10, maxTxDataSize: 95000, awaitTxResult: false);
        TxQueueItem item = CreateItem();

        Exception? result = await queue.EnqueueAsync(item);

        result.Should().BeNull();
        List<TxQueueItem> drained = queue.DrainBatch();
        drained.Should().HaveCount(1);
        drained[0].Should().BeSameAs(item);
    }

    [Test]
    public async Task DrainBatch_AfterMultipleEnqueues_ReturnsAllInFifoOrder()
    {
        TransactionQueue queue = new(capacity: 10, maxTxDataSize: 95000, awaitTxResult: false);
        TxQueueItem item1 = CreateItem();
        TxQueueItem item2 = CreateItem();
        TxQueueItem item3 = CreateItem();

        await queue.EnqueueAsync(item1);
        await queue.EnqueueAsync(item2);
        await queue.EnqueueAsync(item3);

        List<TxQueueItem> drained = queue.DrainBatch();
        drained.Should().HaveCount(3);
        drained[0].Should().BeSameAs(item1);
        drained[1].Should().BeSameAs(item2);
        drained[2].Should().BeSameAs(item3);
    }

    [Test]
    public async Task EnqueueAsync_WhenTxExceedsMaxSize_ReturnsError()
    {
        TransactionQueue queue = new(capacity: 10, maxTxDataSize: 10, awaitTxResult: false);
        TxQueueItem item = CreateItem();

        Exception? result = await queue.EnqueueAsync(item);

        result.Should().BeOfType<InvalidOperationException>();
        result!.Message.Should().Contain("exceeds maximum");
    }

    [Test]
    public async Task EnqueueAsync_WhenQueueFull_ReturnsError()
    {
        TransactionQueue queue = new(capacity: 1, maxTxDataSize: 95000, awaitTxResult: false);

        await queue.EnqueueAsync(CreateItem());
        Exception? result = await queue.EnqueueAsync(CreateItem());

        result.Should().BeOfType<InvalidOperationException>();
        result!.Message.Should().Contain("full");
    }

    [Test]
    public void DrainBatch_WhenQueueEmpty_ReturnsEmptyList()
    {
        TransactionQueue queue = new(capacity: 10, maxTxDataSize: 95000, awaitTxResult: false);

        List<TxQueueItem> drained = queue.DrainBatch();

        drained.Should().BeEmpty();
    }

    [Test]
    public async Task DrainBatch_AfterPreviousDrain_ReturnsEmpty()
    {
        TransactionQueue queue = new(capacity: 10, maxTxDataSize: 95000, awaitTxResult: false);
        await queue.EnqueueAsync(CreateItem());

        queue.DrainBatch().Should().HaveCount(1);
        queue.DrainBatch().Should().BeEmpty();
    }

    [Test]
    public async Task DrainBatch_WithRetryAndChannelItems_ReturnsRetryFirst()
    {
        TransactionQueue queue = new(capacity: 10, maxTxDataSize: 95000, awaitTxResult: false);
        TxQueueItem channelItem = CreateItem();
        TxQueueItem retryItem = CreateItem();

        await queue.EnqueueAsync(channelItem);
        queue.PushRetry(retryItem);

        List<TxQueueItem> drained = queue.DrainBatch();
        drained.Should().HaveCount(2);
        drained[0].Should().BeSameAs(retryItem);
        drained[1].Should().BeSameAs(channelItem);
    }

    [Test]
    public void DrainBatch_WithOnlyRetryItems_ReturnsAll()
    {
        TransactionQueue queue = new(capacity: 10, maxTxDataSize: 95000, awaitTxResult: false);
        TxQueueItem retry1 = CreateItem();
        TxQueueItem retry2 = CreateItem();

        queue.PushRetry(retry1);
        queue.PushRetry(retry2);

        List<TxQueueItem> drained = queue.DrainBatch();
        drained.Should().HaveCount(2);
        drained[0].Should().BeSameAs(retry1);
        drained[1].Should().BeSameAs(retry2);
    }

    [Test]
    public async Task DrainBatch_WithMultipleRetryAndChannelItems_ReturnsRetryBeforeChannel()
    {
        TransactionQueue queue = new(capacity: 10, maxTxDataSize: 95000, awaitTxResult: false);
        TxQueueItem channel1 = CreateItem();
        TxQueueItem channel2 = CreateItem();
        TxQueueItem retry1 = CreateItem();
        TxQueueItem retry2 = CreateItem();

        await queue.EnqueueAsync(channel1);
        await queue.EnqueueAsync(channel2);
        queue.PushRetry(retry1);
        queue.PushRetry(retry2);

        List<TxQueueItem> drained = queue.DrainBatch();
        drained.Should().HaveCount(4);
        drained[0].Should().BeSameAs(retry1);
        drained[1].Should().BeSameAs(retry2);
        drained[2].Should().BeSameAs(channel1);
        drained[3].Should().BeSameAs(channel2);
    }

    [Test]
    public async Task EnqueueAsync_WhenAwaitTxResultEnabled_BlocksUntilResultSet()
    {
        TransactionQueue queue = new(capacity: 10, maxTxDataSize: 95000, awaitTxResult: true);
        TxQueueItem item = CreateItem();

        Task<Exception?> enqueueTask = queue.EnqueueAsync(item);

        enqueueTask.IsCompleted.Should().BeFalse();

        item.ReturnResult(null);

        Exception? result = await enqueueTask;
        result.Should().BeNull();
    }

    [Test]
    public async Task EnqueueAsync_WhenAwaitTxResultEnabledAndResultIsError_PropagatesError()
    {
        TransactionQueue queue = new(capacity: 10, maxTxDataSize: 95000, awaitTxResult: true);
        TxQueueItem item = CreateItem();

        Task<Exception?> enqueueTask = queue.EnqueueAsync(item);
        Exception expected = new InvalidOperationException("nonce too low");
        item.ReturnResult(expected);

        Exception? result = await enqueueTask;
        result.Should().BeSameAs(expected);
    }

    [Test]
    public async Task EnqueueAsync_WhenAwaitTxResultDisabled_ReturnsImmediately()
    {
        TransactionQueue queue = new(capacity: 10, maxTxDataSize: 95000, awaitTxResult: false);
        TxQueueItem item = CreateItem();

        Exception? result = await queue.EnqueueAsync(item);

        result.Should().BeNull();
        // ResultChannel should still be pending — not awaited
        item.ResultChannel.Task.IsCompleted.Should().BeFalse();
    }

    [Test]
    public async Task DrainBatch_AfterOversizedEnqueue_ReturnsEmpty()
    {
        TransactionQueue queue = new(capacity: 10, maxTxDataSize: 10, awaitTxResult: false);

        await queue.EnqueueAsync(CreateItem());

        queue.DrainBatch().Should().BeEmpty();
    }

    [Test]
    public async Task DrainBatch_WithOnlyRetryAndEmptyChannel_ReturnsRetryOnly()
    {
        // When retry items exist, the channel's first-read branch is skipped,
        // but remaining channel items are still drained.
        TransactionQueue queue = new(capacity: 10, maxTxDataSize: 95000, awaitTxResult: false);
        TxQueueItem retryItem = CreateItem();

        queue.PushRetry(retryItem);

        List<TxQueueItem> drained = queue.DrainBatch();
        drained.Should().HaveCount(1);
        drained[0].Should().BeSameAs(retryItem);

        // Channel is still empty — next drain returns empty
        queue.DrainBatch().Should().BeEmpty();
    }

    [Test]
    public async Task DrainBatch_WithTimeboostedItem_PreservesTimeboostProperties()
    {
        TransactionQueue queue = new(capacity: 10, maxTxDataSize: 95000, awaitTxResult: false);
        Transaction tx = Build.A.Transaction.TestObject;
        TxQueueItem item = TxQueueItem.CreateTimeboosted(tx, CancellationToken.None, blockStamp: 42);

        await queue.EnqueueAsync(item);

        List<TxQueueItem> drained = queue.DrainBatch();
        drained.Should().HaveCount(1);
        drained[0].IsTimeboosted.Should().BeTrue();
        drained[0].BlockStamp.Should().Be(42UL);
        drained[0].Tx.Should().BeSameAs(tx);
    }

    private static TxQueueItem CreateItem(Transaction? tx = null)
    {
        return new TxQueueItem(tx ?? Build.A.Transaction.TestObject, CancellationToken.None);
    }
}
