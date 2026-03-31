// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Config;

namespace Nethermind.Arbitrum.Sequencer.Timeboost;

public interface IRoundTimingInfo
{
    TimeProvider TimeProvider { get; }

    ulong RoundNumber();

    TimeSpan TimeTilNextRound();

    bool IsWithinAuctionCloseWindow(DateTime now);
}

public sealed class RoundTimingInfo(IArbitrumConfig config, DateTime offset, TimeProvider timeProvider) : IRoundTimingInfo
{
    private readonly TimeSpan _round = TimeSpan.FromSeconds(config.TimeboostRoundDurationSeconds);
    private readonly TimeSpan _auctionClosing = TimeSpan.FromSeconds(config.TimeboostAuctionClosingWindowSeconds);

    public TimeProvider TimeProvider { get; } = timeProvider;

    public ulong RoundNumber() => RoundNumberAt(TimeProvider.GetUtcNow().UtcDateTime);

    public TimeSpan TimeTilNextRound() => TimeTilNextRoundAt(TimeProvider.GetUtcNow().UtcDateTime);

    private ulong RoundNumberAt(DateTime now)
    {
        TimeSpan elapsed = now - offset;
        return elapsed < TimeSpan.Zero ? 0 : (ulong)(elapsed / _round);
    }

    private TimeSpan TimeTilNextRoundAt(DateTime now)
    {
        ulong roundNum = RoundNumberAt(now);
        DateTime nextRoundStart = offset + _round * (long)(roundNum + 1);
        return nextRoundStart - now;
    }

    public bool IsWithinAuctionCloseWindow(DateTime now)
        => TimeTilNextRoundAt(now) <= _auctionClosing;
}
