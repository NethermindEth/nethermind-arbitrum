// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Test.Stylus;

public class RecentWasmsTests
{
    private const ushort DefaultCapacity = 32;

    private static readonly ValueHash256 HashA = HashOf(0xA);
    private static readonly ValueHash256 HashB = HashOf(0xB);
    private static readonly ValueHash256 HashC = HashOf(0xC);

    [Test]
    public void Insert_FirstCall_ReturnsFalse()
    {
        RecentWasms cache = new();

        bool hit = cache.Insert(in HashA, DefaultCapacity);

        hit.Should().BeFalse();
    }

    [Test]
    public void Insert_SecondCallSameHash_ReturnsTrue()
    {
        RecentWasms cache = new();
        cache.Insert(in HashA, DefaultCapacity);

        bool hit = cache.Insert(in HashA, DefaultCapacity);

        hit.Should().BeTrue();
    }

    [Test]
    public void Insert_DistinctHashes_AllReturnFalse()
    {
        RecentWasms cache = new();

        cache.Insert(in HashA, DefaultCapacity).Should().BeFalse();
        cache.Insert(in HashB, DefaultCapacity).Should().BeFalse();
        cache.Insert(in HashC, DefaultCapacity).Should().BeFalse();
    }

    [Test]
    public void Insert_CapacityZero_BehavesLikeCapacityOne()
    {
        // Mirror Nitro `lru.NewBasicLRU(int(retain))` which clamps non-positive capacity to 1.
        RecentWasms cache = new();

        cache.Insert(in HashA, 0).Should().BeFalse();
        cache.Insert(in HashA, 0).Should().BeTrue();
        // Inserting a second distinct hash evicts the only slot's entry.
        cache.Insert(in HashB, 0).Should().BeFalse();
        cache.Insert(in HashA, 0).Should().BeFalse();
    }

    [Test]
    public void Insert_BeyondCapacity_EvictsLeastRecentlyUsed()
    {
        RecentWasms cache = new();
        const ushort capacity = 2;

        cache.Insert(in HashA, capacity); // cache=[A]
        cache.Insert(in HashB, capacity); // cache=[A,B], LRU=A
        cache.Insert(in HashC, capacity); // overflows: evicts A, cache=[B,C]

        // B and C survived the overflow; A was evicted.
        cache.Insert(in HashB, capacity).Should().BeTrue();
        cache.Insert(in HashC, capacity).Should().BeTrue();
    }

    [Test]
    public void Insert_HitOnExistingEntry_PromotesEntry()
    {
        RecentWasms cache = new();
        const ushort capacity = 2;

        cache.Insert(in HashA, capacity);
        cache.Insert(in HashB, capacity);
        cache.Insert(in HashA, capacity).Should().BeTrue(); // promotes A
        cache.Insert(in HashC, capacity);                   // evicts B (now LRU), not A

        cache.Insert(in HashA, capacity).Should().BeTrue();  // A survived
        cache.Insert(in HashB, capacity).Should().BeFalse(); // B was evicted
    }

    [Test]
    public void Clear_PopulatedCache_ReturnsFalseOnNextInsert()
    {
        RecentWasms cache = new();
        cache.Insert(in HashA, DefaultCapacity);
        cache.Insert(in HashB, DefaultCapacity);

        cache.Clear();

        cache.Insert(in HashA, DefaultCapacity).Should().BeFalse();
        cache.Insert(in HashB, DefaultCapacity).Should().BeFalse();
    }

    [Test]
    public void Clear_UnallocatedCache_DoesNotThrow()
    {
        RecentWasms cache = new();

        Action act = () => cache.Clear();

        act.Should().NotThrow();
    }

    private static ValueHash256 HashOf(byte seed)
    {
        byte[] bytes = new byte[32];
        bytes[0] = seed;
        return new ValueHash256(bytes);
    }
}
