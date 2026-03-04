// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Test.Sequencer;

[TestFixture]
public class SequencerStateTests
{
    [Test]
    public void SequencerState_Transitions_FollowStateMachine()
    {
        SequencerState state = new(LimboLogs.Instance);

        state.Mode.Should().Be(SequencerMode.Inactive);

        state.Activate();
        state.Mode.Should().Be(SequencerMode.Active);
        state.Forwarder.Should().BeNull();

        state.Pause();
        state.Mode.Should().Be(SequencerMode.Paused);
        state.Forwarder.Should().BeNull();

        state.ForwardTo("https://sequencer.example.com");
        state.Mode.Should().Be(SequencerMode.Forwarding);
        state.Forwarder.Should().NotBeNull();
        state.Forwarder!.PrimaryTarget.Should().Be("https://sequencer.example.com");

        state.Activate();
        state.Mode.Should().Be(SequencerMode.Active);
        state.Forwarder.Should().BeNull();
    }

    [Test]
    public void SequencerState_IsActive_OnlyWhenActive()
    {
        SequencerState state = new(LimboLogs.Instance);

        state.IsActive.Should().BeFalse();

        state.Activate();
        state.IsActive.Should().BeTrue();

        state.Pause();
        state.IsActive.Should().BeFalse();

        state.ForwardTo("https://sequencer.example.com");
        state.IsActive.Should().BeFalse();
    }
}
