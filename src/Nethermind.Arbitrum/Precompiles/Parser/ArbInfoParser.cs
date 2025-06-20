
namespace Nethermind.Arbitrum.Precompiles.Parser;

using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Evm;
using Nethermind.Core;

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

    public (byte[], bool) RunAdvanced(ArbitrumPrecompileExecutionContext context, ReadOnlyMemory<byte> inputData)
    {
        ReadOnlySpan<byte> inputDataSpan = inputData.Span;
        uint methodId = ArbitrumBinaryReader.ReadUInt32OrFail(ref inputDataSpan);

        if (methodId == _getBalanceId)
        {
            return (GetBalance(context, inputDataSpan), true);
        }
        else if (methodId == _getCodeId)
        {
            return (GetCode(context, inputDataSpan), true);
        }
        else
        {
            return ([], false);
        }
    }

    private byte[] GetBalance(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        if (inputData.Length != 32)
        {
            throw new ArgumentException("Invalid input data length");
        }
        inputData = inputData[12..];

        Address account = ArbitrumBinaryReader.ReadAddressOrFail(ref inputData);
        Int256.UInt256 res = _arbInfo.GetBalance(context, account);

        return res.ToBigEndian();
    }

    private byte[] GetCode(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        if (inputData.Length != 32)
        {
            throw new ArgumentException("Invalid input data length");
        }
        inputData = inputData[12..];

        Address account = ArbitrumBinaryReader.ReadAddressOrFail(ref inputData);

        return _arbInfo.GetCode(context, account);
    }

}
