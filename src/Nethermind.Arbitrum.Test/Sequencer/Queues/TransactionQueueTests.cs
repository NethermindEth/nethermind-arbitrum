// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Sequencer.Queues;
using Nethermind.Arbitrum.Sequencer.Timeboost;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Test.Sequencer.Queues;

[TestFixture]
public class TransactionQueueTests
{
    [Test]
    public async Task DrainBatch_AfterSingleEnqueue_ReturnsThatItem()
    {
        TransactionQueue queue = new(new ArbitrumConfig { SequencerMaxTxQueueSize = 10 }, new DisabledExpressLaneTracker(), TimeProvider.System);
        TxQueueItem item = CreateItem();

        ResultWrapper<Hash256> result = await queue.EnqueueAsync(item);

        result.Should().RequestSucceed();
        List<TxQueueItem> drained = queue.DrainBatch();
        drained.Should().HaveCount(1);
        drained[0].Should().BeSameAs(item);
    }

    [Test]
    public async Task DrainBatch_AfterMultipleEnqueues_ReturnsAllInFifoOrder()
    {
        TransactionQueue queue = new(new ArbitrumConfig { SequencerMaxTxQueueSize = 10 }, new DisabledExpressLaneTracker(), TimeProvider.System);
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
        TransactionQueue queue = new(new ArbitrumConfig { SequencerMaxTxQueueSize = 10, SequencerMaxTxDataSize = 10 }, new DisabledExpressLaneTracker(), TimeProvider.System);
        TxQueueItem item = CreateItem();

        ResultWrapper<Hash256> result = await queue.EnqueueAsync(item);

        result.Should().RequestFail("exceeds maximum");
    }

    [Test]
    public async Task EnqueueAsync_WhenQueueFull_BlocksUntilTimeout()
    {
        TransactionQueue queue = new(new ArbitrumConfig { SequencerMaxTxQueueSize = 1 }, new DisabledExpressLaneTracker(), TimeProvider.System);

        await queue.EnqueueAsync(CreateItem());

        ResultWrapper<Hash256> result = await queue.EnqueueAsync(
            TxQueueItem.CreateRegular(Build.A.Transaction.TestObject, TimeSpan.FromMilliseconds(100)));

        result.Should().RequestFail("timeout");
    }

    [Test]
    public void DrainBatch_WhenQueueEmpty_ReturnsEmptyList()
    {
        TransactionQueue queue = new(new ArbitrumConfig { SequencerMaxTxQueueSize = 10 }, new DisabledExpressLaneTracker(), TimeProvider.System);

        List<TxQueueItem> drained = queue.DrainBatch();

        drained.Should().BeEmpty();
    }

    [Test]
    public async Task DrainBatch_AfterPreviousDrain_ReturnsEmpty()
    {
        TransactionQueue queue = new(new ArbitrumConfig { SequencerMaxTxQueueSize = 10 }, new DisabledExpressLaneTracker(), TimeProvider.System);
        await queue.EnqueueAsync(CreateItem());

        queue.DrainBatch().Should().HaveCount(1);
        queue.DrainBatch().Should().BeEmpty();
    }

    [Test]
    public async Task DrainBatch_WithRetryAndChannelItems_ReturnsRetryFirst()
    {
        TransactionQueue queue = new(new ArbitrumConfig { SequencerMaxTxQueueSize = 10 }, new DisabledExpressLaneTracker(), TimeProvider.System);
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
        TransactionQueue queue = new(new ArbitrumConfig { SequencerMaxTxQueueSize = 10 }, new DisabledExpressLaneTracker(), TimeProvider.System);
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
        TransactionQueue queue = new(new ArbitrumConfig { SequencerMaxTxQueueSize = 10 }, new DisabledExpressLaneTracker(), TimeProvider.System);
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
        TransactionQueue queue = new(new ArbitrumConfig { SequencerMaxTxQueueSize = 10, SequencerAwaitTxResult = true }, new DisabledExpressLaneTracker(), TimeProvider.System);
        TxQueueItem item = CreateItem();

        Task<ResultWrapper<Hash256>> enqueueTask = queue.EnqueueAsync(item);

        enqueueTask.IsCompleted.Should().BeFalse();

        item.ReturnResult(null);

        ResultWrapper<Hash256> result = await enqueueTask;
        result.Should().RequestSucceed();
        result.Data.Should().Be(item.Tx.Hash!);
    }

