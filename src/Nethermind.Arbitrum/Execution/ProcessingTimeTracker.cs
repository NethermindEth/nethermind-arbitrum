// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Config;

namespace Nethermind.Arbitrum.Execution;

/// <summary>
/// Tracks accumulated block processing time for maintenance scheduling.
/// </summary>
public interface IProcessingTimeTracker
{
    /// <summary>
    /// Time remaining before flush: FlushInterval - (AccumulatedTime + RandomOffset)
    /// </summary>
    TimeSpan TimeBeforeFlush { get; }

    /// <summary>
    /// Add processing time (called from NewProcessingStatistics event).
    /// </summary>
    void AddProcessingTime(TimeSpan elapsed);

    /// <summary>
    /// Reset accumulated time and regenerate random offset after a manual flush.
    /// </summary>
    void Reset();
}

public class ProcessingTimeTracker : IProcessingTimeTracker
{
    private readonly long _randomOffsetRangeMs;
    private readonly Lock _lock = new();

    private TimeSpan _accumulatedTime = TimeSpan.Zero;
    private TimeSpan _randomOffset;

    public ProcessingTimeTracker(IArbitrumConfig config)
    {
        TimeBeforeFlush = TimeSpan.FromMilliseconds(config.TrieTimeLimitMs);
        _randomOffsetRangeMs = config.TrieTimeLimitRandomOffsetMs;
        _randomOffset = GenerateRandomOffset();
    }

    public TimeSpan TimeBeforeFlush
    {
        get
        {
            lock (_lock)
                return field - (_accumulatedTime + _randomOffset);
        }
    }

    public void AddProcessingTime(TimeSpan elapsed)
    {
        lock (_lock)
            _accumulatedTime += elapsed;
    }

    public void Reset()
    {
        lock (_lock)
        {
            _accumulatedTime = TimeSpan.Zero;
            _randomOffset = GenerateRandomOffset();
        }
    }

    private TimeSpan GenerateRandomOffset()
    {
        return _randomOffsetRangeMs <= 0 ?
            TimeSpan.Zero :
            TimeSpan.FromMilliseconds(Random.Shared.NextInt64(0, _randomOffsetRangeMs));
    }
}
