// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Buffers.Binary;
using Nethermind.Arbitrum.Config;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Facade;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Sequencer.Timeboost;

public sealed class ExpressLaneTracker(
    IRoundTimingInfo roundTimingInfo,
    IBlockTree blockTree,
    IBlockchainBridgeFactory bridgeFactory,
    IArbitrumConfig arbitrumConfig,
    ILogManager logManager,
    TimeSpan? pollInterval = null) : IExpressLaneTracker, IDisposable
{
    // resolvedRounds() function selector = keccak256("resolvedRounds()")[:4] = 0x0d253fbe
    private static readonly byte[] ResolvedRoundsSelector = [0x0d, 0x25, 0x3f, 0xbe];

    private readonly Address _auctionContractAddress = new(arbitrumConfig.TimeboostAuctionContractAddress);
    private readonly IBlockchainBridge _bridge = bridgeFactory.CreateBlockchainBridge();
    private readonly TimeSpan _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
    private readonly ILogger _logger = logManager.GetClassLogger<ExpressLaneTracker>();

    private readonly Lock _roundLock = new();
    private readonly Dictionary<ulong, Address> _roundControllers = new();

    private CancellationTokenSource? _cts;
    private Task? _pollingTask;

    public event EventHandler<RoundControllerResolvedEventArgs>? ControllerResolved;

    public Address AuctionContractAddress => _auctionContractAddress;

    public void Start(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _pollingTask = Task.Run(() => PollContractLoopAsync(_cts.Token));
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

    internal void ForceSetController(ulong round, Address controller)
    {
        lock (_roundLock)
            _roundControllers[round] = controller;
    }

    internal bool HasControllerForRound(ulong round)
    {
        lock (_roundLock)
            return _roundControllers.ContainsKey(round);
    }

    internal void TriggerPoll() => PollResolvedRounds();

    private async Task PollContractLoopAsync(CancellationToken ct)
    {
        using PeriodicTimer timer = new(_pollInterval);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(ct);
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
        BlockHeader? head = blockTree.Head?.Header;
        if (head is null)
            return;

        Transaction callTx = new()
        {
            To = _auctionContractAddress,
            Data = ResolvedRoundsSelector,
            GasLimit = 200_000,
            SenderAddress = Address.Zero,
        };

        CallOutput result = _bridge.Call(head, callTx);
        if (result.Error is not null || result.ExecutionReverted)
            return;

        byte[] output = result.OutputData;
        if (output.Length < 128)
            return;

        Address controller = new(output.AsSpan(12, 20));
        ulong round = BinaryPrimitives.ReadUInt64BigEndian(output.AsSpan(56, 8));

        if (controller == Address.Zero || round == 0)
            return;

        ulong currentRound = roundTimingInfo.RoundNumber();
        bool isNewDiscovery;
        lock (_roundLock)
        {
            isNewDiscovery = !_roundControllers.ContainsKey(round);
            _roundControllers[round] = controller;

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
            ControllerResolved?.Invoke(this, new RoundControllerResolvedEventArgs { Round = round, Controller = controller });

        if (_logger.IsDebug)
            _logger.Debug($"ExpressLaneTracker: round {round} controller = {controller}");
    }
}
