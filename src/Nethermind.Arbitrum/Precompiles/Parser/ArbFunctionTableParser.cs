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
        = AbiMetadata.GetAllFunctionDescriptions(ArbFunctionTable.Abi);

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    private static readonly uint _uploadId = PrecompileHelper.GetMethodId("upload(bytes)");
    private static readonly uint _sizeId = PrecompileHelper.GetMethodId("size(address)");
    private static readonly uint _getId = PrecompileHelper.GetMethodId("get(address,uint256)");

    static ArbFunctionTableParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { _uploadId, Upload },
            { _sizeId, Size },
            { _getId, Get },
        }.ToFrozenDictionary();
    }

    private static byte[] Upload(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_uploadId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_sizeId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address addr = (Address)decoded[0];
        UInt256 size = ArbFunctionTable.Size(context, addr);

        return size.ToBigEndian();
    }

    private static byte[] Get(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_getId].AbiFunctionDescription;

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
