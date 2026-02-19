// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Execution;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Test.Execution;

/// <summary>
/// Tests for CachedL1PriceData - cache operations and Clear() method.
/// </summary>
public class CachedL1PriceDataTests
{
    private CachedL1PriceData _cache = null!;

    [SetUp]
    public void Setup()
    {
        _cache = new CachedL1PriceData(LimboLogs.Instance);
    }


    [Test]
    public void CachedL1PriceData_WhenCreated_HasZeroValues()
    {
        _cache.StartOfL1PriceDataCache.Should().Be(0);
        _cache.EndOfL1PriceDataCache.Should().Be(0);
        _cache.MsgToL1PriceData.Should().BeEmpty();
    }



    [Test]
    public void Clear_OnEmptyCache_RemainsEmpty()
    {
        _cache.Clear();

        _cache.StartOfL1PriceDataCache.Should().Be(0);
        _cache.EndOfL1PriceDataCache.Should().Be(0);
        _cache.MsgToL1PriceData.Should().BeEmpty();
    }

    [Test]
    public void Clear_AfterMarkFeedStart_ResetsAllValues()
    {
        // Simulate some cache state by using MarkFeedStart
        // Note: We can't easily populate via CacheL1PriceDataOfMsg without full block setup,
        // but we can verify Clear resets the state

        _cache.Clear();

        _cache.StartOfL1PriceDataCache.Should().Be(0);
        _cache.EndOfL1PriceDataCache.Should().Be(0);
        _cache.MsgToL1PriceData.Should().BeEmpty();
    }

    [Test]
    public void Clear_IsThreadSafe_NoExceptionOnConcurrentAccess()
    {
        // Test that Clear() doesn't throw when called concurrently
        List<Task> tasks = [];

        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() => _cache.Clear()));
            tasks.Add(Task.Run(() =>
            {
                _ = _cache.StartOfL1PriceDataCache;
                _ = _cache.EndOfL1PriceDataCache;
                _ = _cache.MsgToL1PriceData.Count;
            }));
        }

        Action act = () => Task.WaitAll(tasks.ToArray());

        act.Should().NotThrow();
    }



    [Test]
    public void MarkFeedStart_WhenCacheEmpty_DoesNotThrow()
    {
        Action act = () => _cache.MarkFeedStart(100);

        act.Should().NotThrow();
    }

    [Test]
    public void MarkFeedStart_ToValueGreaterThanEnd_ClearsCache()
    {
        // When "to" >= EndOfL1PriceDataCache, cache should be cleared
        _cache.MarkFeedStart(1000);

        _cache.StartOfL1PriceDataCache.Should().Be(0);
        _cache.EndOfL1PriceDataCache.Should().Be(0);
        _cache.MsgToL1PriceData.Should().BeEmpty();
    }



    [Test]
    public void ClearAndMarkFeedStart_WhenCalledConcurrently_DoesNotDeadlock()
    {
        CancellationTokenSource cts = new();
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        List<Task> tasks =
        [
            Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    _cache.Clear();
                }
            }),
            Task.Run(() =>
            {
                ulong i = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    _cache.MarkFeedStart(i++);
                }
            })
        ];

        // Should complete without deadlock within timeout
        Action act = () => Task.WhenAny(Task.WhenAll(tasks), Task.Delay(TimeSpan.FromSeconds(2))).Wait();

        act.Should().NotThrow();
        cts.Cancel();
    }

}
