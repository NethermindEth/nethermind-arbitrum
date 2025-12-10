// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Execution;

namespace Nethermind.Arbitrum.Test.Rpc;

[TestFixture]
public sealed class SyncHistoryTests
{
    [Test]
    public void Constructor_WithZeroMsgLag_ThrowsArgumentOutOfRangeException()
    {
        Action act = () => new SyncHistory(TimeSpan.Zero);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*msgLag must be positive*");
    }

    [Test]
    public void Constructor_WithNegativeMsgLag_ThrowsArgumentOutOfRangeException()
    {
        Action act = () => new SyncHistory(TimeSpan.FromSeconds(-1));

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*msgLag must be positive*");
    }

    [Test]
    public void GetSyncTarget_WithNoEntries_ReturnsZero()
    {
        using SyncHistory syncHistory = new(TimeSpan.FromMinutes(5));

        ulong target = syncHistory.GetSyncTarget(DateTimeOffset.UtcNow);

        target.Should().Be(0);
    }

    [Test]
    public void Add_WithSingleEntry_StoresEntry()
    {
        using SyncHistory syncHistory = new(TimeSpan.FromMinutes(5));
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        syncHistory.Add(100, timestamp);
        ulong target = syncHistory.GetSyncTarget(timestamp);

        target.Should().Be(100);
    }

    [Test]
    public void GetSyncTarget_WithEntryWithinWindow_ReturnsMaxMessageCount()
    {
        using SyncHistory syncHistory = new(TimeSpan.FromMinutes(5));
        DateTimeOffset baseTime = DateTimeOffset.UtcNow;

        syncHistory.Add(100, baseTime);
        ulong target = syncHistory.GetSyncTarget(baseTime.AddMinutes(2));

        target.Should().Be(100);
    }

    [Test]
    public void GetSyncTarget_WithEntryOutsideWindow_ReturnsZero()
    {
        using SyncHistory syncHistory = new(TimeSpan.FromMinutes(5));
        DateTimeOffset baseTime = DateTimeOffset.UtcNow;

        syncHistory.Add(100, baseTime);
        ulong target = syncHistory.GetSyncTarget(baseTime.AddMinutes(6));

        target.Should().Be(0);
    }

    [Test]
    public void Add_WithMultipleEntriesWithinWindow_ReturnsOldestEntry()
    {
        using SyncHistory syncHistory = new(TimeSpan.FromMinutes(5));
        DateTimeOffset baseTime = DateTimeOffset.UtcNow;

        syncHistory.Add(100, baseTime);
        syncHistory.Add(200, baseTime.AddMinutes(1));
        syncHistory.Add(300, baseTime.AddMinutes(2));

        ulong target = syncHistory.GetSyncTarget(baseTime.AddMinutes(3));

        target.Should().Be(100);
    }

    [Test]
    public void Add_WithEntriesSpanningWindow_TrimsOldEntries()
    {
        using SyncHistory syncHistory = new(TimeSpan.FromMinutes(5));
        DateTimeOffset baseTime = DateTimeOffset.UtcNow;

        syncHistory.Add(100, baseTime);
        syncHistory.Add(200, baseTime.AddMinutes(3));
        syncHistory.Add(300, baseTime.AddMinutes(6));

        ulong target = syncHistory.GetSyncTarget(baseTime.AddMinutes(6));

        target.Should().Be(200);
    }

    [Test]
    public void Add_WithOldEntriesBeforeWindow_TrimsOnAdd()
    {
        using SyncHistory syncHistory = new(TimeSpan.FromMinutes(5));
        DateTimeOffset baseTime = DateTimeOffset.UtcNow;

        syncHistory.Add(100, baseTime);
        syncHistory.Add(200, baseTime.AddMinutes(2));
        syncHistory.Add(300, baseTime.AddMinutes(7));

        ulong target = syncHistory.GetSyncTarget(baseTime.AddMinutes(7));

        target.Should().Be(200); // Changed from 300 to 200
    }

    [Test]
    public void GetSyncTarget_WithExactWindowBoundary_ReturnsEntry()
    {
        using SyncHistory syncHistory = new(TimeSpan.FromMinutes(5));
        DateTimeOffset baseTime = DateTimeOffset.UtcNow;

        syncHistory.Add(100, baseTime);
        ulong target = syncHistory.GetSyncTarget(baseTime.AddMinutes(5));

        target.Should().Be(100);
    }

    [Test]
    public void Dispose_AfterDispose_ThrowsObjectDisposedException()
    {
        SyncHistory syncHistory = new(TimeSpan.FromMinutes(5));
        syncHistory.Dispose();

        Action addAct = () => syncHistory.Add(100, DateTimeOffset.UtcNow);
        Action getAct = () => syncHistory.GetSyncTarget(DateTimeOffset.UtcNow);

        addAct.Should().Throw<ObjectDisposedException>();
        getAct.Should().Throw<ObjectDisposedException>();
    }

    [Test]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        SyncHistory syncHistory = new(TimeSpan.FromMinutes(5));

        Action act = () =>
        {
            syncHistory.Dispose();
            syncHistory.Dispose();
        };

        act.Should().NotThrow();
    }

    [Test]
    public void Add_WithMultipleEntriesAtSameTime_KeepsAllEntries()
    {
        using SyncHistory syncHistory = new(TimeSpan.FromMinutes(5));
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        syncHistory.Add(100, timestamp);
        syncHistory.Add(200, timestamp);
        syncHistory.Add(300, timestamp);

        ulong target = syncHistory.GetSyncTarget(timestamp);

        target.Should().Be(100);
    }

    [Test]
    public void GetSyncTarget_WithEntriesJustBeforeWindow_ReturnsZero()
    {
        using SyncHistory syncHistory = new(TimeSpan.FromMinutes(5));
        DateTimeOffset baseTime = DateTimeOffset.UtcNow;

        syncHistory.Add(100, baseTime);
        ulong target = syncHistory.GetSyncTarget(baseTime.AddMinutes(5).AddTicks(1));

        target.Should().Be(0);
    }
}
