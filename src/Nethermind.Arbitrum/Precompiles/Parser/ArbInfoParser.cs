
namespace Nethermind.Arbitrum.Precompiles.Parser;

using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Evm;
using Nethermind.Core;

public class ArbInfoParser: IArbitrumPrecompile<ArbInfoParser>
{
    public static readonly ArbInfoParser Instance = new();

    private readonly ArbInfo _arbInfo = new();
    public static Address Address { get; } = ArbInfo.Address;

    private readonly uint _getBalanceId;
    private readonly uint _getCodeId;

    public ArbInfoParser() {
        _getBalanceId = MethodIdHelper.GetMethodId("getBalance(address)");
        _getCodeId = MethodIdHelper.GetMethodId("getCode(address)");
    }

    public (byte[], bool) RunAdvanced(Context context, ArbVirtualMachine evm, ReadOnlyMemory<byte> inputData)
    {
        ReadOnlySpan<byte> inputDataSpan = inputData.Span;
        uint methodId = ArbitrumBinaryReader.ReadUInt32OrFail(ref inputDataSpan);

        if (methodId == _getBalanceId)
        {
            return (GetBalance(context, evm, inputDataSpan), true);
        }
        else if (methodId == _getCodeId)
        {
            return (GetCode(context, evm, inputDataSpan), true);
        }
        else
        {
            return ([], false);
        }
    }

    public byte[] GetBalance(Context context, ArbVirtualMachine vm, ReadOnlySpan<byte> inputData)
    {
        if (inputData.Length != 32)
        {
            throw new ArgumentException("Invalid input data length");
        }
        inputData = inputData[12..];

        Address account = ArbitrumBinaryReader.ReadAddressOrFail(ref inputData);
        Int256.UInt256 res = _arbInfo.GetBalance(context, vm, account);

        return res.ToBigEndian();
    }

    public byte[] GetCode(Context context, ArbVirtualMachine vm, ReadOnlySpan<byte> inputData)
    {
        if (inputData.Length != 32)
        {
            throw new ArgumentException("Invalid input data length");
        }
        inputData = inputData[12..];

        Address account = ArbitrumBinaryReader.ReadAddressOrFail(ref inputData);

        return _arbInfo.GetCode(context, vm, account);
    }

}