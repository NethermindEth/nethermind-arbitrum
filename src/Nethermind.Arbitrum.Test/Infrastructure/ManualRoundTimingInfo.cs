// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Sequencer.Timeboost;

namespace Nethermind.Arbitrum.Test.Infrastructure;

internal sealed class ManualRoundTimingInfo : IRoundTimingInfo
{
    private readonly RoundTimingInfo _inner;
    private readonly ManualTimeProvider _manualTimeProvider;

    public ManualRoundTimingInfo(IArbitrumConfig config, DateTimeOffset startTime, ulong currentRound, TimeSpan intoRound)
    {
        _manualTimeProvider = new ManualTimeProvider(startTime);

        TimeSpan roundDuration = TimeSpan.FromSeconds(config.TimeboostRoundDurationSeconds);
        DateTime offset = startTime.UtcDateTime - roundDuration * (long)currentRound - intoRound;

        _inner = new RoundTimingInfo(config, offset, _manualTimeProvider);
    }

    public TimeProvider TimeProvider => _inner.TimeProvider;

    public ulong RoundNumber() => _inner.RoundNumber();

    public TimeSpan TimeTilNextRound() => _inner.TimeTilNextRound();

    public bool IsWithinAuctionCloseWindow(DateTime now) => _inner.IsWithinAuctionCloseWindow(now);

    public void Advance(TimeSpan delta) => _manualTimeProvider.Advance(delta);
}

internal sealed class ManualTimeProvider(DateTimeOffset startTime) : TimeProvider
{
    private readonly List<FakeTimer> _timers = [];
    private DateTimeOffset _utcNow = startTime;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        FakeTimer timer = new(this, callback, state, dueTime);
        if (dueTime != Timeout.InfiniteTimeSpan)
            _timers.Add(timer);
        return timer;
    }

    public void Advance(TimeSpan delta)
    {
        _utcNow += delta;

        for (int i = _timers.Count - 1; i >= 0; i--)
        {
            FakeTimer timer = _timers[i];
            if (timer.Disposed || timer.DueAt > _utcNow)
                continue;

            _timers.RemoveAt(i);
            timer.Fire();
        }
    }

    private sealed class FakeTimer(ManualTimeProvider provider, TimerCallback callback, object? state, TimeSpan dueTime) : ITimer
    {
        public DateTimeOffset DueAt { get; private set; } =
            dueTime == Timeout.InfiniteTimeSpan
            ? DateTimeOffset.MaxValue
            : provider._utcNow + dueTime;

        public bool Disposed { get; private set; }

        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period)
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

            return true;
        }

        public void Dispose() => Disposed = true;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
