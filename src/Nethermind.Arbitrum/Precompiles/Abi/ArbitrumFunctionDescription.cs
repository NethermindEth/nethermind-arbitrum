// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Abi;

namespace Nethermind.Arbitrum.Precompiles.Abi;

public class ArbitrumFunctionDescription(AbiFunctionDescription abiFunctionDescription)
{
    public AbiFunctionDescription AbiFunctionDescription { get; } = abiFunctionDescription;

    // Minimum ArbOS version required for a precompile's method to be active
    public ulong ArbOSVersion { get; set; }

    // Maximum ArbOS version for a precompile's method until which to stay active
    public ulong? MaxArbOSVersion { get; set; }
}
