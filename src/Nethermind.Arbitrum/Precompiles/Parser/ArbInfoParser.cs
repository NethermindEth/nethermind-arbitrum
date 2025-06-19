
namespace Nethermind.Arbitrum.Precompiles.Parser;

using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Evm;
using Nethermind.Core;
using Nethermind.Core.Crypto;

public class ArbInfoParser : IArbitrumPrecompile<ArbInfoParser>
{
    public static readonly ArbInfoParser Instance = new();

    private readonly ArbInfo _arbInfo = new();
    public static Address Address { get; } = ArbInfo.Address;

    private readonly uint _getBalanceId;
    private readonly uint _getCodeId;

    public ArbInfoParser()
    {
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
        else if (methodId == _getCodeId)
        {
            return GetCode(context, inputDataSpan);
        }
        else
        {
            throw new ArgumentException($"Invalid precompile method ID: {methodId}");
        }
    }

    private byte[] GetBalance(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> accountBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address account = new(accountBytes[(Hash256.Size - Address.Size)..]);

        return _arbInfo.GetBalance(context, account).ToBigEndian();
    }

    private byte[] GetCode(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> accountBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address account = new(accountBytes[(Hash256.Size - Address.Size)..]);

        return _arbInfo.GetCode(context, account);
    }

}
