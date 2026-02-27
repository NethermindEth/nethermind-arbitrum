// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Core;
using Nethermind.Core.Test.Builders;
using ArbTxExtensions = Nethermind.Arbitrum.Arbos.Compression.TransactionExtensions;

namespace Nethermind.Arbitrum.Test.Arbos.Compression;

/// <summary>
/// Tests for TransactionExtensions static cache operations.
/// </summary>
public class TransactionExtensionsCacheTests
{
    [SetUp]
    public void Setup()
    {
        // Clear a static cache before each test for isolation
        ArbTxExtensions.ClearCache();
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up the static state after each test
        ArbTxExtensions.ClearCache();
    }

    [Test]
    public void GetRawCachedCalldataUnits_NotCached_ReturnsZeros()
    {
        Transaction tx = Build.A.Transaction.TestObject;

        (ulong compressionLevel, ulong calldataUnits) = ArbTxExtensions.GetRawCachedCalldataUnits(tx);

        compressionLevel.Should().Be(0);
        calldataUnits.Should().Be(0);
    }

    [Test]
    public void SetCachedCalldataUnits_ThenGetRaw_ReturnsSetValues()
    {
        Transaction tx = Build.A.Transaction.TestObject;
        const ulong expectedLevel = 5UL;
        const ulong expectedUnits = 12345UL;

        ArbTxExtensions.SetCachedCalldataUnits(tx, expectedLevel, expectedUnits);
        (ulong compressionLevel, ulong calldataUnits) = ArbTxExtensions.GetRawCachedCalldataUnits(tx);

        compressionLevel.Should().Be(expectedLevel);
        calldataUnits.Should().Be(expectedUnits);
    }

    [Test]
    public void GetCachedCalldataUnits_NotCached_ReturnsZero()
    {
        Transaction tx = Build.A.Transaction.TestObject;

        ulong result = ArbTxExtensions.GetCachedCalldataUnits(tx, 5);

        result.Should().Be(0);
    }

    [Test]
    public void GetCachedCalldataUnits_CachedWithMatchingLevel_ReturnsUnits()
    {
        Transaction tx = Build.A.Transaction.TestObject;
        const ulong level = 7UL;
        const ulong units = 9999UL;

        ArbTxExtensions.SetCachedCalldataUnits(tx, level, units);
        ulong result = ArbTxExtensions.GetCachedCalldataUnits(tx, level);

        result.Should().Be(units);
    }

    [Test]
    public void GetCachedCalldataUnits_CachedWithDifferentLevel_ReturnsZero()
    {
        Transaction tx = Build.A.Transaction.TestObject;

        ArbTxExtensions.SetCachedCalldataUnits(tx, 5, 12345);
        ulong result = ArbTxExtensions.GetCachedCalldataUnits(tx, 6); // Different level

        result.Should().Be(0);
    }

    [Test]
    public void SetCachedCalldataUnits_ZeroValues_ClearsCache()
    {
        Transaction tx = Build.A.Transaction.TestObject;

        ArbTxExtensions.SetCachedCalldataUnits(tx, 0, 0);
        (ulong compressionLevel, ulong calldataUnits) = ArbTxExtensions.GetRawCachedCalldataUnits(tx);

        // Zero calldataUnits is treated as empty cache
        calldataUnits.Should().Be(0);
    }

    [Test]
    public void SetCachedCalldataUnits_MaxValidLevel_Works()
    {
        Transaction tx = Build.A.Transaction.TestObject;
        const ulong maxValidLevel = 255UL; // 2^8 - 1
        const ulong units = 1000UL;

        ArbTxExtensions.SetCachedCalldataUnits(tx, maxValidLevel, units);
        ulong result = ArbTxExtensions.GetCachedCalldataUnits(tx, maxValidLevel);

        result.Should().Be(units);
    }