    [Test]
    public async Task EnqueueAsync_WhenAwaitTxResultEnabledAndResultIsError_PropagatesError()
    {
        TransactionQueue queue = new(new ArbitrumConfig { SequencerMaxTxQueueSize = 10, SequencerAwaitTxResult = true }, new DisabledExpressLaneTracker(), TimeProvider.System);
        TxQueueItem item = CreateItem();

        Task<ResultWrapper<Hash256>> enqueueTask = queue.EnqueueAsync(item);
        item.ReturnResult(new InvalidOperationException("nonce too low"));

        ResultWrapper<Hash256> result = await enqueueTask;
        result.Should().RequestFail("nonce too low");
    }

    [Test]
    public async Task EnqueueAsync_WhenAwaitTxResultDisabled_ReturnsImmediately()
    {
        TransactionQueue queue = new(new ArbitrumConfig { SequencerMaxTxQueueSize = 10 }, new DisabledExpressLaneTracker(), TimeProvider.System);
        TxQueueItem item = CreateItem();

        ResultWrapper<Hash256> result = await queue.EnqueueAsync(item);

        result.Should().RequestSucceed();
        result.Data.Should().Be(item.Tx.Hash!);
        // ResultChannel should still be pending — not awaited
        item.ResultChannel.Task.IsCompleted.Should().BeFalse();
    }

    [Test]
    public async Task DrainBatch_AfterOversizedEnqueue_ReturnsEmpty()
    {
        TransactionQueue queue = new(new ArbitrumConfig { SequencerMaxTxQueueSize = 10, SequencerMaxTxDataSize = 10 }, new DisabledExpressLaneTracker(), TimeProvider.System);

        await queue.EnqueueAsync(CreateItem());

        queue.DrainBatch().Should().BeEmpty();
    }

    [Test]
    public async Task DrainBatch_WithOnlyRetryAndEmptyChannel_ReturnsRetryOnly()
    {
        // When retry items exist, the channel's first-read branch is skipped,
        // but remaining channel items are still drained.
        TransactionQueue queue = new(new ArbitrumConfig { SequencerMaxTxQueueSize = 10 }, new DisabledExpressLaneTracker(), TimeProvider.System);
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
        TransactionQueue queue = new(new ArbitrumConfig { SequencerMaxTxQueueSize = 10 }, new DisabledExpressLaneTracker(), TimeProvider.System);
        Transaction tx = Build.A.Transaction.TestObject;
        TxQueueItem item = TxQueueItem.CreateTimeboosted(tx, blockStamp: 42);

        await queue.EnqueueAsync(item);

        List<TxQueueItem> drained = queue.DrainBatch();
        drained.Should().HaveCount(1);
        drained[0].IsTimeboosted.Should().BeTrue();
        drained[0].BlockStamp.Should().Be(42UL);
        drained[0].Tx.Should().BeSameAs(tx);
    }

    [Test]
    public async Task EnqueueAsync_RegularTxWithController_DelayedBeforeEnqueue()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(
            out TestExpressLaneTrackerContext trackerContext,
            setup: c => { c.SequencerMaxTxQueueSize = 10; c.TimeboostEnabled = true; });
        await trackerContext.AdvanceLoop(new ResolvedRound(TestItem.AddressB, trackerContext.CurrentRound));
        TransactionQueue queue = new(trackerContext.Config, tracker, trackerContext.Timing.TimeProvider);
        TxQueueItem item = TxQueueItem.CreateRegular(Build.A.Transaction.TestObject);

