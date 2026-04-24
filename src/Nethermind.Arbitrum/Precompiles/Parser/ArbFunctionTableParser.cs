// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Collections.Frozen;
using Nethermind.Abi;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles;

public class ArbFunctionTableParser : IArbitrumPrecompile<ArbFunctionTableParser>
{
    public static readonly ArbFunctionTableParser Instance = new();

    public static Address Address { get; } = ArbFunctionTable.Address;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }
        = Solgen.ArbFunctionTable.Functions.All.ToFrozenDictionary(f => f.Key, f => f.Value.ToArbitrumFunctionDescription());

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    private const uint UploadId = Solgen.ArbFunctionTable.Methods.Upload;
    private const uint SizeId = Solgen.ArbFunctionTable.Methods.Size;
    private const uint GetId = Solgen.ArbFunctionTable.Methods.Get;

    static ArbFunctionTableParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { UploadId, Upload },
            { SizeId, Size },
            { GetId, Get },
        }.ToFrozenDictionary();
    }

    private static byte[] Upload(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[UploadId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        byte[] buf = (byte[])decoded[0];

        ArbFunctionTable.Upload(context, buf);

        return Array.Empty<byte>();
    }

    private static byte[] Size(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SizeId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address addr = (Address)decoded[0];
        UInt256 size = ArbFunctionTable.Size(context, addr);

        return size.ToBigEndian();
    }

    private static byte[] Get(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[GetId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address addr = (Address)decoded[0];
        UInt256 index = (UInt256)decoded[1];

        (UInt256 value1, bool value2, UInt256 value3) = ArbFunctionTable.Get(context, addr, index);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            value1,
            value2,
            value3
        );
    }
}
