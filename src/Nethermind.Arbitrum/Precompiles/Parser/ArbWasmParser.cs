// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Abi;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbWasmParser : IArbitrumPrecompile<ArbWasmParser>
{
    public static readonly ArbWasmParser Instance = new();

    private static readonly uint ActivateProgramId;
    private static readonly AbiEncodingInfo ActivateProgramOutput;

    static ArbWasmParser()
    {
        Dictionary<string, AbiFunctionDescription> precompileFunctions = AbiMetadata.GetAllFunctionDescriptions(ArbWasm.Abi);

        ActivateProgramOutput = precompileFunctions["activateProgram"].GetReturnInfo();
        ActivateProgramId = MethodIdHelper.GetMethodId("activateProgram(address)");
    }

    public byte[] RunAdvanced(ArbitrumPrecompileExecutionContext context, ReadOnlyMemory<byte> inputData)
    {
        ReadOnlySpan<byte> inputDataSpan = inputData.Span;
        uint methodId = ArbitrumBinaryReader.ReadUInt32OrFail(ref inputDataSpan);

        return methodId switch
        {
            _ when methodId == ActivateProgramId => ActivateProgram(context, inputDataSpan),
            _ => throw new ArgumentException($"Unknown precompile method ID: {methodId}")
        };
    }

    private byte[] ActivateProgram(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        Address program = ArbitrumBinaryReader.ReadAddressFrom256OrFail(ref inputData);

        ArbWasmActivateProgramResult result = ArbWasm.ActivateProgram(context, program, context.Value);
        byte[] response = AbiEncoder.Instance.Encode(ActivateProgramOutput, result.Version, result.DataFee);

        return response;
    }
}
