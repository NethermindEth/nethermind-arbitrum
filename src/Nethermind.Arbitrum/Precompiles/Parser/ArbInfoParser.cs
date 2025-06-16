
namespace Nethermind.Arbitrum.Precompiles.Parser;

using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Evm;
using Nethermind.Core;
using Nethermind.Core.Crypto;

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

    public byte[] RunAdvanced(ArbitrumPrecompileExecutionContext context, ArbVirtualMachine evm, ReadOnlyMemory<byte> inputData)
    {
        ReadOnlySpan<byte> inputDataSpan = inputData.Span;
        uint methodId = ArbitrumBinaryReader.ReadUInt32OrFail(ref inputDataSpan);

        if (methodId == _getBalanceId)
        {
            return GetBalance(context, evm, inputDataSpan);
        }
        else if (methodId == _getCodeId)
        {
            return GetCode(context, evm, inputDataSpan);
        }
        else
        {
            throw new ArgumentException($"Invalid precompile method ID: {methodId}");
        }
    }

    public byte[] GetBalance(ArbitrumPrecompileExecutionContext context, ArbVirtualMachine vm, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> accountBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address account = new(accountBytes[(Hash256.Size - Address.Size)..]);

        Int256.UInt256 res = _arbInfo.GetBalance(context, vm, account);

        return res.ToBigEndian();
    }

    public byte[] GetCode(ArbitrumPrecompileExecutionContext context, ArbVirtualMachine vm, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> accountBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address account = new(accountBytes[(Hash256.Size - Address.Size)..]);

        return _arbInfo.GetCode(context, vm, account);
    }

}