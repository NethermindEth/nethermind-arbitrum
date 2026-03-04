// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Evm;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;

namespace Nethermind.Arbitrum.Test.Evm;

/// <summary>
/// Tests for L1BlockCache - per-transaction and static cache operations.
/// </summary>
public class L1BlockCacheTests
{
    [SetUp]
    public void Setup()
    {
        // Clear a static cache before each test for isolation
        //L1BlockCache.ClearStaticCache();
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up the static state after each test
        //L1BlockCache.ClearStaticCache();
    }

    [Test]
    public void GetCachedL1BlockNumber_InitialState_ReturnsNull()
    {
        L1BlockCache cache = new();

        ulong? result = cache.GetCachedL1BlockNumber();

        result.Should().BeNull();
    }

    [Test]
    public void SetCachedL1BlockNumber_ThenGet_ReturnsSetValue()
    {
        L1BlockCache cache = new();
        const ulong expectedBlockNumber = 12345678UL;

        cache.SetCachedL1BlockNumber(expectedBlockNumber);
        ulong? result = cache.GetCachedL1BlockNumber();

        result.Should().Be(expectedBlockNumber);
    }

    [Test]
    public void SetCachedL1BlockNumber_Zero_ReturnsZero()
    {
        L1BlockCache cache = new();

        cache.SetCachedL1BlockNumber(0);
        ulong? result = cache.GetCachedL1BlockNumber();

        result.Should().Be(0);
    }

    [Test]
    public void SetCachedL1BlockNumber_MaxValue_ReturnsMaxValue()
    {
        L1BlockCache cache = new();

        cache.SetCachedL1BlockNumber(ulong.MaxValue);
        ulong? result = cache.GetCachedL1BlockNumber();

        result.Should().Be(ulong.MaxValue);
    }

    [Test]
    public void ClearL1BlockNumberCache_AfterSet_ReturnsNull()
    {
        L1BlockCache cache = new();
        cache.SetCachedL1BlockNumber(12345UL);

        cache.ClearL1BlockNumberCache();
        ulong? result = cache.GetCachedL1BlockNumber();

        result.Should().BeNull();
    }

    [Test]
    public void ClearL1BlockNumberCache_WhenAlreadyNull_RemainsNull()
    {
        L1BlockCache cache = new();

        cache.ClearL1BlockNumberCache();
        ulong? result = cache.GetCachedL1BlockNumber();

        result.Should().BeNull();
    }

    [Test]
    public void BlockNumberCache_AcrossMultipleInstances_IsIndependent()
    {
        L1BlockCache cache1 = new();
        L1BlockCache cache2 = new();

        cache1.SetCachedL1BlockNumber(111UL);
        cache2.SetCachedL1BlockNumber(222UL);

        cache1.GetCachedL1BlockNumber().Should().Be(111UL);
        cache2.GetCachedL1BlockNumber().Should().Be(222UL);
    }

    [Test]
    public void TryGetL1BlockHash_NotCached_ReturnsFalse()
    {
        L1BlockCache cache = new();

        bool found = cache.TryGetL1BlockHash(12345UL, out Hash256 hash);

        found.Should().BeFalse();
        hash.Should().BeNull();
    }

    [Test]
    public void SetL1BlockHash_ThenGet_ReturnsSetHash()
    {
        L1BlockCache cache = new();
        const ulong blockNumber = 12345UL;
        Hash256 expectedHash = TestItem.KeccakA;

        cache.SetL1BlockHash(blockNumber, expectedHash);
        bool found = cache.TryGetL1BlockHash(blockNumber, out Hash256 hash);

        found.Should().BeTrue();
        hash.Should().Be(expectedHash);
    }

    [Test]
    public void SetL1BlockHash_MultipleBlocks_AllRetrievable()
    {
        L1BlockCache cache = new();

        cache.SetL1BlockHash(100UL, TestItem.KeccakA);
        cache.SetL1BlockHash(101UL, TestItem.KeccakB);
        cache.SetL1BlockHash(102UL, TestItem.KeccakC);

        cache.TryGetL1BlockHash(100UL, out Hash256 hash100).Should().BeTrue();
        hash100.Should().Be(TestItem.KeccakA);

        cache.TryGetL1BlockHash(101UL, out Hash256 hash101).Should().BeTrue();
        hash101.Should().Be(TestItem.KeccakB);

        cache.TryGetL1BlockHash(102UL, out Hash256 hash102).Should().BeTrue();
        hash102.Should().Be(TestItem.KeccakC);
    }

    [Test]
    public void BlockHashCache_AcrossInstances_IsShared()
    {
        L1BlockCache cache1 = new();
        L1BlockCache cache2 = new();
        const ulong blockNumber = 54321UL;
        Hash256 expectedHash = TestItem.KeccakD;

        cache1.SetL1BlockHash(blockNumber, expectedHash);
        bool found = cache2.TryGetL1BlockHash(blockNumber, out Hash256 hash);

        found.Should().BeTrue();
        hash.Should().Be(expectedHash);
    }

    [Test]
    public void AllBlockHashes_AfterClearStaticCache_AreRemoved()
    {
        L1BlockCache cache = new();
        cache.SetL1BlockHash(100UL, TestItem.KeccakA);
        cache.SetL1BlockHash(200UL, TestItem.KeccakB);

        cache.ClearStaticCache();

        cache.TryGetL1BlockHash(100UL, out _).Should().BeFalse();
        cache.TryGetL1BlockHash(200UL, out _).Should().BeFalse();
    }

    [Test]
    public void InstanceBlockNumber_AfterClearStaticCache_IsNotAffected()
    {
        L1BlockCache cache = new();
        cache.SetCachedL1BlockNumber(999UL);
        cache.SetL1BlockHash(100UL, TestItem.KeccakA);

        cache.ClearStaticCache();

        // Static hash cache cleared
        cache.TryGetL1BlockHash(100UL, out _).Should().BeFalse();
        // Instance block number preserved
        cache.GetCachedL1BlockNumber().Should().Be(999UL);
    }

    [Test]
    public void SetL1BlockHash_OverwriteExisting_UpdatesValue()
    {
        L1BlockCache cache = new();
        const ulong blockNumber = 12345UL;

        cache.SetL1BlockHash(blockNumber, TestItem.KeccakA);
        cache.SetL1BlockHash(blockNumber, TestItem.KeccakB);

        cache.TryGetL1BlockHash(blockNumber, out Hash256 hash).Should().BeTrue();
        hash.Should().Be(TestItem.KeccakB);
    }
}
