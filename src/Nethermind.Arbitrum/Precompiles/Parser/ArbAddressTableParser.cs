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
        = Solgen.ArbAddressTable.Functions.All.ToFrozenDictionary(f => f.Key, f => f.Value.ToArbitrumFunctionDescription());

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    private const uint AddressExistsId = Solgen.ArbAddressTable.Methods.AddressExists;
    private const uint CompressId = Solgen.ArbAddressTable.Methods.Compress;
    private const uint DecompressId = Solgen.ArbAddressTable.Methods.Decompress;
    private const uint LookupId = Solgen.ArbAddressTable.Methods.Lookup;
    private const uint LookupIndexId = Solgen.ArbAddressTable.Methods.LookupIndex;
    private const uint RegisterId = Solgen.ArbAddressTable.Methods.Register;
    private const uint SizeId = Solgen.ArbAddressTable.Methods.Size;

    static ArbAddressTableParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { AddressExistsId, AddressExists },
            { CompressId, Compress },
            { DecompressId, Decompress },
            { LookupId, Lookup },
            { LookupIndexId, LookupIndex },
            { RegisterId, Register },
            { SizeId, Size },
        }.ToFrozenDictionary();
    }

    private static byte[] AddressExists(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[AddressExistsId].AbiFunctionDescription;

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
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[CompressId].AbiFunctionDescription;

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
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[DecompressId].AbiFunctionDescription;

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
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[LookupId].AbiFunctionDescription;

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
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[LookupIndexId].AbiFunctionDescription;

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
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[RegisterId].AbiFunctionDescription;

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
