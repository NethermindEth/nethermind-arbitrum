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
    private static readonly uint SAddressExistsId;
    private static readonly uint SCompressId;
    private static readonly uint SDecompressId;
    private static readonly uint SLookupId;
    private static readonly uint SLookupIndexId;
    private static readonly uint SRegisterId;
    private static readonly uint SSizeId;

    static ArbAddressTableParser()
    {
        SPrecompileFunctions = AbiMetadata.GetAllFunctionDescriptions(ArbAddressTable.Abi);

        SAddressExistsId = GetMethodId("addressExists(address)");
        SCompressId = GetMethodId("compress(address)");
        SDecompressId = GetMethodId("decompress(bytes,uint256)");
        SLookupId = GetMethodId("lookup(address)");
        SLookupIndexId = GetMethodId("lookupIndex(uint256)");
        SRegisterId = GetMethodId("register(address)");
        SSizeId = GetMethodId("size()");
    }

    public byte[] RunAdvanced(ArbitrumPrecompileExecutionContext context, ReadOnlyMemory<byte> inputData)
    {
        ReadOnlySpan<byte> inputDataSpan = inputData.Span;
        uint methodId = ArbitrumBinaryReader.ReadUInt32OrFail(ref inputDataSpan);

        return methodId switch
        {
            _ when methodId == SAddressExistsId => AddressExists(context, inputDataSpan),
            _ when methodId == SCompressId => Compress(context, inputDataSpan),
            _ when methodId == SDecompressId => Decompress(context, inputDataSpan),
            _ when methodId == SLookupId => Lookup(context, inputDataSpan),
            _ when methodId == SLookupIndexId => LookupIndex(context, inputDataSpan),
            _ when methodId == SRegisterId => Register(context, inputDataSpan),
            _ when methodId == SSizeId => Size(context),
            _ => throw new ArgumentException($"Invalid precompile method ID: {methodId}")
        };
    }

    private static byte[] AddressExists(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> addressBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address address = new(addressBytes[(Hash256.Size - Address.Size)..]);

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
        ReadOnlySpan<byte> addressBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address address = new(addressBytes[(Hash256.Size - Address.Size)..]);

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
        // Read the ABI-encoded parameters: bytes buffer and uint256 offset
        object[] parameters = AbiEncoder.Instance.Decode(AbiEncodingStyle.None,
            SPrecompileFunctions["decompress"].GetCallInfo().Signature,
            inputData.ToArray());

        byte[] buffer = (byte[])parameters[0];
        UInt256 offset = (UInt256)parameters[1];

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
        ReadOnlySpan<byte> addressBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address address = new(addressBytes[(Hash256.Size - Address.Size)..]);

        UInt256 index = ArbAddressTable.Lookup(context, address);

        return index.ToBigEndian();
    }

    private static byte[] LookupIndex(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> indexBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        UInt256 index = new(indexBytes, true);

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
        ReadOnlySpan<byte> addressBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address address = new(addressBytes[(Hash256.Size - Address.Size)..]);

        UInt256 slot = ArbAddressTable.Register(context, address);

        return slot.ToBigEndian();
    }

    private static byte[] Size(ArbitrumPrecompileExecutionContext context)
        => ArbAddressTable.Size(context).ToBigEndian();
}
