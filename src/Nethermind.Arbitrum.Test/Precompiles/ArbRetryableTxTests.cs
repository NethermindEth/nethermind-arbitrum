using Nethermind.Arbitrum.Precompiles;
using Nethermind.Evm;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Specs.Forks;
using Nethermind.Core;
using Nethermind.Int256;
using Nethermind.Evm.Tracing;
using Nethermind.Core.Crypto;
using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Crypto;
using Nethermind.Arbitrum.Test.Infrastructure;

namespace Nethermind.Arbitrum.Test.Precompiles;

public class ArbRetryableTxTests
{
    private static readonly ILogManager Logger = LimboLogs.Instance;

    [Test]
    public void TicketCreated_EmitsEvent()
    {
        // Initialize ArbOS state
        (IWorldState worldState, _) = ArbOSInitialization.Create();

        string eventSignature = "TicketCreated(bytes32)";
        UInt256 ticketId = 123;
        Hash256 ticketIdHash = new(ticketId.ToBigEndian());
        Hash256[] expectedEventTopics = new Hash256[] { Keccak.Compute(eventSignature), ticketIdHash };
        LogEntry expectedLogEntry = new(ArbRetryableTx.Address, [], expectedEventTopics);

        ulong gasSupplied = GasCostOf.Log + GasCostOf.LogTopic * (ulong)expectedEventTopics.Length + 1;
        ArbitrumPrecompileExecutionContext context = new(
            Address.Zero, gasSupplied, NullTxTracer.Instance, false, worldState, new BlockExecutionContext(), 0
        );

        ArbRetryableTx.EmitTicketCreatedEvent(context, ticketIdHash);

        Assert.That(context.GasLeft, Is.EqualTo(1), "ArbRetryableTx.TicketCreated should consume the correct amount of gas");
        context.EventLogs.Should().BeEquivalentTo(new[] { expectedLogEntry });
    }

    [Test]
    public void RedeemScheduled_EmitsEvent()
    {
        // Initialize ArbOS state
        (IWorldState worldState, _) = ArbOSInitialization.Create();

        string eventSignature = "RedeemScheduled(bytes32,bytes32,uint64,uint64,address,uint256,uint256)";

        // Construct event topics
        UInt256 ticketId = 123;
        Hash256 ticketIdHash256 = new(ticketId.ToBigEndian());
        UInt256 retryTxHash = 456;
        Hash256 retryTxHash256 = new(retryTxHash.ToBigEndian());
        UInt256 sequenceNum = 1;
        Hash256 sequenceNumHash256 = new(sequenceNum.ToBigEndian());
        Hash256[] expectedEventTopics = new Hash256[] { Keccak.Compute(eventSignature), ticketIdHash256, retryTxHash256, sequenceNumHash256 };

        // Construct event data
        ulong donatedGas = 1;
        Address donor = Address.Zero;
        UInt256 maxRefund = 2;
        UInt256 submissionFeeRefund = 3;
        object[] data = new object[] { donatedGas, donor, maxRefund, submissionFeeRefund };
        byte[] expectedEventData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            new AbiSignature(string.Empty, new[] { AbiUInt.UInt64, AbiAddress.Instance, AbiUInt.UInt256, AbiUInt.UInt256 }),
            data);

        LogEntry expectedLogEntry = new(ArbRetryableTx.Address, expectedEventData, expectedEventTopics);

        ulong gasSupplied =
            GasCostOf.Log +
            GasCostOf.LogTopic * (ulong)expectedEventTopics.Length +
            GasCostOf.LogData * (ulong)expectedEventData.Length + 1;
        ArbitrumPrecompileExecutionContext context = new(
            Address.Zero, gasSupplied, NullTxTracer.Instance, false, worldState, new BlockExecutionContext(), 0
        );

        ArbRetryableTx.EmitRedeemScheduledEvent(
            context, ticketIdHash256, retryTxHash256, (ulong)sequenceNum, donatedGas, donor, maxRefund, submissionFeeRefund
        );

