// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Test.Builders;

namespace Nethermind.Arbitrum.Test.Sequencer;

public class NonceFailureCacheTests
{
    [Test]
    public void Add_WhenFull_EvictsOldestEntry()
    {
        NonceFailureCache cache = new(maxSize: 2);
        TxQueueItem oldest = CreateItem();
        TxQueueItem middle = CreateItem();
        TxQueueItem newest = CreateItem();

        cache.Add(FullChainSimulationAccounts.AccountA.Address, 1, oldest);
        cache.Add(FullChainSimulationAccounts.AccountA.Address, 2, middle);

        // Cache is full (2 items). Adding a third should evict the oldest.
        cache.Add(FullChainSimulationAccounts.AccountB.Address, 1, newest);

        // Oldest entry should have been evicted with overflow error
        oldest.ResultChannel.Task.IsCompleted.Should().BeTrue();
        oldest.ResultChannel.Task.Result.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("overflow");

        // Middle and newest should still be in the cache (revivable)
        cache.TryRevive(FullChainSimulationAccounts.AccountA.Address, 2, out TxQueueItem? revivedMiddle).Should().BeTrue();
        revivedMiddle.Should().BeSameAs(middle);

        cache.TryRevive(FullChainSimulationAccounts.AccountB.Address, 1, out TxQueueItem? revivedNewest).Should().BeTrue();
        revivedNewest.Should().BeSameAs(newest);
    }

    [Test]
    public void Add_DuplicateKey_RejectsWithError()
    {
        NonceFailureCache cache = new(maxSize: 10);
        TxQueueItem first = CreateItem();
        TxQueueItem duplicate = CreateItem();

        cache.Add(FullChainSimulationAccounts.AccountA.Address, 5, first);
        cache.Add(FullChainSimulationAccounts.AccountA.Address, 5, duplicate);

        // First should remain in cache (no result yet)
        first.ResultChannel.Task.IsCompleted.Should().BeFalse();

        // Duplicate should be rejected immediately
        duplicate.ResultChannel.Task.IsCompleted.Should().BeTrue();
        duplicate.ResultChannel.Task.Result.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("Nonce too high");
    }

    [Test]
    public void Add_ExpiredOnArrival_RejectsWithError()
    {
        // Negative expiry guarantees any item created at current time is already expired
        NonceFailureCache cache = new(maxSize: 10, expiry: TimeSpan.FromSeconds(-1));
        TxQueueItem item = CreateItem();

        cache.Add(FullChainSimulationAccounts.AccountA.Address, 1, item);

        item.ResultChannel.Task.IsCompleted.Should().BeTrue();
        item.ResultChannel.Task.Result.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("Nonce too high");
    }

    [Test]
    public void TryRevive_ExistingEntry_RemovesAndReturns()
    {
        NonceFailureCache cache = new(maxSize: 10);
        TxQueueItem item = CreateItem();

        cache.Add(FullChainSimulationAccounts.AccountA.Address, 3, item);

        cache.TryRevive(FullChainSimulationAccounts.AccountA.Address, 3, out TxQueueItem? revived).Should().BeTrue();
        revived.Should().BeSameAs(item);

        // Should be removed — second revive should fail
        cache.TryRevive(FullChainSimulationAccounts.AccountA.Address, 3, out _).Should().BeFalse();
    }

    [Test]
    public void TryRevive_MissingKey_ReturnsFalse()
    {
        NonceFailureCache cache = new(maxSize: 10);

        cache.TryRevive(FullChainSimulationAccounts.AccountA.Address, 99, out TxQueueItem? result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Test]
    public void EvictExpired_WithMixedExpiryTimes_RemovesOnlyExpired()
    {
        NonceFailureCache cache = new(maxSize: 10, expiry: TimeSpan.FromMilliseconds(50));

        TxQueueItem expiredA = CreateItem();
        TxQueueItem expiredB = CreateItem();
        cache.Add(FullChainSimulationAccounts.AccountA.Address, 1, expiredA);
        cache.Add(FullChainSimulationAccounts.AccountA.Address, 2, expiredB);

        // Wait for first batch to expire
        Thread.Sleep(100);

        TxQueueItem alive = CreateItem();
        cache.Add(FullChainSimulationAccounts.AccountB.Address, 1, alive);

        cache.EvictExpired();

        // Expired entries should have received error results
        expiredA.ResultChannel.Task.IsCompleted.Should().BeTrue();
        expiredA.ResultChannel.Task.Result.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("expired");

        expiredB.ResultChannel.Task.IsCompleted.Should().BeTrue();
        expiredB.ResultChannel.Task.Result.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("expired");

        // Alive entry should still be in cache
        alive.ResultChannel.Task.IsCompleted.Should().BeFalse();
        cache.TryRevive(FullChainSimulationAccounts.AccountB.Address, 1, out TxQueueItem? revived).Should().BeTrue();
        revived.Should().BeSameAs(alive);
    }

    [Test]
    public void EvictExpired_WithLaterNonExpiredEntries_StopsAtFirstNonExpired()
    {
        NonceFailureCache cache = new(maxSize: 10, expiry: TimeSpan.FromMilliseconds(50));

        TxQueueItem first = CreateItem();
        TxQueueItem second = CreateItem();
        cache.Add(FullChainSimulationAccounts.AccountA.Address, 1, first);
        cache.Add(FullChainSimulationAccounts.AccountA.Address, 2, second);

        // Wait for first two to expire
        Thread.Sleep(100);

        TxQueueItem third = CreateItem();
        TxQueueItem fourth = CreateItem();
        cache.Add(FullChainSimulationAccounts.AccountB.Address, 1, third);
        cache.Add(FullChainSimulationAccounts.AccountB.Address, 2, fourth);

        cache.EvictExpired();

        // First two expired — should have results
        first.ResultChannel.Task.IsCompleted.Should().BeTrue();
        second.ResultChannel.Task.IsCompleted.Should().BeTrue();

        // Third and fourth should be untouched (EvictExpired stops at first non-expired)
        third.ResultChannel.Task.IsCompleted.Should().BeFalse();
        fourth.ResultChannel.Task.IsCompleted.Should().BeFalse();
    }

    [Test]
    public void Clear_WithMultipleEntries_ReturnsErrorToAllEntries()
    {
        NonceFailureCache cache = new(maxSize: 10);
        TxQueueItem itemA = CreateItem();
        TxQueueItem itemB = CreateItem();
        TxQueueItem itemC = CreateItem();

        cache.Add(FullChainSimulationAccounts.AccountA.Address, 1, itemA);
        cache.Add(FullChainSimulationAccounts.AccountB.Address, 1, itemB);
        cache.Add(FullChainSimulationAccounts.AccountC.Address, 1, itemC);

        cache.Clear();

        // All entries should receive error results
        foreach (TxQueueItem item in new[] { itemA, itemB, itemC })
        {
            item.ResultChannel.Task.IsCompleted.Should().BeTrue();
            item.ResultChannel.Task.Result.Should().BeOfType<InvalidOperationException>()
                .Which.Message.Should().Contain("cleared");
        }

        // Cache should be empty
        cache.TryRevive(FullChainSimulationAccounts.AccountA.Address, 1, out _).Should().BeFalse();
        cache.TryRevive(FullChainSimulationAccounts.AccountB.Address, 1, out _).Should().BeFalse();
        cache.TryRevive(FullChainSimulationAccounts.AccountC.Address, 1, out _).Should().BeFalse();
    }

    private static TxQueueItem CreateItem()
    {
        return TxQueueItem.CreateRegular(Build.A.Transaction.TestObject);
    }
}
