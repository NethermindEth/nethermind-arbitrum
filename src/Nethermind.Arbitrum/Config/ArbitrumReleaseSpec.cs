// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;
using Nethermind.Specs;
using System.Collections.Frozen;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumReleaseSpec : ReleaseSpec
{
    public override FrozenSet<AddressAsKey> BuildPrecompilesCache()
    {
        // Get Ethereum precompiles based on fork activation flags (EIP-198, EIP-152, EIP-2537, etc.)
        FrozenSet<AddressAsKey> ethereumPrecompiles = base.BuildPrecompilesCache();

        // Create a mutable set starting with Ethereum precompiles
        HashSet<AddressAsKey> allPrecompiles =
        [
            ..ethereumPrecompiles,
            // Add all Arbitrum precompiles (active since ArbOS 11)
            // These are protocol-level contracts that should not incur cold access costs
            ArbosAddresses.ArbSysAddress, // 0x64 - System operations
            ArbosAddresses.ArbInfoAddress, // 0x65 - Chain info
            ArbosAddresses.ArbAddressTableAddress, // 0x66 - Address compression
            ArbosAddresses.ArbBLSAddress, // 0x67 - BLS signatures
            ArbosAddresses.ArbFunctionTableAddress, // 0x68 - Function table
            ArbosAddresses.ArbosTestAddress, // 0x69 - Testing utilities
            ArbosAddresses.ArbOwnerPublicAddress, // 0x6b - Chain owner (public)
            ArbosAddresses.ArbGasInfoAddress, // 0x6c - Gas pricing info
            ArbosAddresses.ArbAggregatorAddress, // 0x6d - Aggregator info
            ArbosAddresses.ArbRetryableTxAddress, // 0x6e - Retryable transactions
            ArbosAddresses.ArbStatisticsAddress, // 0x6f - Chain statistics
            ArbosAddresses.ArbOwnerAddress, // 0x70 - Chain owner (privileged)
            ArbosAddresses.ArbDebugAddress, // 0xff - Debug utilities
            ArbosAddresses.ArbosAddress // 0xa4b05 - ArbOS state access
        ];


        return allPrecompiles.ToFrozenSet();
    }
}
