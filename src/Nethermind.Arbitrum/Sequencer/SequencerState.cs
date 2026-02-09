// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

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
    private readonly Lock _lock = new();
    private SequencerMode _mode = SequencerMode.Inactive;
    private TransactionForwarder? _forwarder;

    public bool IsActive
    {
        get { lock (_lock) return _mode == SequencerMode.Active; }
    }

    public SequencerMode Mode
    {
        get { lock (_lock) return _mode; }
    }

    public TransactionForwarder? Forwarder
    {
        get { lock (_lock) return _forwarder; }
    }

    public void Activate()
    {
        lock (_lock)
        {
            _forwarder?.Disable();
            _forwarder = null;
            _mode = SequencerMode.Active;
        }
    }

    public void Pause()
    {
        lock (_lock)
        {
            _forwarder?.Disable();
            _forwarder = null;
            _mode = SequencerMode.Paused;
        }
    }

    public void ForwardTo(string url)
    {
        lock (_lock)
        {
            if (_forwarder is not null)
            {
                if (_forwarder.PrimaryTarget == url)
                {
                    if (_logger.IsWarn)
                        _logger.Warn($"Attempted to update sequencer forward target with existing target: {url}");
                    return;
                }

                _forwarder.Disable();
            }

            _forwarder = new TransactionForwarder(url, logManager);
            _mode = SequencerMode.Forwarding;
        }
    }
}
