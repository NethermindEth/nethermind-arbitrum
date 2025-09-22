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

    private static readonly Dictionary<string, AbiFunctionDescription> SPrecompileFunctions;
    public static readonly uint AddressExistsId;
    public static readonly uint CompressId;
    public static readonly uint DecompressId;
    public static readonly uint LookupId;
    public static readonly uint LookupIndexId;
    public static readonly uint RegisterId;
    public static readonly uint SizeId;

    static ArbAddressTableParser()
    {
        SPrecompileFunctions = AbiMetadata.GetAllFunctionDescriptions(ArbAddressTable.Abi);

        AddressExistsId = GetMethodId("addressExists(address)");
        CompressId = GetMethodId("compress(address)");
        DecompressId = GetMethodId("decompress(bytes,uint256)");
        LookupId = GetMethodId("lookup(address)");
        LookupIndexId = GetMethodId("lookupIndex(uint256)");
        RegisterId = GetMethodId("register(address)");
        SizeId = GetMethodId("size()");
    }

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
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode(
            "addressExists",
            inputData,
            AbiType.Address
        );

        Address address = (Address)decoded[0];
        bool exists = ArbAddressTable.AddressExists(context, address);

        AbiFunctionDescription function = SPrecompileFunctions["addressExists"];
        byte[] encodedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            exists
        );

        return encodedResult;
    }

    private static byte[] Compress(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode(
            "compress",
            inputData,
            AbiType.Address
        );

        Address address = (Address)decoded[0];
        byte[] compressed = ArbAddressTable.Compress(context, address);

        AbiFunctionDescription function = SPrecompileFunctions["compress"];
        byte[] encodedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            compressed
        );

        return encodedResult;
    }

    private static byte[] Decompress(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode(
            "decompress",
            inputData,
            AbiType.DynamicBytes,
            AbiType.UInt256
        );

        byte[] buffer = (byte[])decoded[0];
        UInt256 offset = (UInt256)decoded[1];

        (Address address, UInt256 bytesRead) = ArbAddressTable.Decompress(context, buffer, offset);

        AbiFunctionDescription function = SPrecompileFunctions["decompress"];
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
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode(
            "lookup",
            inputData,
            AbiType.Address
        );

        Address address = (Address)decoded[0];
        UInt256 index = ArbAddressTable.Lookup(context, address);

        return index.ToBigEndian();
    }

    private static byte[] LookupIndex(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode(
            "lookupIndex",
            inputData,
            AbiType.UInt256
        );

        UInt256 index = (UInt256)decoded[0];
        Address address = ArbAddressTable.LookupIndex(context, index);

        AbiFunctionDescription function = SPrecompileFunctions["lookupIndex"];
        byte[] encodedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            address
        );

        return encodedResult;
    }

    private static byte[] Register(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode(
            "register",
            inputData,
            AbiType.Address
        );

        Address address = (Address)decoded[0];
        UInt256 slot = ArbAddressTable.Register(context, address);

        return slot.ToBigEndian();
    }

    private static byte[] Size(ArbitrumPrecompileExecutionContext context)
        => ArbAddressTable.Size(context).ToBigEndian();
}
