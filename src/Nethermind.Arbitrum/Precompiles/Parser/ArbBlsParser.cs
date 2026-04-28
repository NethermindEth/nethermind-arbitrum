// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Collections.Frozen;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Precompiles.Parser;

/// <summary>
/// Parser for ArbBLS precompile.
/// This precompile is disabled in Arbitrum - no functions are implemented.
/// </summary>
public class ArbBlsParser : IArbitrumPrecompile<ArbBlsParser>
{
    public static readonly ArbBlsParser Instance = new();

    public static Address Address { get; } = ArbBls.Address;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }
        = FrozenDictionary<uint, ArbitrumFunctionDescription>.Empty;

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }
        = FrozenDictionary<uint, PrecompileHandler>.Empty;
}
