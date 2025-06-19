
namespace Nethermind.Arbitrum.Precompiles.Parser;

using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Evm;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;

public class ArbRetryableTxParser : IArbitrumPrecompile<ArbRetryableTxParser>
{
    public static readonly ArbRetryableTxParser Instance = new();

    private readonly ArbRetryableTx _arbRetryableTx = new();
    public static Address Address { get; } = ArbRetryableTx.Address;

    private readonly uint _redeemId;
    private readonly uint _getLifetimeId;
    private readonly uint _getTimeoutId;
    private readonly uint _keepaliveId;
    private readonly uint _getBeneficiaryId;
    private readonly uint _cancelId;
    private readonly uint _getCurrentRedeemerId;
    private readonly uint _submitRetryableId;

    public ArbRetryableTxParser()
    {
        _redeemId = MethodIdHelper.GetMethodId("redeem(bytes32)");
        _getLifetimeId = MethodIdHelper.GetMethodId("getLifetime()");
        _getTimeoutId = MethodIdHelper.GetMethodId("getTimeout(bytes32)");
        _keepaliveId = MethodIdHelper.GetMethodId("keepalive(bytes32)");
        _getBeneficiaryId = MethodIdHelper.GetMethodId("getBeneficiary(bytes32)");
        _cancelId = MethodIdHelper.GetMethodId("cancel(bytes32)");
        _getCurrentRedeemerId = MethodIdHelper.GetMethodId("getCurrentRedeemer()");
        _submitRetryableId = MethodIdHelper.GetMethodId("submitRetryable(bytes32,uint256,uint256,uint256,uint256,uint64,uint256,address,address,address,bytes)");
    }

    public byte[] RunAdvanced(ArbitrumPrecompileExecutionContext context, ArbitrumVirtualMachine evm, ReadOnlyMemory<byte> inputData)
    {
        ReadOnlySpan<byte> inputDataSpan = inputData.Span;
        uint methodId = ArbitrumBinaryReader.ReadUInt32OrFail(ref inputDataSpan);

        if (methodId == _redeemId)
        {
            return Redeem(context, evm, inputDataSpan);
        }
        else if (methodId == _getLifetimeId)
        {
            return GetLifetime(context, evm, inputDataSpan);
        }
        else if (methodId == _getTimeoutId)
        {
            return GetTimeout(context, evm, inputDataSpan);
        }
        else if (methodId == _keepaliveId)
        {
            return KeepAlive(context, evm, inputDataSpan);
        }
        else if (methodId == _getBeneficiaryId)
        {
            return GetBeneficiary(context, evm, inputDataSpan);
        }
        else if (methodId == _cancelId)
        {
            return Cancel(context, evm, inputDataSpan);
        }
        else if (methodId == _getCurrentRedeemerId)
        {
            return GetCurrentRedeemer(context, evm, inputDataSpan);
        }
        else if (methodId == _submitRetryableId)
        {
            return SubmitRetryable(context, evm, inputDataSpan);
        }
        else
        {
            throw new ArgumentException($"Invalid precompile method ID: {methodId}");
        }
    }

    public byte[] Redeem(ArbitrumPrecompileExecutionContext context, ArbitrumVirtualMachine vm, ReadOnlySpan<byte> inputData)
    {
        Hash256 ticketId = ArbitrumBinaryReader.ReadHash256OrFail(ref inputData);

        return _arbRetryableTx.Redeem(context, vm, ticketId).BytesToArray();
    }

    public byte[] GetLifetime(ArbitrumPrecompileExecutionContext context, ArbitrumVirtualMachine vm, ReadOnlySpan<byte> inputData)
    {
        return _arbRetryableTx.GetLifetime(context, vm).ToBigEndian();
    }

    public byte[] GetTimeout(ArbitrumPrecompileExecutionContext context, ArbitrumVirtualMachine vm, ReadOnlySpan<byte> inputData)
    {
        Hash256 ticketId = ArbitrumBinaryReader.ReadHash256OrFail(ref inputData);

        return _arbRetryableTx.GetTimeout(context, vm, ticketId).ToBigEndian();
    }

    public byte[] KeepAlive(ArbitrumPrecompileExecutionContext context, ArbitrumVirtualMachine vm, ReadOnlySpan<byte> inputData)
    {
        Hash256 ticketId = ArbitrumBinaryReader.ReadHash256OrFail(ref inputData);

        return _arbRetryableTx.KeepAlive(context, vm, ticketId).ToBigEndian();
    }

    public byte[] GetBeneficiary(ArbitrumPrecompileExecutionContext context, ArbitrumVirtualMachine vm, ReadOnlySpan<byte> inputData)
    {
        Hash256 ticketId = ArbitrumBinaryReader.ReadHash256OrFail(ref inputData);

        return _arbRetryableTx.GetBeneficiary(context, vm, ticketId).Bytes;
    }

    public byte[] Cancel(ArbitrumPrecompileExecutionContext context, ArbitrumVirtualMachine vm, ReadOnlySpan<byte> inputData)
    {
        Hash256 ticketId = ArbitrumBinaryReader.ReadHash256OrFail(ref inputData);

        _arbRetryableTx.Cancel(context, vm, ticketId);

        return [];
    }

    public byte[] GetCurrentRedeemer(ArbitrumPrecompileExecutionContext context, ArbitrumVirtualMachine vm, ReadOnlySpan<byte> inputData)
    {
        return _arbRetryableTx.GetCurrentRedeemer(context, vm).Bytes;
    }

    public byte[] SubmitRetryable(ArbitrumPrecompileExecutionContext context, ArbitrumVirtualMachine vm, ReadOnlySpan<byte> inputData)
    {
        Hash256 requestId = ArbitrumBinaryReader.ReadHash256OrFail(ref inputData);
        UInt256 l1BaseFee = ArbitrumBinaryReader.ReadUInt256OrFail(ref inputData);
        UInt256 deposit = ArbitrumBinaryReader.ReadUInt256OrFail(ref inputData);
        UInt256 callvalue = ArbitrumBinaryReader.ReadUInt256OrFail(ref inputData);
        UInt256 gasFeeCap = ArbitrumBinaryReader.ReadUInt256OrFail(ref inputData);

        ReadOnlySpan<byte> gasLimitBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        ulong gasLimit = gasLimitBytes[(Hash256.Size - 8)..].ToULongFromBigEndianByteArrayWithoutLeadingZeros();

        UInt256 maxSubmissionFee = ArbitrumBinaryReader.ReadUInt256OrFail(ref inputData);

        ReadOnlySpan<byte> feeRefundAddressBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address feeRefundAddress = new(feeRefundAddressBytes[(Hash256.Size - Address.Size)..]);

        ReadOnlySpan<byte> beneficiaryBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address beneficiary = new(beneficiaryBytes[(Hash256.Size - Address.Size)..]);

        ReadOnlySpan<byte> retryToBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address retryTo = new(retryToBytes[(Hash256.Size - Address.Size)..]);

        _arbRetryableTx.SubmitRetryable(
            context, vm, requestId, l1BaseFee, deposit, callvalue,
            gasFeeCap, gasLimit, maxSubmissionFee, feeRefundAddress,
            beneficiary, retryTo, inputData.ToArray()
        );

        return [];
    }
}
