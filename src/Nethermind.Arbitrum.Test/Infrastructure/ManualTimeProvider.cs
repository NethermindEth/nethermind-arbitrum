// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

namespace Nethermind.Arbitrum.Test.Infrastructure;

internal sealed class ManualTimeProvider(DateTimeOffset startTime) : TimeProvider
{
    // The polling loop's Task.Delay continuations may run on a threadpool thread
    // (async-scheduled by the runtime), concurrently with the test thread calling Advance.
    // Both paths mutate _timers, so all accesses are synchronized.
    private readonly Lock _lock = new();
    private readonly List<FakeTimer> _timers = [];
    private DateTimeOffset _utcNow = startTime;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
            return _utcNow;
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (_lock)
        {
            FakeTimer timer = new(this, callback, state, dueTime);
            if (dueTime != Timeout.InfiniteTimeSpan)
                _timers.Add(timer);

            return timer;
        }
    }

    public void Advance(TimeSpan delta)
    {
        List<FakeTimer> dueTimers = [];
        lock (_lock)
        {
            _utcNow += delta;

            for (int i = _timers.Count - 1; i >= 0; i--)
            {
                FakeTimer timer = _timers[i];
                if (timer.Disposed || timer.DueAt > _utcNow)
                    continue;

                _timers.RemoveAt(i);
                dueTimers.Add(timer);
            }
        }

        foreach (FakeTimer timer in dueTimers)
            timer.Fire();
    }

    private sealed class FakeTimer(ManualTimeProvider provider, TimerCallback callback, object? state, TimeSpan dueTime)
        : ITimer
    {
        public DateTimeOffset DueAt { get; private set; } =
            dueTime == Timeout.InfiniteTimeSpan
                ? DateTimeOffset.MaxValue
                : provider._utcNow + dueTime;

        public bool Disposed { get; private set; }

        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (provider._lock)
            {
                if (Disposed)
                    return false;

                if (dueTime == Timeout.InfiniteTimeSpan)
                {
                    DueAt = DateTimeOffset.MaxValue;
                    provider._timers.Remove(this);
                }
                else
                {
                    DueAt = provider._utcNow + dueTime;
                    if (!provider._timers.Contains(this))
                        provider._timers.Add(this);
                }
            }

            return true;
        }

        public void Dispose()
        {
            lock (provider._lock)
                Disposed = true;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
