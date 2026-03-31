// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Sequencer.Timeboost;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public sealed class ManualRoundTimingInfo : IRoundTimingInfo
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
