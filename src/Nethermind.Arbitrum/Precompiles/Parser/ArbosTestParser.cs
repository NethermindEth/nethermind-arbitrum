// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Collections.Frozen;
using Nethermind.Abi;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbosTestParser : IArbitrumPrecompile<ArbosTestParser>
{
    public static readonly ArbosTestParser Instance = new();

    public static Address Address { get; } = ArbosTest.Address;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }
        = Solgen.ArbosTest.Functions.All.ToFrozenDictionary(f => f.Key, f => f.Value.ToArbitrumFunctionDescription());

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    private const uint BurnArbGasId = Solgen.ArbosTest.Methods.BurnArbGas;

    static ArbosTestParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { BurnArbGasId, BurnArbGas },
        }.ToFrozenDictionary();
    }

    private static byte[] BurnArbGas(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[BurnArbGasId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        UInt256 gasAmount = (UInt256)decoded[0];

        ArbosTest.BurnArbGas(context, gasAmount);

        return Array.Empty<byte>();
    }
}
