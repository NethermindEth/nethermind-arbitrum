// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Config;

namespace Nethermind.Arbitrum.Sequencer.Timeboost;

public interface IRoundTimingInfo
{
    TimeProvider TimeProvider { get; }

    ulong RoundNumber();

    ulong RoundNumberAt(DateTime now);

    TimeSpan TimeTilNextRound();

    TimeSpan TimeTilNextRoundAt(DateTime now);

    bool IsWithinAuctionCloseWindow(DateTime now);
}

public sealed class RoundTimingInfo : IRoundTimingInfo
{
    private readonly DateTime _offset;
    private readonly TimeSpan _round;
    private readonly TimeSpan _auctionClosing;

    public TimeProvider TimeProvider { get; }

    // public RoundTimingInfo(IArbitrumConfig config) : this(config, DateTime.UnixEpoch) { }

    public RoundTimingInfo(IArbitrumConfig config, DateTime offset) : this(config, offset, TimeProvider.System) { }

    public RoundTimingInfo(IArbitrumConfig config, DateTime offset, TimeProvider timeProvider)
    {
        _offset = offset;
        _round = TimeSpan.FromSeconds(config.TimeboostRoundDurationSeconds);
        _auctionClosing = TimeSpan.FromSeconds(config.TimeboostAuctionClosingWindowSeconds);
        TimeProvider = timeProvider;
    }

    public ulong RoundNumber() => RoundNumberAt(TimeProvider.GetUtcNow().UtcDateTime);

    public ulong RoundNumberAt(DateTime now)
    {
        TimeSpan elapsed = now - _offset;
        if (elapsed < TimeSpan.Zero)
            return 0;
        return (ulong)(elapsed / _round);
    }

    public TimeSpan TimeTilNextRound() => TimeTilNextRoundAt(TimeProvider.GetUtcNow().UtcDateTime);

    public TimeSpan TimeTilNextRoundAt(DateTime now)
    {
        ulong roundNum = RoundNumberAt(now);
        DateTime nextRoundStart = _offset + _round * (long)(roundNum + 1);
        return nextRoundStart - now;
    }

    public bool IsWithinAuctionCloseWindow(DateTime now)
        => TimeTilNextRoundAt(now) <= _auctionClosing;
}
