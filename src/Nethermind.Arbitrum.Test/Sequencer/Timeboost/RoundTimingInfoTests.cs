// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Sequencer.Timeboost;

namespace Nethermind.Arbitrum.Test.Sequencer.Timeboost;

[TestFixture]
public class RoundTimingInfoTests
{
    private static readonly DateTime Epoch = DateTime.UnixEpoch;
    private static readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan FifteenSeconds = TimeSpan.FromSeconds(15);

    [Test]
    public void RoundNumberAt_TimeBeforeOffset_ReturnsZero()
    {
        RoundTimingInfo timing = new(Epoch, OneMinute, FifteenSeconds);

        timing.RoundNumberAt(Epoch - TimeSpan.FromSeconds(1)).Should().Be(0);
    }

    [Test]
    public void RoundNumberAt_TimeAtOffset_ReturnsZero()
    {
        RoundTimingInfo timing = new(Epoch, OneMinute, FifteenSeconds);

        timing.RoundNumberAt(Epoch).Should().Be(0);
    }

    [Test]
    public void RoundNumberAt_TimeAtStartOfSecondRound_ReturnsOne()
    {
        RoundTimingInfo timing = new(Epoch, OneMinute, FifteenSeconds);

        timing.RoundNumberAt(Epoch + OneMinute).Should().Be(1);
    }

    [Test]
    public void RoundNumberAt_TimeOneSecondBeforeSecondRound_ReturnsZero()
    {
        RoundTimingInfo timing = new(Epoch, OneMinute, FifteenSeconds);

        timing.RoundNumberAt(Epoch + TimeSpan.FromSeconds(59)).Should().Be(0);
    }

    [Test]
    public void RoundNumberAt_ManyRoundsElapsed_ReturnsCorrectRound()
    {
        RoundTimingInfo timing = new(Epoch, OneMinute, FifteenSeconds);

        timing.RoundNumberAt(Epoch + OneMinute * 100).Should().Be(100);
    }

    [Test]
    public void TimeTilNextRoundAt_AtRoundStart_ReturnsFullRoundDuration()
    {
        RoundTimingInfo timing = new(Epoch, OneMinute, FifteenSeconds);

        timing.TimeTilNextRoundAt(Epoch).Should().Be(OneMinute);
    }

    [Test]
    public void TimeTilNextRoundAt_HalfwayThroughRound_ReturnsHalfRoundDuration()
    {
        RoundTimingInfo timing = new(Epoch, OneMinute, FifteenSeconds);

        timing.TimeTilNextRoundAt(Epoch + TimeSpan.FromSeconds(30)).Should().Be(TimeSpan.FromSeconds(30));
    }

    [Test]
    public void IsWithinAuctionCloseWindow_EarlyInRound_ReturnsFalse()
    {
        RoundTimingInfo timing = new(Epoch, OneMinute, FifteenSeconds);

        // 5s elapsed, 55s remain — well outside the 15s window
        timing.IsWithinAuctionCloseWindow(Epoch + TimeSpan.FromSeconds(5)).Should().BeFalse();
    }

    [Test]
    public void IsWithinAuctionCloseWindow_ExactlyAtWindowBoundary_ReturnsTrue()
    {
        RoundTimingInfo timing = new(Epoch, OneMinute, FifteenSeconds);

        // 45s elapsed, exactly 15s remain — on the boundary
        timing.IsWithinAuctionCloseWindow(Epoch + TimeSpan.FromSeconds(45)).Should().BeTrue();
    }

    [Test]
    public void IsWithinAuctionCloseWindow_InsideWindow_ReturnsTrue()
    {
        RoundTimingInfo timing = new(Epoch, OneMinute, FifteenSeconds);

        // 50s elapsed, 10s remain — inside window
        timing.IsWithinAuctionCloseWindow(Epoch + TimeSpan.FromSeconds(50)).Should().BeTrue();
    }

    [Test]
    public void IsWithinAuctionCloseWindow_OneSecondBeforeWindow_ReturnsFalse()
    {
        RoundTimingInfo timing = new(Epoch, OneMinute, FifteenSeconds);

        // 44s elapsed, 16s remain — just outside window
        timing.IsWithinAuctionCloseWindow(Epoch + TimeSpan.FromSeconds(44)).Should().BeFalse();
    }
}
