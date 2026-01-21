// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Execution;

namespace Nethermind.Arbitrum.Test.Execution;

[TestFixture]
public sealed class ProcessingTimeTrackerTests
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromHours(1);

    [Test]
    public void TimeBeforeFlush_Initial_IsOneHour()
    {
        ProcessingTimeTracker tracker = new();

        tracker.TimeBeforeFlush.Should().Be(FlushInterval);
    }

    [Test]
    public void TimeBeforeFlush_WithZeroOffsetRange_IsExactlyFlushInterval()
    {
        ProcessingTimeTracker tracker = new(randomOffsetRangeMs: 0);

        tracker.TimeBeforeFlush.Should().Be(FlushInterval);
    }

    [Test]
    public void TimeBeforeFlush_WithOffsetRange_IsWithinExpectedBounds()
    {
        ProcessingTimeTracker tracker = new(randomOffsetRangeMs: 1000);

        tracker.TimeBeforeFlush.Should().BeLessThanOrEqualTo(FlushInterval);
        tracker.TimeBeforeFlush.Should().BeGreaterThanOrEqualTo(FlushInterval - TimeSpan.FromMilliseconds(1000));
    }

    [Test]
    public void AddProcessingTime_SingleAddition_DecreasesTimeBeforeFlush()
    {
        ProcessingTimeTracker tracker = new();

        tracker.AddProcessingTime(TimeSpan.FromMilliseconds(100));

        tracker.TimeBeforeFlush.Should().Be(FlushInterval - TimeSpan.FromMilliseconds(100));
    }

    [Test]
    public void AddProcessingTime_MultipleAdditions_AccumulatesCorrectly()
    {
        ProcessingTimeTracker tracker = new();

        tracker.AddProcessingTime(TimeSpan.FromMilliseconds(100));
        tracker.AddProcessingTime(TimeSpan.FromMilliseconds(200));
        tracker.AddProcessingTime(TimeSpan.FromMilliseconds(50));

        tracker.TimeBeforeFlush.Should().Be(FlushInterval - TimeSpan.FromMilliseconds(350));
    }

    [Test]
    public void AddProcessingTime_ZeroTime_DoesNotChangeTimeBeforeFlush()
    {
        ProcessingTimeTracker tracker = new();
        TimeSpan initial = tracker.TimeBeforeFlush;

        tracker.AddProcessingTime(TimeSpan.Zero);

        tracker.TimeBeforeFlush.Should().Be(initial);
    }

    [Test]
    public void AddProcessingTime_LargeAccumulation_CanExceedFlushInterval()
    {
        ProcessingTimeTracker tracker = new();

        tracker.AddProcessingTime(TimeSpan.FromHours(2));

        tracker.TimeBeforeFlush.Should().Be(TimeSpan.FromHours(-1));
    }

    [Test]
    public void Reset_AfterAccumulation_RestoresTimeBeforeFlush()
    {
        ProcessingTimeTracker tracker = new();
        tracker.AddProcessingTime(TimeSpan.FromMinutes(30));

        tracker.Reset();

        tracker.TimeBeforeFlush.Should().Be(FlushInterval);
    }

    [Test]
    public void Reset_WithOffsetRange_RegeneratesRandomOffset()
    {
        ProcessingTimeTracker tracker = new(randomOffsetRangeMs: 10000);
        TimeSpan initialTimeBeforeFlush = tracker.TimeBeforeFlush;

        bool changed = false;
        for (int i = 0; i < 100; i++)
        {
            tracker.Reset();
            if (tracker.TimeBeforeFlush != initialTimeBeforeFlush)
            {
                changed = true;
                break;
            }
        }

        changed.Should().BeTrue("random offset should be regenerated on reset");
    }

    [Test]
    public void Reset_MultipleResets_MaintainsConsistentBehavior()
    {
        ProcessingTimeTracker tracker = new();

        for (int i = 0; i < 5; i++)
        {
            tracker.AddProcessingTime(TimeSpan.FromMinutes(10));
            tracker.Reset();
            tracker.TimeBeforeFlush.Should().Be(FlushInterval);
        }
    }

    [Test]
    public void AddProcessingTime_ConcurrentAccess_IsThreadSafe()
    {
        ProcessingTimeTracker tracker = new();
        int iterations = 1000;
        TimeSpan increment = TimeSpan.FromMilliseconds(1);

        Parallel.For(0, iterations, _ => tracker.AddProcessingTime(increment));

        tracker.TimeBeforeFlush.Should().Be(FlushInterval - TimeSpan.FromMilliseconds(iterations));
    }

    [Test]
    public void TimeBeforeFlush_ConcurrentReads_IsThreadSafe()
    {
        ProcessingTimeTracker tracker = new();
        tracker.AddProcessingTime(TimeSpan.FromMinutes(10));
        TimeSpan expected = FlushInterval - TimeSpan.FromMinutes(10);

        TimeSpan[] results = new TimeSpan[100];
        Parallel.For(0, 100, i => results[i] = tracker.TimeBeforeFlush);

        results.Should().AllBeEquivalentTo(expected);
    }

    [Test]
    public void TimeBeforeFlush_CustomFlushInterval_UsesConfiguredValue()
    {
        TimeSpan customInterval = TimeSpan.FromMinutes(5);
        ProcessingTimeTracker tracker = new(flushIntervalMs: 300000, randomOffsetRangeMs: 0);

        tracker.TimeBeforeFlush.Should().Be(customInterval);
    }

    [Test]
    public void AddProcessingTime_CustomFlushInterval_AccumulatesCorrectly()
    {
        TimeSpan customInterval = TimeSpan.FromMinutes(5);
        ProcessingTimeTracker tracker = new(flushIntervalMs: 300000, randomOffsetRangeMs: 0);

        tracker.AddProcessingTime(TimeSpan.FromMinutes(3));

        tracker.TimeBeforeFlush.Should().Be(customInterval - TimeSpan.FromMinutes(3));
    }

    [Test]
    public void Reset_CustomFlushInterval_RestoresToConfiguredValue()
    {
        TimeSpan customInterval = TimeSpan.FromMinutes(5);
        ProcessingTimeTracker tracker = new(flushIntervalMs: 300000, randomOffsetRangeMs: 0);
        tracker.AddProcessingTime(TimeSpan.FromMinutes(3));

        tracker.Reset();

        tracker.TimeBeforeFlush.Should().Be(customInterval);
    }
}
