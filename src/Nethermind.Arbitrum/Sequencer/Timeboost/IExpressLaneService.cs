// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;

namespace Nethermind.Arbitrum.Sequencer.Timeboost;

public interface IExpressLaneService
{
    Address AuctionContractAddress { get; }

    bool CurrentRoundHasController();

    bool IsWithinAuctionCloseWindow(DateTime t);

    Task SequenceAsync(ExpressLaneSubmission submission, ulong currentBlockNumber);
}

public class DisabledExpressLaneService : IExpressLaneService
{
    public Address AuctionContractAddress => Address.Zero;

    public bool CurrentRoundHasController() => false;

    public bool IsWithinAuctionCloseWindow(DateTime t) => false;

    public Task SequenceAsync(ExpressLaneSubmission submission, ulong currentBlockNumber)
    {
        return Task.CompletedTask;
    }
}
