// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

namespace Nethermind.Arbitrum.Sequencer.Timeboost;

public sealed class RoundTimingInfo(
    DateTime offset,
    TimeSpan round,
    TimeSpan auctionClosing)
{
    public ulong RoundNumber() => RoundNumberAt(DateTime.UtcNow);

    public ulong RoundNumberAt(DateTime t)
    {
        TimeSpan elapsed = t - offset;
        if (elapsed < TimeSpan.Zero)
            return 0;
        return (ulong)(elapsed / round);
    }

    public TimeSpan TimeTilNextRound() => TimeTilNextRoundAt(DateTime.UtcNow);

    public TimeSpan TimeTilNextRoundAt(DateTime t)
    {
        ulong roundNum = RoundNumberAt(t);
        DateTime nextRoundStart = offset + round * (long)(roundNum + 1);
        return nextRoundStart - t;
    }

    public bool IsWithinAuctionCloseWindow(DateTime t)
        => TimeTilNextRoundAt(t) <= auctionClosing;
}
