// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core;

namespace Nethermind.Arbitrum.Sequencer.Timeboost;

public readonly record struct ResolvedRound(Address Controller, ulong Round)
{
    public static readonly ResolvedRound Empty = new(Address.Zero, 0);
}

public interface IAuctionContract
{
    Address Address { get; }

    ResolvedRound ResolveRounds();
}
