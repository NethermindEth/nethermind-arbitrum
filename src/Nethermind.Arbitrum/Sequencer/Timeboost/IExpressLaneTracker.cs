// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core;

namespace Nethermind.Arbitrum.Sequencer.Timeboost;

public interface IExpressLaneTracker
{
    Address AuctionContractAddress { get; }

    Address? GetController(ulong round);

    bool CurrentRoundHasController();

    bool IsWithinAuctionCloseWindow(DateTime t);

    event EventHandler<RoundControllerResolvedEventArgs>? ControllerResolved;

    void Start(CancellationToken ct);
}

public sealed class RoundControllerResolvedEventArgs : EventArgs
{
    public required ulong Round { get; init; }
    public required Address Controller { get; init; }
}

public sealed class DisabledExpressLaneTracker : IExpressLaneTracker
{
    public event EventHandler<RoundControllerResolvedEventArgs>? ControllerResolved
    {
        add { }
        remove { }
    }

    public Address? GetController(ulong round) => null;

    public bool CurrentRoundHasController() => false;

    public bool IsWithinAuctionCloseWindow(DateTime t) => false;

    public Address AuctionContractAddress => Address.Zero;

    public void Start(CancellationToken ct) { }
}
