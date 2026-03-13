// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Sequencer.Timeboost;
using Nethermind.Arbitrum.Test.Infrastructure;

namespace Nethermind.Arbitrum.Test.Sequencer.Timeboost;

[TestFixture]
public class RoundTimingInfoTests
{
    private static readonly DateTime Epoch = DateTime.UnixEpoch;
    private static readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);
    private static readonly ArbitrumConfig DefaultConfig = new() { TimeboostRoundDurationSeconds = 60, TimeboostAuctionClosingWindowSeconds = 15 };

    [Test]
    public void RoundNumber_TimeBeforeOffset_ReturnsZero()
    {
        ManualTimeProvider time = new(Epoch - TimeSpan.FromSeconds(1));
        RoundTimingInfo timing = new(DefaultConfig, Epoch, time);

        timing.RoundNumber().Should().Be(0);
    }

    [Test]
    public void RoundNumber_TimeAtOffset_ReturnsZero()
    {
        ManualTimeProvider time = new(Epoch);
        RoundTimingInfo timing = new(DefaultConfig, Epoch, time);

        timing.RoundNumber().Should().Be(0);
    }

    [Test]
    public void RoundNumber_TimeAtStartOfSecondRound_ReturnsOne()
    {
        ManualTimeProvider time = new(Epoch + OneMinute);
        RoundTimingInfo timing = new(DefaultConfig, Epoch, time);

        timing.RoundNumber().Should().Be(1);
    }

    [Test]
    public void RoundNumber_TimeOneSecondBeforeSecondRound_ReturnsZero()
    {
        ManualTimeProvider time = new(Epoch + TimeSpan.FromSeconds(59));
        RoundTimingInfo timing = new(DefaultConfig, Epoch, time);

        timing.RoundNumber().Should().Be(0);
    }

    [Test]
    public void RoundNumber_ManyRoundsElapsed_ReturnsCorrectRound()
    {
        ManualTimeProvider time = new(Epoch + OneMinute * 100);
        RoundTimingInfo timing = new(DefaultConfig, Epoch, time);

        timing.RoundNumber().Should().Be(100);
    }

    [Test]
    public void TimeTilNextRound_AtRoundStart_ReturnsFullRoundDuration()
    {
        ManualTimeProvider time = new(Epoch);
        RoundTimingInfo timing = new(DefaultConfig, Epoch, time);

        timing.TimeTilNextRound().Should().Be(OneMinute);
    }

    [Test]
    public void TimeTilNextRound_HalfwayThroughRound_ReturnsHalfRoundDuration()
    {
        ManualTimeProvider time = new(Epoch + TimeSpan.FromSeconds(30));
        RoundTimingInfo timing = new(DefaultConfig, Epoch, time);

        timing.TimeTilNextRound().Should().Be(TimeSpan.FromSeconds(30));
    }

    [Test]
    public void IsWithinAuctionCloseWindow_EarlyInRound_ReturnsFalse()
    {
        RoundTimingInfo timing = new(DefaultConfig, Epoch, new ManualTimeProvider(Epoch));

        timing.IsWithinAuctionCloseWindow(Epoch + TimeSpan.FromSeconds(5)).Should().BeFalse();
    }

    [Test]
    public void IsWithinAuctionCloseWindow_ExactlyAtWindowBoundary_ReturnsTrue()
    {
        RoundTimingInfo timing = new(DefaultConfig, Epoch, new ManualTimeProvider(Epoch));

        timing.IsWithinAuctionCloseWindow(Epoch + TimeSpan.FromSeconds(45)).Should().BeTrue();
    }

    [Test]
    public void IsWithinAuctionCloseWindow_InsideWindow_ReturnsTrue()
    {
        RoundTimingInfo timing = new(DefaultConfig, Epoch, new ManualTimeProvider(Epoch));

        timing.IsWithinAuctionCloseWindow(Epoch + TimeSpan.FromSeconds(50)).Should().BeTrue();
    }

    [Test]
    public void IsWithinAuctionCloseWindow_OneSecondBeforeWindow_ReturnsFalse()
    {
        RoundTimingInfo timing = new(DefaultConfig, Epoch, new ManualTimeProvider(Epoch));

        timing.IsWithinAuctionCloseWindow(Epoch + TimeSpan.FromSeconds(44)).Should().BeFalse();
    }
}
