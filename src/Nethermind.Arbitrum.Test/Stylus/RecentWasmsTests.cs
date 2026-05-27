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

    [TestCaseSource(nameof(InsertScenarios))]
    public void Insert_FollowsLruSemantics(ushort capacity, (ValueHash256 Hash, bool ExpectedHit)[] steps)
    {
        RecentWasms cache = new();

        for (int i = 0; i < steps.Length; i++)
        {
            (ValueHash256 hash, bool expectedHit) = steps[i];
            cache.Insert(in hash, capacity).Should().Be(expectedHit, $"step {i} (hash {hash})");
        }
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

    public static IEnumerable<TestCaseData> InsertScenarios()
    {
        yield return new TestCaseData(DefaultCapacity, new (ValueHash256 Hash, bool ExpectedHit)[]
        {
            (HashA, false),
        }).SetName("FirstCall_ReturnsFalse");

        yield return new TestCaseData(DefaultCapacity, new (ValueHash256 Hash, bool ExpectedHit)[]
        {
            (HashA, false),
            (HashA, true),
        }).SetName("SecondCallSameHash_ReturnsTrue");

        yield return new TestCaseData(DefaultCapacity, new (ValueHash256 Hash, bool ExpectedHit)[]
        {
            (HashA, false),
            (HashB, false),
            (HashC, false),
        }).SetName("DistinctHashes_AllReturnFalse");

        // Mirror Nitro `lru.NewBasicLRU(int(retain))` which clamps non-positive capacity to 1.
        yield return new TestCaseData((ushort)0, new (ValueHash256 Hash, bool ExpectedHit)[]
        {
            (HashA, false),
            (HashA, true),
            (HashB, false), // second distinct hash evicts the only slot's entry
            (HashA, false), // A was evicted
        }).SetName("CapacityZero_BehavesLikeCapacityOne");

        yield return new TestCaseData((ushort)2, new (ValueHash256 Hash, bool ExpectedHit)[]
        {
            (HashA, false),
            (HashB, false),
            (HashC, false), // overflows: evicts A (LRU)
            (HashB, true),  // B survived
            (HashC, true),  // C survived
        }).SetName("BeyondCapacity_EvictsLeastRecentlyUsed");

        yield return new TestCaseData((ushort)2, new (ValueHash256 Hash, bool ExpectedHit)[]
        {
            (HashA, false),
            (HashB, false),
            (HashA, true),  // promotes A
            (HashC, false), // evicts B (now LRU), not A
            (HashA, true),  // A survived
            (HashB, false), // B was evicted
        }).SetName("HitOnExistingEntry_PromotesEntry");
    }

    private static ValueHash256 HashOf(byte seed)
    {
        byte[] bytes = new byte[32];
        bytes[0] = seed;
        return new ValueHash256(bytes);
    }
}
