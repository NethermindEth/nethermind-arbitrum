// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Frozen;
using Nethermind.Arbitrum.Arbos;
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
        = AbiMetadata.GetAllFunctionDescriptions(ArbBls.Abi);

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    static ArbBlsParser()
    {
        // Empty implementation - no functions to register
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>()
            .ToFrozenDictionary();
    }
}
