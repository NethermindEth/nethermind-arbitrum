using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles;

public static class ArbRetryableTx
{
    public static Address Address => ArbosAddresses.ArbRetryableTxAddress;

    public static readonly string Abi =
        "[{\"inputs\":[],\"name\":\"NoTicketWithID\",\"type\":\"error\"},{\"inputs\":[],\"name\":\"NotCallable\",\"type\":\"error\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"}],\"name\":\"Canceled\",\"type\":\"event\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"},{\"indexed\":false,\"internalType\":\"uint256\",\"name\":\"newTimeout\",\"type\":\"uint256\"}],\"name\":\"LifetimeExtended\",\"type\":\"event\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"},{\"indexed\":true,\"internalType\":\"bytes32\",\"name\":\"retryTxHash\",\"type\":\"bytes32\"},{\"indexed\":true,\"internalType\":\"uint64\",\"name\":\"sequenceNum\",\"type\":\"uint64\"},{\"indexed\":false,\"internalType\":\"uint64\",\"name\":\"donatedGas\",\"type\":\"uint64\"},{\"indexed\":false,\"internalType\":\"address\",\"name\":\"gasDonor\",\"type\":\"address\"},{\"indexed\":false,\"internalType\":\"uint256\",\"name\":\"maxRefund\",\"type\":\"uint256\"},{\"indexed\":false,\"internalType\":\"uint256\",\"name\":\"submissionFeeRefund\",\"type\":\"uint256\"}],\"name\":\"RedeemScheduled\",\"type\":\"event\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"bytes32\",\"name\":\"userTxHash\",\"type\":\"bytes32\"}],\"name\":\"Redeemed\",\"type\":\"event\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"}],\"name\":\"TicketCreated\",\"type\":\"event\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"}],\"name\":\"cancel\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"}],\"name\":\"getBeneficiary\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"\",\"type\":\"address\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getCurrentRedeemer\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"\",\"type\":\"address\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getLifetime\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"}],\"name\":\"getTimeout\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"}],\"name\":\"keepalive\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"}],\"name\":\"redeem\",\"outputs\":[{\"internalType\":\"bytes32\",\"name\":\"\",\"type\":\"bytes32\"}],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"requestId\",\"type\":\"bytes32\"},{\"internalType\":\"uint256\",\"name\":\"l1BaseFee\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"deposit\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"callvalue\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"gasFeeCap\",\"type\":\"uint256\"},{\"internalType\":\"uint64\",\"name\":\"gasLimit\",\"type\":\"uint64\"},{\"internalType\":\"uint256\",\"name\":\"maxSubmissionFee\",\"type\":\"uint256\"},{\"internalType\":\"address\",\"name\":\"feeRefundAddress\",\"type\":\"address\"},{\"internalType\":\"address\",\"name\":\"beneficiary\",\"type\":\"address\"},{\"internalType\":\"address\",\"name\":\"retryTo\",\"type\":\"address\"},{\"internalType\":\"bytes\",\"name\":\"retryData\",\"type\":\"bytes\"}],\"name\":\"submitRetryable\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"}]";

    // Events
    public static readonly AbiEventDescription TicketCreatedEvent;
    public static readonly AbiEventDescription LifetimeExtendedEvent;
    public static readonly AbiEventDescription RedeemScheduledEvent;
    public static readonly AbiEventDescription CanceledEvent;

    // Solidity errors
    public static readonly AbiErrorDescription NoTicketWithID;
    public static readonly AbiErrorDescription NotCallable;


    static ArbRetryableTx()
    {
        List<AbiEventDescription> allEvents = AbiMetadata.GetAllEventDescriptions(Abi)!;
        TicketCreatedEvent = allEvents.FirstOrDefault(e => e.Name == "TicketCreated") ?? throw new ArgumentException("TicketCreated event not found");
        RedeemScheduledEvent = allEvents.FirstOrDefault(e => e.Name == "RedeemScheduled") ?? throw new ArgumentException("RedeemScheduled event not found");
        LifetimeExtendedEvent = allEvents.FirstOrDefault(e => e.Name == "LifetimeExtended") ?? throw new ArgumentException("LifetimeExtended event not found");
        CanceledEvent = allEvents.FirstOrDefault(e => e.Name == "Canceled") ?? throw new ArgumentException("Canceled event not found");

        List<AbiErrorDescription> allErrors = AbiMetadata.GetAllErrorDescriptions(Abi)!;
        NoTicketWithID = allErrors.FirstOrDefault(e => e.Name == "NoTicketWithID") ?? throw new ArgumentException("NoTicketWithID error not found");
        NotCallable = allErrors.FirstOrDefault(e => e.Name == "NotCallable") ?? throw new ArgumentException("NotCallable error not found");
    }

    public static void EmitTicketCreatedEvent(ArbitrumPrecompileExecutionContext context, Hash256 ticketId)
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(TicketCreatedEvent, Address, ticketId);
        EventsEncoder.EmitEvent(context, eventLog);
    }

    public static void EmitRedeemScheduledEvent(
        ArbitrumPrecompileExecutionContext context,
        Hash256 ticketId, Hash256 retryTxHash, ulong sequenceNum, ulong donatedGas,
        Address gasDonor, UInt256 maxRefund, UInt256 submissionFeeRefund
    )
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(
            RedeemScheduledEvent, Address, ticketId, retryTxHash, sequenceNum,
            donatedGas, gasDonor, maxRefund, submissionFeeRefund
        );
        EventsEncoder.EmitEvent(context, eventLog);
    }

    public static void EmitLifetimeExtendedEvent(ArbitrumPrecompileExecutionContext context, Hash256 ticketId, UInt256 newTimeout)
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(LifetimeExtendedEvent, Address, ticketId, newTimeout);
        EventsEncoder.EmitEvent(context, eventLog);
    }

    public static void EmitCanceledEvent(ArbitrumPrecompileExecutionContext context, Hash256 ticketId)
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(CanceledEvent, Address, ticketId);
        EventsEncoder.EmitEvent(context, eventLog);
    }

    public static ulong TicketCreatedEventGasCost(Hash256 ticketId)
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(TicketCreatedEvent, Address, ticketId);
        return EventsEncoder.EventCost(eventLog);
    }

    public static ulong RedeemScheduledEventGasCost(
        Hash256 ticketId, Hash256 retryTxHash, ulong sequenceNum, ulong donatedGas,
        Address gasDonor, UInt256 maxRefund, UInt256 submissionFeeRefund
    )
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(
            RedeemScheduledEvent, Address, ticketId, retryTxHash, sequenceNum,
            donatedGas, gasDonor, maxRefund, submissionFeeRefund
        );
        return EventsEncoder.EventCost(eventLog);
    }

    public static ulong LifetimeExtendedEventGasCost(Hash256 ticketId, UInt256 newTimeout)
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(LifetimeExtendedEvent, Address, ticketId, newTimeout);
        return EventsEncoder.EventCost(eventLog);
    }

    public static ulong CanceledEventGasCost(Hash256 ticketId)
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(CanceledEvent, Address, ticketId);
        return EventsEncoder.EventCost(eventLog);
    }

    public static PrecompileSolidityError NoTicketWithIdSolidityError()
    {
        byte[] errorData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            new AbiSignature(NoTicketWithID.Name, NoTicketWithID.Inputs.Select(p => p.Type).ToArray()),
            []
        );
        return new PrecompileSolidityError(errorData);
    }

    public static PrecompileSolidityError NotCallableSolidityError()
    {
        byte[] errorData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            new AbiSignature(NotCallable.Name, NotCallable.Inputs.Select(p => p.Type).ToArray()),
            []
        );
        return new PrecompileSolidityError(errorData);
    }
    
    public static byte[] PackArbRetryableTxRedeem(params object[] arguments)
    {
        AbiSignature signature = AbiMetadata.GetAbiSignature(Abi, "redeem");
        return new AbiEncoder().Encode(AbiEncodingStyle.IncludeSignature, signature, arguments);
    }
    

    private static void ThrowOldNotFoundError(ArbitrumPrecompileExecutionContext context)
    {
        if (context.ArbosState.CurrentArbosVersion >= ArbosVersion.Three)
        {
            throw NoTicketWithIdSolidityError();
        }

        throw new Exception("TicketId not found");
    }

    // Redeem schedules an attempt to redeem the retryable, donating all of the call's gas to the redeem attempt
    public static Hash256 Redeem(ArbitrumPrecompileExecutionContext context, Hash256 ticketId)
    {
        if (ticketId == context.CurrentRetryable)
        {
            throw SelfModifyingRetryableException();
        }

        RetryableState state = context.ArbosState.RetryableState;
        ulong byteCount = state.RetryableSizeBytes(
            ticketId,
            context.BlockExecutionContext.Header.Timestamp
        );

        ulong writeBytes = Math.Utils.Div32Ceiling(byteCount);
        context.Burn(GasCostOf.SLoad * writeBytes);

        Retryable? retryable = state.OpenRetryable(
            ticketId,
            context.BlockExecutionContext.Header.Timestamp
        );
        if (retryable is null)
        {
            ThrowOldNotFoundError(context);
        }

        ulong nonce = retryable!.IncrementNumTries() - 1;

        UInt256 maxRefund = UInt256.MaxValue;
        ArbitrumRetryTx retryTxInner = new(
            context.ChainId,
            nonce,
            retryable.From.Get(),
            context.BlockExecutionContext.Header.BaseFeePerGas,
            0, // will fill this in below (retryable fields access gas cost should not be included in futureGasCosts)
            retryable.To?.Get(),
            retryable.CallValue.Get(),
            retryable.Calldata.Get(),
            new Hash256(ticketId),
            context.Caller,
            maxRefund,
            0
        );

        // figure out how much gas the event issuance will cost, and reduce the donated gas amount
        // in the event by that much, so that we'll donate the correct amount of gas
        ulong eventGasCost = RedeemScheduledEventGasCost(Hash256.Zero, Hash256.Zero, 0, 0, Address.Zero, 0, 0);

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

        // fix up the gas in the retry (now that gasToDonate has been computed)
        retryTxInner.Gas = gasToDonate;

        var transaction = new ArbitrumTransaction<ArbitrumRetryTx>(retryTxInner);
        Hash256 retryTxHash = transaction.CalculateHash();

        EmitRedeemScheduledEvent(
            context, ticketId, retryTxHash, nonce, gasToDonate, context.Caller, maxRefund, 0
        );

        // To prepare for the enqueued retry event, we burn gas here, adding it back to the pool right before retrying.
        // The gas payer for this tx will get a credit for the wei they paid for this gas when retrying.
        // We burn as much gas as we can, leaving only enough to pay for copying out the return data.
        context.Burn(gasToDonate);

        // Add the gasToDonate back to the gas pool: the retryable attempt will then consume it.
        // This ensures that the gas pool has enough gas to run the retryable attempt.
        context.ArbosState.L2PricingState.AddToGasPool(long.CreateSaturating(gasToDonate));

        return retryTxHash;
    }

    // GetLifetime gets the default lifetime period a retryable has at creation
    public static UInt256 GetLifetime(ArbitrumPrecompileExecutionContext _)
    {
        return Retryable.RetryableLifetimeSeconds;
    }

    // GetTimeout gets the timestamp for when ticket will expire
    public static UInt256 GetTimeout(ArbitrumPrecompileExecutionContext context, Hash256 ticketId)
    {
        RetryableState retryableState = context.ArbosState.RetryableState;

        Retryable retryable = retryableState.OpenRetryable(
            ticketId, context.BlockExecutionContext.Header.Timestamp
        ) ?? throw NoTicketWithIdSolidityError();

        return retryable.CalculateTimeout();
    }

    // KeepAlive adds one lifetime period to the ticket's expiry
    public static UInt256 KeepAlive(ArbitrumPrecompileExecutionContext context, Hash256 ticketId)
    {
        ulong currentTime = context.BlockExecutionContext.Header.Timestamp;

        // charge for the expiry update
        RetryableState retryableState = context.ArbosState.RetryableState;
        ulong byteCount = retryableState.RetryableSizeBytes(ticketId, currentTime);
        if (byteCount == 0)
        {
            ThrowOldNotFoundError(context);
        }

        ulong updateCost = Math.Utils.Div32Ceiling(byteCount) * GasCostOf.SSet / 100;
        context.Burn(updateCost);

        ulong newTimeout = retryableState.KeepAlive(ticketId, currentTime);

        EmitLifetimeExtendedEvent(context, ticketId, newTimeout);
        return newTimeout;
    }

    public static Address GetBeneficiary(ArbitrumPrecompileExecutionContext context, Hash256 ticketId)
    {
        RetryableState retryableState = context.ArbosState.RetryableState;
        Retryable? retryable = retryableState.OpenRetryable(
            ticketId, context.BlockExecutionContext.Header.Timestamp
        );
        if (retryable is null)
        {
            ThrowOldNotFoundError(context);
        }

        return retryable!.Beneficiary.Get();
    }

    public static void Cancel(ArbitrumPrecompileExecutionContext context, Hash256 ticketId)
    {
        if (context.CurrentRetryable == ticketId)
        {
            throw SelfModifyingRetryableException();
        }

        Address beneficiary = GetBeneficiary(context, ticketId);

        if (context.Caller != beneficiary)
        {
            throw new InvalidOperationException("Only the beneficiary may cancel a retryable");
        }

        // No refunds are given for deleting retryables because they use rented space
        bool success = ArbitrumTransactionProcessor.DeleteRetryable(ticketId, context.ArbosState, context.WorldState,
            context.ReleaseSpec, context.TracingInfo!.Tracer, context.TracingInfo.Scenario);
        if (!success)
        {
            throw new InvalidOperationException("Failed to delete retryable");
        }

        EmitCanceledEvent(context, ticketId);
    }

    // Gets the redeemer of the current retryable redeem attempt.
    // Returns the zero address if the current transaction is not a retryable redeem attempt.
    // If this is an auto-redeem, returns the fee refund address of the retryable.
    public static Address GetCurrentRedeemer(ArbitrumPrecompileExecutionContext context)
    {
        return context.CurrentRefundTo ?? Address.Zero;
    }

    // Do not call. This method represents a retryable submission to aid explorers. Calling it will always revert.
    public static void SubmitRetryable(
        ArbitrumPrecompileExecutionContext context, Hash256 requestId, UInt256 l1BaseFee,
        UInt256 deposit, UInt256 callvalue, UInt256 gasFeeCap, ulong gasLimit,
        UInt256 maxSubmissionFee, Address feeRefundAddress, Address beneficiary,
        Address retryTo, byte[] retryData
    )
    {
        throw NotCallableSolidityError();
    }

    public static InvalidOperationException SelfModifyingRetryableException()
    {
        return new InvalidOperationException("Retryable cannot modify itself");
    }

    public static ArbRetryableTxRedeemScheduled DecodeRedeemScheduledEvent(LogEntry logEntry)
    {
        var data = EventsEncoder.DecodeEvent(RedeemScheduledEvent, logEntry);
        return new ArbRetryableTxRedeemScheduled()
        {
            TicketId = new ValueHash256((byte[])data["ticketId"]),
            RetryTxHash = new ValueHash256((byte[])data["retryTxHash"]),
            SequenceNum = (ulong)data["sequenceNum"],
            DonatedGas = (ulong)data["donatedGas"],
            GasDonor = (Address)data["gasDonor"],
            MaxRefund = (UInt256)data["maxRefund"],
            SubmissionFeeRefund = (UInt256)data["submissionFeeRefund"]
        };
    }

    public struct ArbRetryableTxRedeemScheduled
    {
        public ValueHash256 TicketId;
        public ValueHash256 RetryTxHash;
        public ulong SequenceNum;
        public ulong DonatedGas;
        public Address GasDonor;
        public UInt256 MaxRefund;
        public UInt256 SubmissionFeeRefund;
    }
}
