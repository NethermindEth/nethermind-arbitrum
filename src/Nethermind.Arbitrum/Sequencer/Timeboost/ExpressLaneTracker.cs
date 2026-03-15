// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Config;
using Nethermind.Core;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Sequencer.Timeboost;

public sealed class ExpressLaneTracker(
    IRoundTimingInfo roundTimingInfo,
    IAuctionContract auctionContract,
    IArbitrumConfig arbitrumConfig,
    ILogManager logManager) : IExpressLaneTracker, IDisposable
{
    private readonly ILogger _logger = logManager.GetClassLogger<ExpressLaneTracker>();
    private readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(arbitrumConfig.TimeboostAuctionContractPollIntervalMs);

    private readonly Lock _roundLock = new();
    private readonly Dictionary<ulong, Address> _roundControllers = new();

    private CancellationTokenSource? _cts;
    private Task? _pollingTask;

    public event EventHandler<RoundControllerResolvedEventArgs>? ControllerResolved;

    public Address AuctionContractAddress => auctionContract.Address;

    public Task Start(CancellationToken ct)
    {
        if (_pollingTask is not null)
            return Task.CompletedTask;

        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _pollingTask = Task.Run(async () =>
        {
            started.TrySetResult();
            await PollContractLoopAsync(_cts.Token);
        });

        return started.Task;
    }

    public Address? GetController(ulong round)
    {
        lock (_roundLock)
            return _roundControllers.TryGetValue(round, out Address? controller) ? controller : null;
    }

    public bool CurrentRoundHasController()
    {
        ulong round = roundTimingInfo.RoundNumber();
        lock (_roundLock)
            return _roundControllers.ContainsKey(round);
    }

    public bool IsWithinAuctionCloseWindow(DateTime t)
        => roundTimingInfo.IsWithinAuctionCloseWindow(t);

    public void Dispose()
    {
        _cts?.Cancel();
        if (_pollingTask is not null)
            _pollingTask.GetAwaiter().GetResult();
        _cts?.Dispose();
    }

    private async Task PollContractLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_pollInterval, roundTimingInfo.TimeProvider, ct);
                PollResolvedRounds();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (_logger.IsDebug)
                    _logger.Debug($"ExpressLaneTracker: poll failed: {ex.Message}");
            }
        }
    }

    private void PollResolvedRounds()
    {
        ResolvedRound resolved = auctionContract.ResolveRounds();

        if (resolved.Controller == Address.Zero || resolved.Round == 0)
            return;

        ulong currentRound = roundTimingInfo.RoundNumber();
        bool isNewDiscovery;
        lock (_roundLock)
        {
            isNewDiscovery = !_roundControllers.ContainsKey(resolved.Round);
            _roundControllers[resolved.Round] = resolved.Controller;

            // Clean up rounds older than 2 behind current
            if (currentRound > 2)
            {
                ulong oldest = currentRound - 2;
                List<ulong>? toRemove = null;
                foreach (ulong key in _roundControllers.Keys)
                {
                    if (key < oldest)
                        (toRemove ??= new()).Add(key);
                }
                if (toRemove is not null)
                {
                    foreach (ulong key in toRemove)
                        _roundControllers.Remove(key);
                }
            }
        }

        if (isNewDiscovery)
            ControllerResolved?.Invoke(this, new RoundControllerResolvedEventArgs { Round = resolved.Round, Controller = resolved.Controller });

        if (_logger.IsDebug)
            _logger.Debug($"ExpressLaneTracker: round {resolved.Round} controller = {resolved.Controller}");
    }
}
