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
    public void SequencerState_Initially_IsInactive()
    {
        SequencerState state = new(LimboLogs.Instance);

        state.Mode.Should().Be(SequencerMode.Inactive);
    }

    [Test]
    public void SequencerState_AfterActivate_IsActive()
    {
        SequencerState state = new(LimboLogs.Instance);

        state.Activate();

        state.Mode.Should().Be(SequencerMode.Active);
    }

    [Test]
    public void SequencerState_AfterActivate_HasNoForwarder()
    {
        SequencerState state = new(LimboLogs.Instance);

        state.Activate();

        state.Forwarder.Should().BeNull();
    }

    [Test]
    public void SequencerState_AfterPause_IsPaused()
    {
        SequencerState state = new(LimboLogs.Instance);
        state.Activate();

        state.Pause();

        state.Mode.Should().Be(SequencerMode.Paused);
    }

    [Test]
    public void SequencerState_AfterPause_HasNoForwarder()
    {
        SequencerState state = new(LimboLogs.Instance);
        state.ForwardTo("https://sequencer.example.com");

        state.Pause();

        state.Forwarder.Should().BeNull();
    }

    [Test]
    public void SequencerState_AfterForwardTo_IsForwarding()
    {
        SequencerState state = new(LimboLogs.Instance);

        state.ForwardTo("https://sequencer.example.com");

        state.Mode.Should().Be(SequencerMode.Forwarding);
    }

    [Test]
    public void SequencerState_AfterForwardTo_HasForwarderWithTarget()
    {
        SequencerState state = new(LimboLogs.Instance);

        state.ForwardTo("https://sequencer.example.com");

        state.Forwarder.Should().NotBeNull();
        state.Forwarder!.PrimaryTarget.Should().Be("https://sequencer.example.com");
    }

    [Test]
    public void SequencerState_AfterActivateFromForwarding_ClearsForwarder()
    {
        SequencerState state = new(LimboLogs.Instance);
        state.ForwardTo("https://sequencer.example.com");

        state.Activate();

        state.Forwarder.Should().BeNull();
    }

    [Test]
    public void IsActive_WhenActive_ReturnsTrue()
    {
        SequencerState state = new(LimboLogs.Instance);

        state.Activate();

        state.IsActive.Should().BeTrue();
    }

    [Test]
    public void IsActive_WhenInactive_ReturnsFalse()
    {
        SequencerState state = new(LimboLogs.Instance);

        state.IsActive.Should().BeFalse();
    }

    [Test]
    public void IsActive_WhenPaused_ReturnsFalse()
    {
        SequencerState state = new(LimboLogs.Instance);
        state.Activate();

        state.Pause();

        state.IsActive.Should().BeFalse();
    }

    [Test]
    public void IsActive_WhenForwarding_ReturnsFalse()
    {
        SequencerState state = new(LimboLogs.Instance);

        state.ForwardTo("https://sequencer.example.com");

        state.IsActive.Should().BeFalse();
    }

    [Test]
    public void Snapshot_AfterMutation_RetainsOriginalMode()
    {
        SequencerState state = new(LimboLogs.Instance);
        state.ForwardTo("https://sequencer.example.com");
        SequencerStateSnapshot snapshot = state.Current;

        state.Activate();

        snapshot.Mode.Should().Be(SequencerMode.Forwarding);
    }

    [Test]
    public void Snapshot_AfterMutation_RetainsOriginalForwarder()
    {
        SequencerState state = new(LimboLogs.Instance);
        state.ForwardTo("https://sequencer.example.com");
        SequencerStateSnapshot snapshot = state.Current;

        state.Activate();

        snapshot.Forwarder.Should().NotBeNull();
        snapshot.Forwarder!.PrimaryTarget.Should().Be("https://sequencer.example.com");
    }
}
