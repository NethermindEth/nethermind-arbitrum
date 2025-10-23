// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

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
public static class ArbosTest
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
        {
            throw ArbitrumPrecompileException.CreateRevertException("not a uint64");
        }

        // Burn the amount, even if it's more than the user has
        context.Burn((ulong)gasAmount);
    }
}
