using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Evm;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles;

public class ArbRetryableTx
{
    public static Address Address => ArbosAddresses.ArbRetryableTxAddress;

    public static readonly string Metadata =
        "[{\"inputs\":[],\"name\":\"NoTicketWithID\",\"type\":\"error\"},{\"inputs\":[],\"name\":\"NotCallable\",\"type\":\"error\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"}],\"name\":\"Canceled\",\"type\":\"event\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"},{\"indexed\":false,\"internalType\":\"uint256\",\"name\":\"newTimeout\",\"type\":\"uint256\"}],\"name\":\"LifetimeExtended\",\"type\":\"event\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"},{\"indexed\":true,\"internalType\":\"bytes32\",\"name\":\"retryTxHash\",\"type\":\"bytes32\"},{\"indexed\":true,\"internalType\":\"uint64\",\"name\":\"sequenceNum\",\"type\":\"uint64\"},{\"indexed\":false,\"internalType\":\"uint64\",\"name\":\"donatedGas\",\"type\":\"uint64\"},{\"indexed\":false,\"internalType\":\"address\",\"name\":\"gasDonor\",\"type\":\"address\"},{\"indexed\":false,\"internalType\":\"uint256\",\"name\":\"maxRefund\",\"type\":\"uint256\"},{\"indexed\":false,\"internalType\":\"uint256\",\"name\":\"submissionFeeRefund\",\"type\":\"uint256\"}],\"name\":\"RedeemScheduled\",\"type\":\"event\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"bytes32\",\"name\":\"userTxHash\",\"type\":\"bytes32\"}],\"name\":\"Redeemed\",\"type\":\"event\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"}],\"name\":\"TicketCreated\",\"type\":\"event\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"}],\"name\":\"cancel\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"}],\"name\":\"getBeneficiary\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"\",\"type\":\"address\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getCurrentRedeemer\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"\",\"type\":\"address\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getLifetime\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"}],\"name\":\"getTimeout\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"}],\"name\":\"keepalive\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"}],\"name\":\"redeem\",\"outputs\":[{\"internalType\":\"bytes32\",\"name\":\"\",\"type\":\"bytes32\"}],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"requestId\",\"type\":\"bytes32\"},{\"internalType\":\"uint256\",\"name\":\"l1BaseFee\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"deposit\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"callvalue\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"gasFeeCap\",\"type\":\"uint256\"},{\"internalType\":\"uint64\",\"name\":\"gasLimit\",\"type\":\"uint64\"},{\"internalType\":\"uint256\",\"name\":\"maxSubmissionFee\",\"type\":\"uint256\"},{\"internalType\":\"address\",\"name\":\"feeRefundAddress\",\"type\":\"address\"},{\"internalType\":\"address\",\"name\":\"beneficiary\",\"type\":\"address\"},{\"internalType\":\"address\",\"name\":\"retryTo\",\"type\":\"address\"},{\"internalType\":\"bytes\",\"name\":\"retryData\",\"type\":\"bytes\"}],\"name\":\"submitRetryable\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"}]";

    public static readonly AbiEventDescription TicketCreatedEvent;
    public static readonly AbiEventDescription RedeemScheduledEvent;
    public static readonly AbiEventDescription LifetimeExtendedEvent;

    static ArbRetryableTx()
    {
        List<AbiEventDescription> allEvents = AbiMetadata.GetAllEventDescriptions(Metadata)!;
        TicketCreatedEvent = allEvents.FirstOrDefault(e => e.Name == "TicketCreated") ?? throw new ArgumentException("TicketCreated event not found");
        RedeemScheduledEvent = allEvents.FirstOrDefault(e => e.Name == "RedeemScheduled") ?? throw new ArgumentException("RedeemScheduled event not found");
        LifetimeExtendedEvent = allEvents.FirstOrDefault(e => e.Name == "LifetimeExtended") ?? throw new ArgumentException("LifetimeExtended event not found");
    }

    /********************************
     *          Events
     ********************************/
    public static LogEntry TicketCreated(Context context, ArbVirtualMachine vm, byte[] ticketId)
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(TicketCreatedEvent, Address, ticketId);
        return EventsEncoder.EmitEvent(context, vm, eventLog);
    }

    public static LogEntry RedeemScheduled(
        Context context, ArbVirtualMachine vm,
        byte[] ticketId, byte[] retryTxHash, ulong sequenceNum, ulong donatedGas,
        Address gasDonor, UInt256 maxRefund, UInt256 submissionFeeRefund
    )
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(
            RedeemScheduledEvent, Address, ticketId, retryTxHash, sequenceNum,
            donatedGas, gasDonor, maxRefund, submissionFeeRefund
        );
        return EventsEncoder.EmitEvent(context, vm, eventLog);
    }

    public static LogEntry LifetimeExtended(Context context, ArbVirtualMachine vm, Hash256 ticketId, UInt256 newTimeout)
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(LifetimeExtendedEvent, Address, ticketId, newTimeout);
        return EventsEncoder.EmitEvent(context, vm, eventLog);
    }

    /********************************
     *          Events Cost
     ********************************/
    public static ulong TicketCreatedGasCost(byte[] ticketId)
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(TicketCreatedEvent, Address, ticketId);
        return EventsEncoder.EventCost(eventLog);
    }

    public static ulong RedeemScheduledGasCost(
        byte[] ticketId, byte[] retryTxHash, ulong sequenceNum, ulong donatedGas,
        Address gasDonor, UInt256 maxRefund, UInt256 submissionFeeRefund
    )
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(
            RedeemScheduledEvent, Address, ticketId, retryTxHash, sequenceNum,
            donatedGas, gasDonor, maxRefund, submissionFeeRefund
        );
        return EventsEncoder.EventCost(eventLog);
    }

    public static ulong LifetimeExtendedGasCost(Hash256 ticketId, UInt256 newTimeout)
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(LifetimeExtendedEvent, Address, ticketId, newTimeout);
        return EventsEncoder.EventCost(eventLog);
    }


    /********************************
     *          Methods
     ********************************/

    private void oldNotFoundError(Context context) {
        if (context.ArbosState.CurrentArbosVersion >= ArbosVersion.Three)
        {
            //TODO evm error
        }
        //TODO maybe return an error here ? depending on above evm contract error implem
        throw new Exception("TicketId not found");
    }


    // Redeem schedules an attempt to redeem the retryable, donating all of the call's gas to the redeem attempt
    public Hash256 Redeem(Context context, ArbVirtualMachine vm, byte[] ticketId)
    {
        if (ticketId.Length != Hash256.Size)
        {
            throw new ArgumentException("Invalid ticketId length");
        }

        if (context.TxProcessor.CurrentRetryable is not null && ticketId == context.TxProcessor.CurrentRetryable.BytesToArray())
        {
            throw new Exception("Retryable cannot modify itself");
        }

        RetryableState state = context.ArbosState.RetryableState;

        ulong byteCount = state.RetryableSizeBytes(
            new ValueHash256(ticketId),
            vm.EvmState.Env.TxExecutionContext.BlockExecutionContext.Header.Timestamp
        );

        ulong writeBytes = (ulong)EvmPooledMemory.Div32Ceiling(byteCount);
        context.Burn(GasCostOf.SLoad * writeBytes);

        Retryable? retryable = state.OpenRetryable(
            new ValueHash256(ticketId),
            vm.EvmState.Env.TxExecutionContext.BlockExecutionContext.Header.Timestamp
        );
        if (retryable is null)
        {
            oldNotFoundError(context);
        }

        ulong nonce = retryable!.IncrementNumTries() - 1;

        // figure out how much gas the event issuance will cost, and reduce the donated gas amount
        // in the event by that much, so that we'll donate the correct amount of gas
        ulong eventGasCost = RedeemScheduledGasCost(new byte[32], new byte[32], 0, 0, Address.Zero, 0, 0);

	    // Result is 32 bytes long which is 1 word
        ulong gasCostToReturnResult = GasCostOf.DataCopy;
        ulong gasPoolUpdateCost = GasCostOf.SLoadEip1884 + GasCostOf.SSet;
        ulong futureGasCosts = eventGasCost + gasCostToReturnResult + gasPoolUpdateCost;

        if (context.GasLeft < futureGasCosts)
        {
            // This will throw
            context.Burn(futureGasCosts);
        }

        ulong gasToDonate = context.GasLeft - futureGasCosts;
        if (gasToDonate < GasCostOf.Transaction)
        {
            throw new Exception("Not enough gas to run redeem attempt");
        }

        UInt256 maxRefund = UInt256.MaxValue;
        ArbitrumRetryTx retryTxInner = retryable.MakeTx(
            vm.ChainId.ToULongFromBigEndianByteArrayWithoutLeadingZeros(),
            nonce,
            vm.EvmState.Env.TxExecutionContext.BlockExecutionContext.Header.BaseFeePerGas,
            gasToDonate,
            new Hash256(ticketId),
            context.Caller,
            maxRefund,
            0
        );

        var transaction = new ArbitrumTransaction<ArbitrumRetryTx>(retryTxInner);
        Hash256 retryTxHash = transaction.Hash!;

        RedeemScheduled(
            context, vm, ticketId, retryTxHash.BytesToArray(), nonce, gasToDonate, context.Caller, maxRefund, 0
        );

        // To prepare for the enqueued retry event, we burn gas here, adding it back to the pool right before retrying.
        // The gas payer for this tx will get a credit for the wei they paid for this gas when retrying.
        // We burn as much gas as we can, leaving only enough to pay for copying out the return data.
        context.Burn(gasToDonate);

        // Add the gasToDonate back to the gas pool: the retryable attempt will then consume it.
	    // This ensures that the gas pool has enough gas to run the retryable attempt.

        //TODO: finish implementing once saturating cast is available
        // see go: c.State.L2PricingState().AddToGasPool(arbmath.SaturatingCast[int64](gasToDonate))
        // context.ArbosState.L2PricingState.AddToGasPool()

        return retryTxHash;
    }

    // GetLifetime gets the default lifetime period a retryable has at creation
    public UInt256 GetLifetime(Context context, ArbVirtualMachine vm)
    {
        return Retryable.RetryableLifetimeSeconds;
    }

    // GetTimeout gets the timestamp for when ticket will expire
    public UInt256 GetTimeout(Context context, ArbVirtualMachine vm, Hash256 ticketId)
    {
        RetryableState retryableState = context.ArbosState.RetryableState;
        Retryable? retryable = retryableState.OpenRetryable(
            ticketId, vm.EvmState.Env.TxExecutionContext.BlockExecutionContext.Header.Timestamp
        );
        if (retryable is null)
        {
            //TODO contract error
        }

        return retryable!.CalculateTimeout();
    }


    // KeepAlive adds one lifetime period to the ticket's expiry
    public UInt256 KeepAlive(Context context, ArbVirtualMachine vm, Hash256 ticketId)
    {
        ulong currentTime = vm.EvmState.Env.TxExecutionContext.BlockExecutionContext.Header.Timestamp;

        // charge for the expiry update
        RetryableState retryableState = context.ArbosState.RetryableState;
        ulong byteCount = retryableState.RetryableSizeBytes(ticketId, currentTime);
        if (byteCount == 0)
        {
            oldNotFoundError(context);
        }

        ulong updateCost = (ulong)EvmPooledMemory.Div32Ceiling(byteCount) * GasCostOf.SSet / 100;
        context.Burn(updateCost);

        ulong window = currentTime + Retryable.RetryableLifetimeSeconds;
        ulong newTimeout = retryableState.KeepAlive(ticketId, currentTime, window);

        LifetimeExtended(context, vm, ticketId, newTimeout);
        return newTimeout;
    }
}