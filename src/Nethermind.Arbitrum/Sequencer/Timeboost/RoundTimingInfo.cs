// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Arbitrum.Sequencer.Timeboost;

public sealed class RoundTimingInfo(
    DateTime offset,
    TimeSpan round,
    TimeSpan auctionClosing)
{
    public DateTime Offset { get; } = offset;
    public TimeSpan Round { get; } = round;
    public TimeSpan AuctionClosing { get; } = auctionClosing;

    public ulong RoundNumber() => RoundNumberAt(DateTime.UtcNow);

    public ulong RoundNumberAt(DateTime t)
    {
        TimeSpan elapsed = t - Offset;
        if (elapsed < TimeSpan.Zero)
            return 0;
        return (ulong)(elapsed / Round);
    }

    public TimeSpan TimeTilNextRound() => TimeTilNextRoundAt(DateTime.UtcNow);

    public TimeSpan TimeTilNextRoundAt(DateTime t)
    {
        ulong roundNum = RoundNumberAt(t);
        DateTime nextRoundStart = Offset + Round * (long)(roundNum + 1);
        return nextRoundStart - t;
    }

    public bool IsWithinAuctionCloseWindow(DateTime t)
        => TimeTilNextRoundAt(t) <= AuctionClosing;
}
