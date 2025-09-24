using Nethermind.Abi;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using static Nethermind.Arbitrum.Precompiles.MethodIdHelper;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public sealed class ArbAddressTableParser : IArbitrumPrecompile<ArbAddressTableParser>
{
    public static readonly ArbAddressTableParser Instance = new();

    public static Address Address { get; } = ArbAddressTable.Address;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctions { get; }
        = AbiMetadata.GetAllFunctionDescriptions(ArbAddressTable.Abi);

    public static readonly uint AddressExistsId = GetMethodId("addressExists(address)");
    public static readonly uint CompressId = GetMethodId("compress(address)");
    public static readonly uint DecompressId = GetMethodId("decompress(bytes,uint256)");
    public static readonly uint LookupId = GetMethodId("lookup(address)");
    public static readonly uint LookupIndexId = GetMethodId("lookupIndex(uint256)");
    public static readonly uint RegisterId = GetMethodId("register(address)");
    public static readonly uint SizeId = GetMethodId("size()");

    public byte[] RunAdvanced(ArbitrumPrecompileExecutionContext context, ReadOnlyMemory<byte> inputData)
    {
        ReadOnlySpan<byte> inputDataSpan = inputData.Span;
        uint methodId = ArbitrumBinaryReader.ReadUInt32OrFail(ref inputDataSpan);

        return methodId switch
        {
            _ when methodId == AddressExistsId => AddressExists(context, inputDataSpan),
            _ when methodId == CompressId => Compress(context, inputDataSpan),
            _ when methodId == DecompressId => Decompress(context, inputDataSpan),
            _ when methodId == LookupId => Lookup(context, inputDataSpan),
            _ when methodId == LookupIndexId => LookupIndex(context, inputDataSpan),
            _ when methodId == RegisterId => Register(context, inputDataSpan),
            _ when methodId == SizeId => Size(context),
            _ => throw new ArgumentException($"Invalid precompile method ID: {methodId}")
        };
    }

    private static byte[] AddressExists(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[AddressExistsId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address address = (Address)decoded[0];
        bool exists = ArbAddressTable.AddressExists(context, address);

        AbiFunctionDescription function = PrecompileFunctions[AddressExistsId].AbiFunctionDescription;
        byte[] encodedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            exists
        );

        return encodedResult;
    }

    private static byte[] Compress(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[CompressId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address address = (Address)decoded[0];
        byte[] compressed = ArbAddressTable.Compress(context, address);

        AbiFunctionDescription function = PrecompileFunctions[CompressId].AbiFunctionDescription;
        byte[] encodedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            compressed
        );

        return encodedResult;
    }

    private static byte[] Decompress(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[DecompressId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        byte[] buffer = (byte[])decoded[0];
        UInt256 offset = (UInt256)decoded[1];

        (Address address, UInt256 bytesRead) = ArbAddressTable.Decompress(context, buffer, offset);

        AbiFunctionDescription function = PrecompileFunctions[DecompressId].AbiFunctionDescription;
        byte[] encodedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            address.Bytes,
            bytesRead.ToBigEndian()
        );

        return encodedResult;
    }

    private static byte[] Lookup(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[LookupId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address address = (Address)decoded[0];
        UInt256 index = ArbAddressTable.Lookup(context, address);

        return index.ToBigEndian();
    }

    private static byte[] LookupIndex(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[LookupIndexId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        UInt256 index = (UInt256)decoded[0];
        Address address = ArbAddressTable.LookupIndex(context, index);

        AbiFunctionDescription function = PrecompileFunctions[LookupIndexId].AbiFunctionDescription;
        byte[] encodedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            address
        );

        return encodedResult;
    }

    private static byte[] Register(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[RegisterId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address address = (Address)decoded[0];
        UInt256 slot = ArbAddressTable.Register(context, address);

        return slot.ToBigEndian();
    }

    private static byte[] Size(ArbitrumPrecompileExecutionContext context)
        => ArbAddressTable.Size(context).ToBigEndian();
}
