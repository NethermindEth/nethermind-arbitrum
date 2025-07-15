using Nethermind.Abi;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbInfoParser : IArbitrumPrecompile<ArbInfoParser>
{
    public static readonly ArbInfoParser Instance = new();
    public static Address Address { get; } = ArbInfo.Address;

    private static readonly List<AbiFunctionDescription> precompileFunctions;

    private static readonly uint _getBalanceId;
    private static readonly uint _getCodeId;


    static ArbInfoParser()
    {
        precompileFunctions = AbiMetadata.GetAllFunctionDescriptions(ArbInfo.Abi);

        _getBalanceId = MethodIdHelper.GetMethodId("getBalance(address)");
        _getCodeId = MethodIdHelper.GetMethodId("getCode(address)");
    }

    public byte[] RunAdvanced(ArbitrumPrecompileExecutionContext context, ReadOnlyMemory<byte> inputData)
    {
        ReadOnlySpan<byte> inputDataSpan = inputData.Span;
        uint methodId = ArbitrumBinaryReader.ReadUInt32OrFail(ref inputDataSpan);

        if (methodId == _getBalanceId)
        {
            return GetBalance(context, inputDataSpan);
        }

        if (methodId == _getCodeId)
        {
            return GetCode(context, inputDataSpan);
        }

        throw new ArgumentException($"Invalid precompile method ID: {methodId}");
    }

    private static byte[] GetBalance(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> accountBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address account = new(accountBytes[(Hash256.Size - Address.Size)..]);

        return ArbInfo.GetBalance(context, account).ToBigEndian();
    }

    private static byte[] GetCode(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> accountBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address account = new(accountBytes[(Hash256.Size - Address.Size)..]);

        byte[] code = ArbInfo.GetCode(context, account);

        AbiFunctionDescription function = precompileFunctions.FirstOrDefault(e => e.Name == "getCode")
            ?? throw new ArgumentException("getCode function not found");

        byte[] encodedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            code
        );

        return encodedResult;
    }

}
