// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.HealthChecks;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumClHealthTracker(ILogManager logManager) : IClHealthTracker, IAsyncDisposable
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumClHealthTracker>();
    private volatile bool _connected;
    private Timer? _timer;

    public Task StartAsync()
    {
        _timer = new Timer(CheckConnection, null, InitialDelay, CheckInterval);
        return Task.CompletedTask;
    }

    public bool CheckClAlive() => _connected;

    public void MarkConnected()
    {
        if (_connected)
            return;

        _connected = true;
        _timer?.Change(Timeout.Infinite, 0);
    }

    private void CheckConnection(object? _)
    {
        if (_connected)
            return;

        if (_logger.IsInfo)
            _logger.Info("Waiting for connection from consensus layer...");
    }

    public async ValueTask DisposeAsync()
    {
        _timer?.Change(Timeout.Infinite, 0);
        if (_timer is not null)
            await _timer.DisposeAsync();
    }
}
