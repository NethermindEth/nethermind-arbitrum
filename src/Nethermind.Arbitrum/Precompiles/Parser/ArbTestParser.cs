// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Collections.Frozen;
using Nethermind.Abi;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbTestParser : IArbitrumPrecompile<ArbTestParser>
{
    public static readonly ArbTestParser Instance = new();

    public static Address Address { get; } = ArbTest.Address;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }
        = AbiMetadata.GetAllFunctionDescriptions(ArbTest.Abi);

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    private static readonly uint _burnArbGasId = PrecompileHelper.GetMethodId("burnArbGas(uint256)");

    static ArbTestParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { _burnArbGasId, BurnArbGas },
        }.ToFrozenDictionary();
    }

    private static byte[] BurnArbGas(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_burnArbGasId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        UInt256 gasAmount = (UInt256)decoded[0];

        ArbTest.BurnArbGas(context, gasAmount);

        return Array.Empty<byte>();
    }
}
