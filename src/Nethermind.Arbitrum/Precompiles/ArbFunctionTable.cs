// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Int256;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Precompiles;

/// <summary>
/// Deprecated - Provided aggregators the ability to manage function tables.
/// The Nitro aggregator implementation does not use these,
/// so these methods have been stubbed and their effects disabled.
/// They are kept for backwards compatibility.
/// </summary>
public static class ArbFunctionTable
{
    public static readonly string Abi =
        "[{\"inputs\":[{\"internalType\":\"bytes\",\"name\":\"buf\",\"type\":\"bytes\"}],\"name\":\"upload\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"addr\",\"type\":\"address\"}],\"name\":\"size\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"addr\",\"type\":\"address\"},{\"internalType\":\"uint256\",\"name\":\"index\",\"type\":\"uint256\"}],\"name\":\"get\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"},{\"internalType\":\"bool\",\"name\":\"\",\"type\":\"bool\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"}]";
    public static Address Address => ArbosAddresses.ArbFunctionTableAddress;

    /// <summary>
    /// Get fails since the table is empty
    /// </summary>
    public static (UInt256, bool, UInt256) Get(ArbitrumPrecompileExecutionContext context, Address addr, UInt256 index)
    {
        throw ArbitrumPrecompileException.CreateFailureException("table is empty");
    }

    /// <summary>
    /// Size returns the empty table's size, which is 0
    /// </summary>
    public static UInt256 Size(ArbitrumPrecompileExecutionContext context, Address addr)
    {
        return UInt256.Zero;
    }

    /// <summary>
    /// Upload does nothing (no-op for backwards compatibility)
    /// </summary>
    public static void Upload(ArbitrumPrecompileExecutionContext context, byte[] buf)
    {
        // Intentionally does nothing - kept for backwards compatibility
    }
}
