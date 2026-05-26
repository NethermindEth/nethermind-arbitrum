// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

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
    public static Address Address => ArbosAddresses.ArbFunctionTableAddress;

    /// <summary>
    /// Upload does nothing (no-op for backwards compatibility)
    /// </summary>
    public static void Upload(ArbitrumPrecompileExecutionContext context, byte[] buf)
    {
        // Intentionally does nothing - kept for backwards compatibility
    }

    /// <summary>
    /// Size returns the empty table's size, which is 0
    /// </summary>
    public static UInt256 Size(ArbitrumPrecompileExecutionContext context, Address addr)
    {
        return UInt256.Zero;
    }

    /// <summary>
    /// Get fails since the table is empty
    /// </summary>
    public static (UInt256, bool, UInt256) Get(ArbitrumPrecompileExecutionContext context, Address addr, UInt256 index)
    {
        throw ArbitrumPrecompileException.CreateFailureException("table is empty");
    }
}
