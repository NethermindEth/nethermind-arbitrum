// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles;

/// <summary>
/// Deprecated - Provides a method of burning arbitrary amounts of gas.
/// This exists for historical reasons. Pre-Nitro, ArbosTest had additional methods only the zero address could call.
/// These have been removed since users don't use them and calls to missing methods revert.
/// </summary>
public static class ArbTest
{
    public static Address Address => ArbosAddresses.ArbosTestAddress;

    public static readonly string Abi =
        "[{\"inputs\":[{\"internalType\":\"uint256\",\"name\":\"gasAmount\",\"type\":\"uint256\"}],\"name\":\"burnArbGas\",\"outputs\":[],\"stateMutability\":\"pure\",\"type\":\"function\"}]";

    /// <summary>
    /// Unproductively burns the amount of L2 ArbGas
    /// </summary>
    public static void BurnArbGas(ArbitrumPrecompileExecutionContext context, UInt256 gasAmount)
    {
        if (gasAmount > ulong.MaxValue)
            throw ArbitrumPrecompileException.CreateFailureException("not a uint64");

        // Burn the amount, even if it's more than the user has
        context.Burn((ulong)gasAmount);
    }
}
