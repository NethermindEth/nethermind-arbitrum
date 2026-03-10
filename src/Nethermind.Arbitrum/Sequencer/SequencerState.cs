// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Logging;

namespace Nethermind.Arbitrum.Sequencer;

public enum SequencerMode
{
    Inactive,
    Active,
    Paused,
    Forwarding
}

public class SequencerState(ILogManager logManager)
{
    private readonly ILogger _logger = logManager.GetClassLogger<SequencerState>();
    private volatile SequencerStateSnapshot _state = new(SequencerMode.Inactive, null);

    public SequencerStateSnapshot Current => _state;

    public bool IsActive => _state.Mode == SequencerMode.Active;

    public SequencerMode Mode => _state.Mode;

    public TransactionForwarder? Forwarder => _state.Forwarder;

    public void Activate()
    {
        _state.Forwarder?.Disable();
        _state = new SequencerStateSnapshot(SequencerMode.Active, null);
    }

    public void Pause()
    {
        _state.Forwarder?.Disable();
        _state = new SequencerStateSnapshot(SequencerMode.Paused, null);
    }

    public void ForwardTo(string url)
    {
        SequencerStateSnapshot current = _state;

        if (current.Forwarder is not null)
        {
            if (current.Forwarder.PrimaryTarget == url)
            {
                if (_logger.IsWarn)
                    _logger.Warn($"Attempted to update sequencer forward target with existing target: {url}");
                return;
            }

            current.Forwarder.Disable();
        }

        _state = new SequencerStateSnapshot(SequencerMode.Forwarding, new TransactionForwarder(url, logManager));
    }
}

public record SequencerStateSnapshot(SequencerMode Mode, TransactionForwarder? Forwarder);
