// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Collections.Frozen;
using Nethermind.Abi;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public sealed class ArbAddressTableParser : IArbitrumPrecompile<ArbAddressTableParser>
{
    public static readonly ArbAddressTableParser Instance = new();

    public static Address Address { get; } = ArbAddressTable.Address;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }
        = AbiMetadata.GetAllFunctionDescriptions(ArbAddressTable.Abi);

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    private static readonly uint _addressExistsId = PrecompileHelper.GetMethodId("addressExists(address)");
    private static readonly uint _compressId = PrecompileHelper.GetMethodId("compress(address)");
    private static readonly uint _decompressId = PrecompileHelper.GetMethodId("decompress(bytes,uint256)");
    private static readonly uint _lookupId = PrecompileHelper.GetMethodId("lookup(address)");
    private static readonly uint _lookupIndexId = PrecompileHelper.GetMethodId("lookupIndex(uint256)");
    private static readonly uint _registerId = PrecompileHelper.GetMethodId("register(address)");
    private static readonly uint _sizeId = PrecompileHelper.GetMethodId("size()");

    static ArbAddressTableParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { _addressExistsId, AddressExists },
            { _compressId, Compress },
            { _decompressId, Decompress },
            { _lookupId, Lookup },
            { _lookupIndexId, LookupIndex },
            { _registerId, Register },
            { _sizeId, Size },
        }.ToFrozenDictionary();
    }

    private static byte[] AddressExists(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_addressExistsId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address address = (Address)decoded[0];
        bool exists = ArbAddressTable.AddressExists(context, address);

        byte[] encodedResult = PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            exists
        );

        return encodedResult;
    }

    private static byte[] Compress(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_compressId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address address = (Address)decoded[0];
        byte[] compressed = ArbAddressTable.Compress(context, address);

        byte[] encodedResult = PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            compressed
        );

        return encodedResult;
    }

    private static byte[] Decompress(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_decompressId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        byte[] buffer = (byte[])decoded[0];
        UInt256 offset = (UInt256)decoded[1];

        (Address address, UInt256 bytesRead) = ArbAddressTable.Decompress(context, buffer, offset);

        byte[] encodedResult = PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            address,
            bytesRead
        );

        return encodedResult;
    }

    private static byte[] Lookup(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_lookupId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address address = (Address)decoded[0];
        UInt256 index = ArbAddressTable.Lookup(context, address);

        return index.ToBigEndian();
    }

    private static byte[] LookupIndex(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_lookupIndexId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        UInt256 index = (UInt256)decoded[0];
        Address address = ArbAddressTable.LookupIndex(context, index);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            address
        );
    }

    private static byte[] Register(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_registerId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address address = (Address)decoded[0];
        UInt256 slot = ArbAddressTable.Register(context, address);

        return slot.ToBigEndian();
    }

    private static byte[] Size(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => ArbAddressTable.Size(context).ToBigEndian();
}