    [Test]
    public void SetCachedCalldataUnits_LevelTooLarge_ClearsCache()
    {
        Transaction tx = Build.A.Transaction.TestObject;
        const ulong tooLargeLevel = 256UL; // >= 2^8
        const ulong units = 1000UL;

        ArbTxExtensions.SetCachedCalldataUnits(tx, tooLargeLevel, units);
        (ulong compressionLevel, ulong calldataUnits) = ArbTxExtensions.GetRawCachedCalldataUnits(tx);

        // Should be cleared (repr = 0)
        compressionLevel.Should().Be(0);
        calldataUnits.Should().Be(0);
    }

    [Test]
    public void SetCachedCalldataUnits_MaxValidUnits_Works()
    {
        Transaction tx = Build.A.Transaction.TestObject;
        const ulong level = 1UL;
        const ulong maxValidUnits = (1UL << 56) - 1; // 2^56 - 1

        ArbTxExtensions.SetCachedCalldataUnits(tx, level, maxValidUnits);
        ulong result = ArbTxExtensions.GetCachedCalldataUnits(tx, level);

        result.Should().Be(maxValidUnits);
    }

    [Test]
    public void SetCachedCalldataUnits_UnitsTooLarge_ClearsCache()
    {
        Transaction tx = Build.A.Transaction.TestObject;
        const ulong level = 1UL;
        const ulong tooLargeUnits = 1UL << 56; // >= 2^56

        ArbTxExtensions.SetCachedCalldataUnits(tx, level, tooLargeUnits);
        (ulong compressionLevel, ulong calldataUnits) = ArbTxExtensions.GetRawCachedCalldataUnits(tx);

        // Should be cleared (repr = 0)
        compressionLevel.Should().Be(0);
        calldataUnits.Should().Be(0);
    }

    [Test]
    public void AllCachedData_AfterClearCache_IsRemoved()
    {
        Transaction tx1 = Build.A.Transaction.WithNonce(1).TestObject;
        Transaction tx2 = Build.A.Transaction.WithNonce(2).TestObject;

        ArbTxExtensions.SetCachedCalldataUnits(tx1, 1, 100);
        ArbTxExtensions.SetCachedCalldataUnits(tx2, 2, 200);

        ArbTxExtensions.ClearCache();

        ArbTxExtensions.GetCachedCalldataUnits(tx1, 1).Should().Be(0);
        ArbTxExtensions.GetCachedCalldataUnits(tx2, 2).Should().Be(0);
    }

    [Test]
    public void ClearCache_OnEmptyCache_DoesNotThrow()
    {
        Action act = ArbTxExtensions.ClearCache;

        act.Should().NotThrow();
    }

    [Test]
    public void ClearCache_MultipleTimes_DoesNotThrow()
    {
        for (int i = 0; i < 10; i++)
            ArbTxExtensions.ClearCache();

        // Should complete without error
        true.Should().BeTrue();
    }

    [Test]
    public void Cache_AcrossTransactions_IsShared()
    {
        // Cache is keyed by transaction hash, so the same hash = the same cache entry
        Transaction tx = Build.A.Transaction.TestObject;

        ArbTxExtensions.SetCachedCalldataUnits(tx, 3, 333);

        // Same transaction instance retrieves from cache
        ulong result = ArbTxExtensions.GetCachedCalldataUnits(tx, 3);
        result.Should().Be(333);
    }

    [Test]
    public void CacheEntries_ForDifferentTransactions_AreSeparate()
    {
        Transaction tx1 = Build.A.Transaction.WithNonce(100).TestObject;
        Transaction tx2 = Build.A.Transaction.WithNonce(200).TestObject;

        ArbTxExtensions.SetCachedCalldataUnits(tx1, 1, 111);
        ArbTxExtensions.SetCachedCalldataUnits(tx2, 2, 222);

        ArbTxExtensions.GetCachedCalldataUnits(tx1, 1).Should().Be(111);
        ArbTxExtensions.GetCachedCalldataUnits(tx2, 2).Should().Be(222);
        ArbTxExtensions.GetCachedCalldataUnits(tx1, 2).Should().Be(0); // Different level
        ArbTxExtensions.GetCachedCalldataUnits(tx2, 1).Should().Be(0); // Different level
    }
}