        Task<ResultWrapper<Hash256>> enqueueTask = queue.EnqueueAsync(item);

        enqueueTask.IsCompleted.Should().BeFalse("regular tx should be delayed when controller exists");

        trackerContext.AdvanceTime(TimeSpan.FromMilliseconds(200));
        ResultWrapper<Hash256> result = await enqueueTask;

        result.Should().RequestSucceed();
        List<TxQueueItem> drained = queue.DrainBatch();
        drained.Should().HaveCount(1);
        drained[0].Should().BeSameAs(item);
    }

    [Test]
    public async Task EnqueueAsync_RegularTxWithNoController_NotDelayed()
    {
        ManualTimeProvider timeProvider = new(DateTimeOffset.UtcNow);
        TransactionQueue queue = new(new ArbitrumConfig { SequencerMaxTxQueueSize = 10, TimeboostEnabled = true }, new DisabledExpressLaneTracker(), timeProvider);
        TxQueueItem item = TxQueueItem.CreateRegular(Build.A.Transaction.TestObject);

        Task<ResultWrapper<Hash256>> enqueueTask = queue.EnqueueAsync(item);

        enqueueTask.IsCompleted.Should().BeTrue("no controller means no delay");
        ResultWrapper<Hash256> result = await enqueueTask;
        result.Should().RequestSucceed();
    }

    [Test]
    public async Task EnqueueAsync_TimeboostedTxWithController_NotDelayed()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(
            out TestExpressLaneTrackerContext trackerContext,
            setup: c => { c.SequencerMaxTxQueueSize = 10; c.TimeboostEnabled = true; });
        await trackerContext.AdvanceLoop(new ResolvedRound(TestItem.AddressB, trackerContext.CurrentRound));
        TransactionQueue queue = new(trackerContext.Config, tracker, trackerContext.Timing.TimeProvider);
        TxQueueItem item = TxQueueItem.CreateTimeboosted(Build.A.Transaction.TestObject, blockStamp: 1);

        Task<ResultWrapper<Hash256>> enqueueTask = queue.EnqueueAsync(item);

        enqueueTask.IsCompleted.Should().BeTrue("timeboosted tx should never be delayed");
        ResultWrapper<Hash256> result = await enqueueTask;
        result.Should().RequestSucceed();
    }

    [Test]
    public async Task DrainBatch_TimeboostedAfterRegularWithController_TimeboostedFirst()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(
            out TestExpressLaneTrackerContext trackerContext,
            setup: c => { c.SequencerMaxTxQueueSize = 10; c.TimeboostEnabled = true; });
        await trackerContext.AdvanceLoop(new ResolvedRound(TestItem.AddressB, trackerContext.CurrentRound));
        TransactionQueue queue = new(trackerContext.Config, tracker, trackerContext.Timing.TimeProvider);

        TxQueueItem regularItem = TxQueueItem.CreateRegular(Build.A.Transaction.WithNonce(0).TestObject);
        TxQueueItem expressItem = TxQueueItem.CreateTimeboosted(Build.A.Transaction.WithNonce(1).TestObject, blockStamp: 1);

        Task<ResultWrapper<Hash256>> regularTask = queue.EnqueueAsync(regularItem);
        await queue.EnqueueAsync(expressItem);

        trackerContext.AdvanceTime(TimeSpan.FromMilliseconds(200));
        await regularTask;

        List<TxQueueItem> drained = queue.DrainBatch();
        drained.Should().HaveCount(2);
        drained[0].Should().BeSameAs(expressItem, "timeboosted tx should be first despite being enqueued later");
        drained[1].Should().BeSameAs(regularItem, "regular tx should be second due to delay");
    }

    private static TxQueueItem CreateItem(Transaction? tx = null)
    {
        return TxQueueItem.CreateRegular(tx ?? Build.A.Transaction.TestObject);
    }
}
