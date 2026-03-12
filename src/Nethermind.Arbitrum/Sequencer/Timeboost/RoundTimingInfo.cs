// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Config;

namespace Nethermind.Arbitrum.Sequencer.Timeboost;

public sealed class RoundTimingInfo
{
    private readonly DateTime _offset;
    private readonly TimeSpan _round;
    private readonly TimeSpan _auctionClosing;

    public RoundTimingInfo(IArbitrumConfig config) : this(config, DateTime.UnixEpoch) { }

    public RoundTimingInfo(IArbitrumConfig config, DateTime offset)
    {
        _offset = offset;
        _round = TimeSpan.FromSeconds(config.TimeboostRoundDurationSeconds);
        _auctionClosing = TimeSpan.FromSeconds(config.TimeboostAuctionClosingWindowSeconds);
    }

    public ulong RoundNumber() => RoundNumberAt(DateTime.UtcNow);

    public ulong RoundNumberAt(DateTime t)
    {
        TimeSpan elapsed = t - _offset;
        if (elapsed < TimeSpan.Zero)
            return 0;
        return (ulong)(elapsed / _round);
    }

    public TimeSpan TimeTilNextRound() => TimeTilNextRoundAt(DateTime.UtcNow);

    public TimeSpan TimeTilNextRoundAt(DateTime t)
    {
        ulong roundNum = RoundNumberAt(t);
        DateTime nextRoundStart = _offset + _round * (long)(roundNum + 1);
        return nextRoundStart - t;
    }

    public bool IsWithinAuctionCloseWindow(DateTime t)
        => TimeTilNextRoundAt(t) <= _auctionClosing;
}