        Assert.That(context.GasLeft, Is.EqualTo(1), "ArbRetryableTx.RedeemScheduled should consume the correct amount of gas");
        context.EventLogs.Should().BeEquivalentTo(new[] { expectedLogEntry });
    }

    [Test]
    public void LifetimeExtended_EmitsEvent()
    {
        // Initialize ArbOS state
        (IWorldState worldState, _) = ArbOSInitialization.Create();

        string eventSignature = "LifetimeExtended(bytes32,uint256)";

        // topics
        UInt256 ticketId = 123;
        Hash256 ticketIdHash = new(ticketId.ToBigEndian());
        Hash256[] expectedEventTopics = new Hash256[] { Keccak.Compute(eventSignature), ticketIdHash };

        // data
        UInt256 newTimeout = 456;
        object[] data = { newTimeout };
        byte[] expectedEventData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            new AbiSignature(string.Empty, new[] { AbiUInt.UInt256 }),
            data);

        LogEntry expectedLogEntry = new(ArbRetryableTx.Address, expectedEventData, expectedEventTopics);

        ulong gasSupplied =
            GasCostOf.Log +
            GasCostOf.LogTopic * (ulong)expectedEventTopics.Length +
            GasCostOf.LogData * (ulong)expectedEventData.Length + 1;

        ArbitrumPrecompileExecutionContext context = new(
            Address.Zero, gasSupplied, NullTxTracer.Instance, false, worldState, new BlockExecutionContext(), 0
        );

        ArbRetryableTx.EmitLifetimeExtendedEvent(context, ticketIdHash, newTimeout);

        Assert.That(context.GasLeft, Is.EqualTo(1), "ArbRetryableTx.LifetimeExtended should consume the correct amount of gas");
        context.EventLogs.Should().BeEquivalentTo(new[] { expectedLogEntry });
    }

    [Test]
    public void Canceled_EmitsEvent()
    {
        // Initialize ArbOS state
        (IWorldState worldState, _) = ArbOSInitialization.Create();

        string eventSignature = "Canceled(bytes32)";

        // topics
        UInt256 ticketId = 123;
        Hash256 ticketIdHash = new(ticketId.ToBigEndian());
        Hash256[] expectedEventTopics = new Hash256[] { Keccak.Compute(eventSignature), ticketIdHash };
        LogEntry expectedLogEntry = new(ArbRetryableTx.Address, [], expectedEventTopics);

        ulong gasSupplied = GasCostOf.Log + GasCostOf.LogTopic * (ulong)expectedEventTopics.Length + 1;
        ArbitrumPrecompileExecutionContext context = new(
            Address.Zero, gasSupplied, NullTxTracer.Instance, false, worldState, new BlockExecutionContext(), 0
        );

        ArbRetryableTx.EmitCanceledEvent(context, ticketIdHash);

        Assert.That(context.GasLeft, Is.EqualTo(1), "ArbRetryableTx.Canceled should consume the correct amount of gas");
        context.EventLogs.Should().BeEquivalentTo(new[] { expectedLogEntry });
    }

    [Test]
    public void NoTicketWithIdSolidityError_ReturnsError()
    {
        // Initialize ArbOS state
        (IWorldState worldState, _) = ArbOSInitialization.Create();

        string eventSignature = "NoTicketWithID()";

        // no parameter, only the error signature
        byte[] expectedErrorData = Keccak.Compute(eventSignature).Bytes[0..4].ToArray();

        PrecompileSolidityError returnedError = ArbRetryableTx.NoTicketWithIdSolidityError();
        returnedError.ErrorData.Should().BeEquivalentTo(expectedErrorData);
    }

    [Test]
    public void NotCallableSolidityError_ReturnsError()
    {
        // Initialize ArbOS state
        (IWorldState worldState, _) = ArbOSInitialization.Create();

        string eventSignature = "NotCallable()";

        // no parameter, only the error signature
        byte[] expectedErrorData = Keccak.Compute(eventSignature).Bytes[0..4].ToArray();

        PrecompileSolidityError returnedError = ArbRetryableTx.NotCallableSolidityError();
        returnedError.ErrorData.Should().BeEquivalentTo(expectedErrorData);
    }

    [Test]
    public void Redeem_RetryableExists_ReturnsRetryTxHash()
    {
        // Initialize ArbOS state
        (IWorldState worldState, Block genesis) = ArbOSInitialization.Create();
        genesis.Header.Timestamp = 90;

        ulong gasSupplied = ulong.MaxValue;
        ulong gasLeft = gasSupplied;

        PrecompileTestContextBuilder testContext = new(worldState, gasSupplied);
        // Reset gas left as opening arbos state consumes gas
        testContext.WithArbosState().WithBlockExecutionContext(genesis).SetGasLeft(gasSupplied);

        Hash256 ticketIdHash = Hash256FromUlong(123);

        ulong calldataSize = 65;
        byte[] calldata = new byte[calldataSize];

        // retryable not expired
        ulong timeout = genesis.Header.Timestamp + 1;

        Retryable retryable = testContext.ArbosState.RetryableState.CreateRetryable(
            ticketIdHash, Address.Zero, Address.Zero, 0, Address.Zero, timeout, calldata
        );

        ulong retryableSizeBytesCost = 2 * ArbosStorage.StorageReadCost;
        gasLeft -= retryableSizeBytesCost;

        ulong byteCount = 6 * 32 + 32 + EvmPooledMemory.WordSize * (ulong)EvmPooledMemory.Div32Ceiling(calldataSize);
        ulong writeBytes = (ulong)EvmPooledMemory.Div32Ceiling(byteCount);
        ulong retryableCalldataCost = GasCostOf.SLoad * writeBytes;
        gasLeft -= retryableCalldataCost;

        ulong openRetryableCost = ArbosStorage.StorageReadCost;
        gasLeft -= openRetryableCost;

        ulong incrementNumTriesCost = ArbosStorage.StorageReadCost + ArbosStorage.StorageWriteCost;
        gasLeft -= incrementNumTriesCost;

        ulong nonce = retryable.NumTries.Get(); // 0
        UInt256 maxRefund = UInt256.MaxValue;

        ArbitrumRetryTx expectedRetryInnerTx = new(
            testContext.ChainId,
            nonce,
            retryable.From.Get(),
            testContext.BlockExecutionContext.Header.BaseFeePerGas,
            0, // fill in after
            retryable.To?.Get(),
            retryable.CallValue.Get(),
            retryable.Calldata.Get(),
            ticketIdHash,
            testContext.Caller,
            maxRefund,
            0
        );

        // 3 reads (from, to, callvalue) + 1 read (calldata size) + 3 reads (actual calldata)
        ulong arbitrumRetryTxCreationCost =
            3 * ArbosStorage.StorageReadCost +
            (1 + (ulong)EvmPooledMemory.Div32Ceiling(calldataSize)) * ArbosStorage.StorageReadCost;
        gasLeft -= arbitrumRetryTxCreationCost;

        // topics: event signature + 3 indexed parameters
        // data: 4 non-indexed static (32 bytes each) parameters
        ulong redeemScheduledEventGasCost =
            GasCostOf.Log +
            GasCostOf.LogTopic * (1 + 3) +
            GasCostOf.LogData * (4 * EvmPooledMemory.WordSize);
        ulong futureGasCosts = GasCostOf.DataCopy + GasCostOf.SLoadEip1884 + GasCostOf.SSet + redeemScheduledEventGasCost;
        ulong gasToDonate = gasLeft - futureGasCosts;

        // fix up the gas in the retry
        expectedRetryInnerTx.Gas = gasToDonate;

        var expectedTx = new ArbitrumTransaction<ArbitrumRetryTx>(expectedRetryInnerTx);
        Hash256 expectedTxHash = expectedTx.CalculateHash();

        LogEntry redeemScheduleEvent = EventsEncoder.BuildLogEntryFromEvent(
            ArbRetryableTx.RedeemScheduledEvent, ArbRetryableTx.Address, ticketIdHash,
            expectedTxHash, nonce, gasToDonate, testContext.Caller, maxRefund, 0
        );

        gasLeft -= redeemScheduledEventGasCost;
        gasLeft -= gasToDonate;

        ulong addToGasPoolCost = ArbosStorage.StorageReadCost + ArbosStorage.StorageWriteCost;
        gasLeft -= addToGasPoolCost;

        PrecompileTestContextBuilder newContext = new(worldState, gasSupplied);
        newContext.WithArbosState().WithBlockExecutionContext(genesis).WithTransactionProcessor();
        newContext.ArbosState.L2PricingState.GasBacklogStorage.Set(System.Math.Min(long.MaxValue, gasToDonate) + 1);
        newContext.SetGasLeft(gasSupplied); // reset gas left (opening arbos and setting backlog consumes gas)
        newContext.TxProcessor.CurrentRetryable = Hash256.Zero;

        // Redeem the retryable
        Hash256 returnedTxHash = ArbRetryableTx.Redeem(newContext, ticketIdHash);

        returnedTxHash.Should().BeEquivalentTo(expectedTxHash);
        newContext.EventLogs.Should().BeEquivalentTo(new[] { redeemScheduleEvent });
        newContext.GasLeft.Should().Be(gasLeft);
        newContext.GasLeft.Should().Be(GasCostOf.DataCopy); // just enough for returning the 32bytes result
        retryable.NumTries.Get().Should().Be(1);

        // Redeem execution used up all gas, give some gas for asserting
        newContext.SetGasLeft(gasSupplied);
        newContext.ArbosState.L2PricingState.GasBacklogStorage.Get().Should().Be(1);
    }

    [Test]
    public void Redeem_SelfModifyingRetryable_Throws()
    {
        (IWorldState worldState, _) = ArbOSInitialization.Create();
        PrecompileTestContextBuilder context = new(worldState, ulong.MaxValue);
        context.WithTransactionProcessor();
        Hash256 ticketIdHash = Hash256FromUlong(123);
        context.TxProcessor.CurrentRetryable = ticketIdHash;

        Action action = () => ArbRetryableTx.Redeem(context, ticketIdHash);
        InvalidOperationException expectedError = ArbRetryableTx.NewSelfModifyingRetryableException();
        action.Should().Throw<InvalidOperationException>().WithMessage(expectedError.Message);
    }

    [Test]
    public void Redeem_RetryableDoesNotExists_Throws()
    {
        (IWorldState worldState, Block genesis) = ArbOSInitialization.Create();
        PrecompileTestContextBuilder context = new(worldState, ulong.MaxValue);
        context.WithArbosState().WithTransactionProcessor().WithBlockExecutionContext(genesis);
        context.TxProcessor.CurrentRetryable = Hash256FromUlong(123);

        PrecompileSolidityError expectedError = ArbRetryableTx.NoTicketWithIdSolidityError();

        Action action = () => ArbRetryableTx.Redeem(context, Hash256.Zero);
        PrecompileSolidityError thrownException = action.Should().Throw<PrecompileSolidityError>().Which;
        thrownException.ErrorData.Should().BeEquivalentTo(expectedError.ErrorData);
    }

    [Test]
    public void GetLifetime_Always_ReturnsDefaultLifetime()
    {
        UInt256 lifetime = ArbRetryableTx.GetLifetime(null!);
        lifetime.Should().Be(Retryable.RetryableLifetimeSeconds);
    }

    [Test]
    public void GetTimeout_RetryableExists_ReturnsCalculatedTimeout()
    {
        (IWorldState worldState, Block genesis) = ArbOSInitialization.Create();
        genesis.Header.Timestamp = 90;
        PrecompileTestContextBuilder context = new(worldState, ulong.MaxValue);
        context.WithArbosState().WithBlockExecutionContext(genesis);

        Hash256 ticketId = Hash256FromUlong(123);
        ulong timeout = 100; // greater than current timestamp
        ulong timeoutWindowsLeft = 2;

        Retryable retryable = context.ArbosState.RetryableState.CreateRetryable(
            ticketId, Address.Zero, Address.Zero, 0, Address.Zero, timeout, []
        );
        retryable.TimeoutWindowsLeft.Set(timeoutWindowsLeft);

        UInt256 calculatedTimeout = ArbRetryableTx.GetTimeout(context, ticketId);
        calculatedTimeout.Should().Be(timeout + timeoutWindowsLeft * Retryable.RetryableLifetimeSeconds);
    }

    [Test]
    public void GetTimeout_RetryableExpired_Throws()
    {
        (IWorldState worldState, Block genesis) = ArbOSInitialization.Create();
        genesis.Header.Timestamp = 100;
        PrecompileTestContextBuilder context = new(worldState, ulong.MaxValue);
        context.WithArbosState().WithBlockExecutionContext(genesis);

        Hash256 ticketId = Hash256FromUlong(123);
        ulong timeout = 90; // inferior to current timestamp

        Retryable retryable = context.ArbosState.RetryableState.CreateRetryable(
            ticketId, Address.Zero, Address.Zero, 0, Address.Zero, timeout, []
        );

        PrecompileSolidityError expectedError = ArbRetryableTx.NoTicketWithIdSolidityError();

        Action action = () => ArbRetryableTx.GetTimeout(context, ticketId);
        PrecompileSolidityError thrownException = action.Should().Throw<PrecompileSolidityError>().Which;
        thrownException.ErrorData.Should().BeEquivalentTo(expectedError.ErrorData);
    }

    [Test]
    public void KeepAlive_RetryableExpiresBefore1Lifetime_ReturnsNewTimeout()
    {
        (IWorldState worldState, Block genesis) = ArbOSInitialization.Create();
        genesis.Header.Timestamp = 90;
        ulong gasSupplied = ulong.MaxValue;
        ulong gasLeft = gasSupplied;
        PrecompileTestContextBuilder context = new(worldState, gasSupplied);
        context.WithArbosState().WithBlockExecutionContext(genesis);
        context.SetGasLeft(gasSupplied); // reset gas left for gas computation and assertion

        Hash256 ticketId = Hash256FromUlong(123);
        ulong timeout = 100;
        ulong calldataLength = 33;
        byte[] calldata = new byte[calldataLength];

        Retryable retryable = context.ArbosState.RetryableState.CreateRetryable(
            ticketId, Address.Zero, Address.Zero, 0, Address.Zero, timeout, calldata
        );

        ulong retryableSizeBytesCost = 2 * ArbosStorage.StorageReadCost;
        gasLeft -= retryableSizeBytesCost;

        ulong byteCount = 6 * 32 + 32 + EvmPooledMemory.WordSize * (ulong)EvmPooledMemory.Div32Ceiling(calldataLength);
        ulong updateCost = (ulong)EvmPooledMemory.Div32Ceiling(byteCount) * GasCostOf.SSet / 100;
        gasLeft -= updateCost;

        ulong openRetryableCost = ArbosStorage.StorageReadCost;
        ulong calculateTimeoutCost = 2 * ArbosStorage.StorageReadCost;
        ulong timeoutQueuePushCost = ArbosStorage.StorageReadCost + 2 * ArbosStorage.StorageWriteCost;
        ulong timeoutWindowsLeftCost = ArbosStorage.StorageReadCost + ArbosStorage.StorageWriteCost;
        ulong keepAliveCost =
            openRetryableCost + calculateTimeoutCost + timeoutQueuePushCost +
            timeoutWindowsLeftCost + Retryable.RetryableReapPrice;
        gasLeft -= keepAliveCost;

        ulong lifetimeExtendedCost = ArbRetryableTx.LifetimeExtendedEventGasCost(ticketId, UInt256.Zero);
        gasLeft -= lifetimeExtendedCost;

        ulong expectedNewTimeout = timeout + Retryable.RetryableLifetimeSeconds;

        PrecompileTestContextBuilder newContext = new(worldState, gasSupplied);
        newContext.WithArbosState().WithBlockExecutionContext(genesis);
        newContext.SetGasLeft(gasSupplied); // reset gas left (opening arbos and setting backlog consumes gas)

        UInt256 returnedTimeout = ArbRetryableTx.KeepAlive(newContext, ticketId);

        returnedTimeout.Should().Be(expectedNewTimeout);
        newContext.GasLeft.Should().Be(gasLeft);
        newContext.ArbosState.RetryableState.TimeoutQueue.Peek().Should().Be(ticketId);
        retryable.TimeoutWindowsLeft.Get().Should().Be(1);

        LogEntry lifetimeExtendedEvent = EventsEncoder.BuildLogEntryFromEvent(
            ArbRetryableTx.LifetimeExtendedEvent, ArbRetryableTx.Address, ticketId, expectedNewTimeout
        );
        newContext.EventLogs.Should().BeEquivalentTo(new[] { lifetimeExtendedEvent });
    }

    [Test]
    public void KeepAlive_RetryableDoesNotExist_Throws()
    {
        (IWorldState worldState, Block genesis) = ArbOSInitialization.Create();
        genesis.Header.Timestamp = 90;

        PrecompileTestContextBuilder context = new(worldState, ulong.MaxValue);
        context.WithArbosState().WithBlockExecutionContext(genesis);

        PrecompileSolidityError expectedError = ArbRetryableTx.NoTicketWithIdSolidityError();

        Action action = () => ArbRetryableTx.KeepAlive(context, Hash256.Zero);
        PrecompileSolidityError thrownException = action.Should().Throw<PrecompileSolidityError>().Which;
        thrownException.ErrorData.Should().BeEquivalentTo(expectedError.ErrorData);
    }

    [Test]
    public void KeepAlive_RetryableExpiresAfter1Lifetime_Throws()
    {
        (IWorldState worldState, Block genesis) = ArbOSInitialization.Create();
        genesis.Header.Timestamp = 90;

        ulong gasSupplied = ulong.MaxValue;
        PrecompileTestContextBuilder context = new(worldState, gasSupplied);
        context.WithArbosState().WithBlockExecutionContext(genesis);

        Hash256 ticketId = Hash256FromUlong(123);
        ulong timeout = 100;

        Retryable retryable = context.ArbosState.RetryableState.CreateRetryable(
            ticketId, Address.Zero, Address.Zero, 0, Address.Zero, timeout, []
        );
        retryable.TimeoutWindowsLeft.Set(1);

        Action action = () => ArbRetryableTx.KeepAlive(context, ticketId);
        action.Should().Throw<Exception>().WithMessage("Timeout too far into the future");
    }

    [Test]
    public void GetBeneficiary_RetryableExists_ReturnsBeneficiary()
    {
        (IWorldState worldState, Block genesis) = ArbOSInitialization.Create();
        genesis.Header.Timestamp = 90;
        PrecompileTestContextBuilder context = new(worldState, ulong.MaxValue);
        context.WithArbosState().WithBlockExecutionContext(genesis);

        Hash256 ticketId = Hash256FromUlong(123);
        ulong timeout = 100;
        Address beneficiary = Address.SystemUser;
        Retryable retryable = context.ArbosState.RetryableState.CreateRetryable(
            ticketId, Address.Zero, Address.Zero, 0, beneficiary, timeout, []
        );

        Address returnedBeneficiary = ArbRetryableTx.GetBeneficiary(context, ticketId);
        returnedBeneficiary.Should().BeEquivalentTo(beneficiary);
    }

    [Test]
    public void GetBeneficiary_RetryableExpired_Throws()
    {
        (IWorldState worldState, Block genesis) = ArbOSInitialization.Create();
        genesis.Header.Timestamp = 100;
        PrecompileTestContextBuilder context = new(worldState, ulong.MaxValue);
        context.WithArbosState().WithBlockExecutionContext(genesis);

        Hash256 ticketId = Hash256FromUlong(123);
        ulong timeout = 90; // before current timestamp
        Retryable retryable = context.ArbosState.RetryableState.CreateRetryable(
            ticketId, Address.Zero, Address.Zero, 0, Address.Zero, timeout, []
        );

        PrecompileSolidityError expectedError = ArbRetryableTx.NoTicketWithIdSolidityError();

        Action action = () => ArbRetryableTx.GetBeneficiary(context, ticketId);
        PrecompileSolidityError thrownException = action.Should().Throw<PrecompileSolidityError>().Which;
        thrownException.ErrorData.Should().BeEquivalentTo(expectedError.ErrorData);
    }

    private static Hash256 Hash256FromUlong(ulong value) => new(new UInt256(value).ToBigEndian());
}
