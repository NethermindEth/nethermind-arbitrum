// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Diagnostics;
using FluentAssertions;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Sequencer.Queues;
using Nethermind.Arbitrum.Sequencer.Timeboost;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.JsonRpc;
using NSubstitute;

namespace Nethermind.Arbitrum.Test.Sequencer.Timeboost;

[TestFixture]
public class ExpressLanePriorityTests
{
    private static IExpressLaneTracker CreateTrackerWithController()
    {
        IExpressLaneTracker tracker = Substitute.For<IExpressLaneTracker>();
        tracker.CurrentRoundHasController().Returns(true);
        return tracker;
    }

    private static IExpressLaneTracker CreateTrackerWithoutController()
    {
        IExpressLaneTracker tracker = Substitute.For<IExpressLaneTracker>();
        tracker.CurrentRoundHasController().Returns(false);
        return tracker;
    }

    [Test]
    public async Task RegularTx_DelayedBeforeQueue_WhenControllerExists()
    {
        TransactionQueue queue = new(10, 95000, false, 200, CreateTrackerWithController());
        TxQueueItem item = new(Build.A.Transaction.TestObject, CancellationToken.None);

        Stopwatch sw = Stopwatch.StartNew();
        await queue.EnqueueAsync(item);
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeGreaterOrEqualTo(150, "regular tx should be delayed by ~200ms");
        List<TxQueueItem> drained = queue.DrainBatch();
        drained.Should().HaveCount(1);
        drained[0].Should().BeSameAs(item);
    }

    [Test]
    public async Task RegularTx_NotDelayed_WhenNoController()
    {
        TransactionQueue queue = new(10, 95000, false, 200, CreateTrackerWithoutController());
        TxQueueItem item = new(Build.A.Transaction.TestObject, CancellationToken.None);

        Stopwatch sw = Stopwatch.StartNew();
        await queue.EnqueueAsync(item);
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(50, "no controller means no delay");
    }

    [Test]
    public async Task ExpressLaneTx_WhenControllerExists_NeverDelayed()
    {
        TransactionQueue queue = new(10, 95000, false, 200, CreateTrackerWithController());
        TxQueueItem item = TxQueueItem.CreateTimeboosted(Build.A.Transaction.TestObject, CancellationToken.None, blockStamp: 1);

        Stopwatch sw = Stopwatch.StartNew();
        await queue.EnqueueAsync(item);
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(50, "express lane tx should never be delayed");
    }

    [Test]
    public async Task ExpressLaneTx_EnqueuedAfterRegularTx_DrainedFirst()
    {
        TransactionQueue queue = new(10, 95000, false, 200, CreateTrackerWithController());

        Transaction regularTx = Build.A.Transaction.WithNonce(0).TestObject;
        Transaction expressLaneTx = Build.A.Transaction.WithNonce(1).TestObject;

        TxQueueItem regularItem = new(regularTx, CancellationToken.None);
        TxQueueItem expressItem = TxQueueItem.CreateTimeboosted(expressLaneTx, CancellationToken.None, blockStamp: 1);

        // Enqueue regular tx first — it will be delayed 200ms
        Task<ResultWrapper<Hash256>> regularTask = queue.EnqueueAsync(regularItem);

        // Wait a bit, then enqueue express lane tx — it goes straight through
        await Task.Delay(50);
        await queue.EnqueueAsync(expressItem);

        // Wait for regular tx to finish its delay
        await regularTask;

        List<TxQueueItem> drained = queue.DrainBatch();
        drained.Should().HaveCount(2);
        drained[0].Should().BeSameAs(expressItem, "express lane tx should be first despite being enqueued later");
        drained[1].Should().BeSameAs(regularItem, "regular tx should be second due to delay");
    }
}
