using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test;
using Nethermind.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.State;

namespace Nethermind.Arbitrum.Test.Precompiles;

public class ArbRetryableTxTests
{
    [Test]
    public void TicketCreated_EmitsEvent()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        string eventSignature = "TicketCreated(bytes32)";
        UInt256 ticketId = 123;
        Hash256 ticketIdHash = new(ticketId.ToBigEndian());
        Hash256[] expectedEventTopics = [Keccak.Compute(eventSignature), ticketIdHash];
        LogEntry expectedLogEntry = new(ArbRetryableTx.Address, [], expectedEventTopics);

        ulong gasSupplied = GasCostOf.Log + GasCostOf.LogTopic * (ulong)expectedEventTopics.Length + 1;

        PrecompileTestContextBuilder context = new(worldState, gasSupplied);
        ArbRetryableTx.EmitTicketCreatedEvent(context, ticketIdHash);

        Assert.That(context.GasLeft, Is.EqualTo(1), "ArbRetryableTx.TicketCreated should consume the correct amount of gas");
        context.EventLogs.Should().BeEquivalentTo(new[] { expectedLogEntry });
    }

    [Test]
    public void RedeemScheduled_EmitsEvent()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

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
        object[] data = [donatedGas, donor, maxRefund, submissionFeeRefund];
        byte[] expectedEventData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            new AbiSignature(string.Empty, [AbiUInt.UInt64, AbiAddress.Instance, AbiUInt.UInt256, AbiUInt.UInt256]),
            data);

        LogEntry expectedLogEntry = new(ArbRetryableTx.Address, expectedEventData, expectedEventTopics);

        ulong gasSupplied =
            GasCostOf.Log +
            GasCostOf.LogTopic * (ulong)expectedEventTopics.Length +
            GasCostOf.LogData * (ulong)expectedEventData.Length + 1;

        PrecompileTestContextBuilder context = new(worldState, gasSupplied);
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
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        string eventSignature = "LifetimeExtended(bytes32,uint256)";

        // topics
        UInt256 ticketId = 123;
        Hash256 ticketIdHash = new(ticketId.ToBigEndian());
        Hash256[] expectedEventTopics = [Keccak.Compute(eventSignature), ticketIdHash];

        // data
        UInt256 newTimeout = 456;
        object[] data = [newTimeout];
        byte[] expectedEventData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            new AbiSignature(string.Empty, [AbiUInt.UInt256]),
            data);

        LogEntry expectedLogEntry = new(ArbRetryableTx.Address, expectedEventData, expectedEventTopics);

        ulong gasSupplied =
            GasCostOf.Log +
            GasCostOf.LogTopic * (ulong)expectedEventTopics.Length +
            GasCostOf.LogData * (ulong)expectedEventData.Length + 1;

        PrecompileTestContextBuilder context = new(worldState, gasSupplied);
        ArbRetryableTx.EmitLifetimeExtendedEvent(context, ticketIdHash, newTimeout);

        Assert.That(context.GasLeft, Is.EqualTo(1), "ArbRetryableTx.LifetimeExtended should consume the correct amount of gas");
        context.EventLogs.Should().BeEquivalentTo(new[] { expectedLogEntry });
    }

    [Test]
    public void Canceled_EmitsEvent()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        string eventSignature = "Canceled(bytes32)";

        // topics
        UInt256 ticketId = 123;
        Hash256 ticketIdHash = new(ticketId.ToBigEndian());
        Hash256[] expectedEventTopics = [Keccak.Compute(eventSignature), ticketIdHash];
        LogEntry expectedLogEntry = new(ArbRetryableTx.Address, [], expectedEventTopics);

        ulong gasSupplied = GasCostOf.Log + GasCostOf.LogTopic * (ulong)expectedEventTopics.Length + 1;

        PrecompileTestContextBuilder context = new(worldState, gasSupplied);
        ArbRetryableTx.EmitCanceledEvent(context, ticketIdHash);

        Assert.That(context.GasLeft, Is.EqualTo(1), "ArbRetryableTx.Canceled should consume the correct amount of gas");
        context.EventLogs.Should().BeEquivalentTo(new[] { expectedLogEntry });
    }

    [Test]
    public void NoTicketWithIdSolidityError_ReturnsError()
    {
        string eventSignature = "NoTicketWithID()";
        // no parameter, only the error signature
        byte[] expectedErrorData = Keccak.Compute(eventSignature).Bytes[0..4].ToArray();

        ArbitrumPrecompileException returnedError = ArbRetryableTx.NoTicketWithIdSolidityError();
        returnedError.Output.Should().BeEquivalentTo(expectedErrorData);
        returnedError.Type.Should().Be(ArbitrumPrecompileException.PrecompileExceptionType.Solidity);
    }

    [Test]
    public void NotCallableSolidityError_ReturnsError()
    {
        string eventSignature = "NotCallable()";
        // no parameter, only the error signature
        byte[] expectedErrorData = Keccak.Compute(eventSignature).Bytes[0..4].ToArray();

        ArbitrumPrecompileException returnedError = ArbRetryableTx.NotCallableSolidityError();
        returnedError.Output.Should().BeEquivalentTo(expectedErrorData);
        returnedError.Type.Should().Be(ArbitrumPrecompileException.PrecompileExceptionType.Solidity);
    }

    [Test]
    public void Redeem_RetryableExists_ReturnsCreatedRetryTxHash()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        var genesis = ArbOSInitialization.Create(worldState);
        genesis.Header.Timestamp = 100;

        ulong gasSupplied = ulong.MaxValue;
        PrecompileTestContextBuilder setupContext = new(worldState, gasSupplied);
        setupContext.WithArbosState().WithBlockExecutionContext(genesis.Header);

        Hash256 ticketIdHash = Hash256FromUlong(123);

        ulong calldataSize = 65;
        byte[] calldata = new byte[calldataSize];
        ulong timeout = genesis.Header.Timestamp + 1; // retryable not expired

        Retryable retryable = setupContext.ArbosState.RetryableState.CreateRetryable(
            ticketIdHash, Address.Zero, Address.Zero, 0, Address.Zero, timeout, calldata
        );

        ulong nonce = retryable.NumTries.Get(); // 0
        UInt256 maxRefund = UInt256.MaxValue;

        ArbitrumRetryTransaction expectedRetryTx = new ArbitrumRetryTransaction
        {
            ChainId = setupContext.ChainId,
            Nonce = nonce,
            SenderAddress = retryable.From.Get(),
            DecodedMaxFeePerGas = setupContext.BlockExecutionContext.Header.BaseFeePerGas,
            GasFeeCap = setupContext.BlockExecutionContext.Header.BaseFeePerGas,
            Gas = 0,
            GasLimit = 0,
            To = retryable.To?.Get(),
            Value = retryable.CallValue.Get(),
            Data = retryable.Calldata.Get(),
            TicketId = ticketIdHash,
            RefundTo = setupContext.Caller,
            MaxRefund = maxRefund,
            SubmissionFeeRefund = 0
        };

        ulong gasLeft = ComputeRedeemCost(out ulong gasToDonate, gasSupplied, calldataSize);

        expectedRetryTx.Gas = gasToDonate;
        expectedRetryTx.GasLimit = (long)gasToDonate;

        Hash256 expectedTxHash = expectedRetryTx.CalculateHash();

        LogEntry redeemScheduleEvent = EventsEncoder.BuildLogEntryFromEvent(
            ArbRetryableTx.RedeemScheduledEvent, ArbRetryableTx.Address, ticketIdHash,
            expectedTxHash, nonce, gasToDonate, setupContext.Caller, maxRefund, 0
        );

        PrecompileTestContextBuilder newContext = new(worldState, gasSupplied)
        {
            CurrentRetryable = Hash256.Zero
        };
        newContext.WithArbosState().WithBlockExecutionContext(genesis.Header);
        newContext.ArbosState.L2PricingState.GasBacklogStorage.Set(System.Math.Min(long.MaxValue, gasToDonate) + 1);
        newContext.ResetGasLeft(); // for gas assertion check (opening arbos and setting backlog consumes gas)

        // Redeem the retryable
        Hash256 returnedTxHash = ArbRetryableTx.Redeem(newContext, ticketIdHash);

        returnedTxHash.Should().BeEquivalentTo(expectedTxHash);
        newContext.EventLogs.Should().BeEquivalentTo(new[] { redeemScheduleEvent });
        newContext.GasLeft.Should().Be(gasLeft);
        newContext.GasLeft.Should().Be(GasCostOf.DataCopy); // just enough for returning the 32bytes result
        retryable.NumTries.Get().Should().Be(1);

        // Redeem execution used up all gas, give some gas for asserting
        newContext.ResetGasLeft();
        newContext.ArbosState.L2PricingState.GasBacklogStorage.Get().Should().Be(1);
    }

    public static ulong ComputeRedeemCost(out ulong gasToDonate, ulong gasSupplied, ulong calldataSize)
    {
        ulong gasLeft = gasSupplied;

        ulong retryableSizeBytesCost = 2 * ArbosStorage.StorageReadCost;
        gasLeft -= retryableSizeBytesCost;

        ulong byteCount = 6 * 32 + 32 + EvmPooledMemory.WordSize * Math.Utils.Div32Ceiling(calldataSize);
        ulong writeBytes = Math.Utils.Div32Ceiling(byteCount);
        ulong retryableCalldataCost = GasCostOf.SLoad * writeBytes;
        gasLeft -= retryableCalldataCost;

        ulong openRetryableCost = ArbosStorage.StorageReadCost;
        gasLeft -= openRetryableCost;

        ulong incrementNumTriesCost = ArbosStorage.StorageReadCost + ArbosStorage.StorageWriteCost;
        gasLeft -= incrementNumTriesCost;

        // 3 reads (from, to, callvalue) + 1 read (calldata size) + 3 reads (actual calldata)
        ulong arbitrumRetryTxCreationCost =
            3 * ArbosStorage.StorageReadCost +
            (1 + Math.Utils.Div32Ceiling(calldataSize)) * ArbosStorage.StorageReadCost;
        gasLeft -= arbitrumRetryTxCreationCost;

        // topics: event signature + 3 indexed parameters
        // data: 4 non-indexed static (32 bytes each) parameters
        ulong redeemScheduledEventGasCost =
            GasCostOf.Log +
            GasCostOf.LogTopic * (1 + 3) +
            GasCostOf.LogData * (4 * EvmPooledMemory.WordSize);
        ulong futureGasCosts = GasCostOf.DataCopy + GasCostOf.SLoadEip1884 + GasCostOf.SSet + redeemScheduledEventGasCost;
        gasToDonate = gasLeft - futureGasCosts;

        gasLeft -= redeemScheduledEventGasCost;
        gasLeft -= gasToDonate;

        ulong addToGasPoolCost = ArbosStorage.StorageReadCost + ArbosStorage.StorageWriteCost;
        gasLeft -= addToGasPoolCost;

        return gasLeft;
    }

    [Test]
    public void Redeem_SelfModifyingRetryable_Throws()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        Hash256 ticketIdHash = Hash256FromUlong(123);
        PrecompileTestContextBuilder context = new(worldState, ulong.MaxValue)
        {
            CurrentRetryable = ticketIdHash
        };

        Action action = () => ArbRetryableTx.Redeem(context, ticketIdHash);
        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbRetryableTx.SelfModifyingRetryableException();
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void Redeem_RetryableDoesNotExists_Throws()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new(worldState, ulong.MaxValue)
        {
            CurrentRetryable = Hash256FromUlong(123)
        };
        context.WithArbosState().WithBlockExecutionContext(genesis.Header);

        Action action = () => ArbRetryableTx.Redeem(context, Hash256.Zero);
        ArbitrumPrecompileException thrownException = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbRetryableTx.NoTicketWithIdSolidityError();
        thrownException.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
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
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);
        genesis.Header.Timestamp = 100;
        PrecompileTestContextBuilder context = new(worldState, ulong.MaxValue);
        context.WithArbosState().WithBlockExecutionContext(genesis.Header);

        Hash256 ticketId = Hash256FromUlong(123);
        ulong timeout = genesis.Header.Timestamp + 1; // greater than current timestamp
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
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);
        genesis.Header.Timestamp = 100;
        PrecompileTestContextBuilder context = new(worldState, ulong.MaxValue);
        context.WithArbosState().WithBlockExecutionContext(genesis.Header);

        Hash256 ticketId = Hash256FromUlong(123);
        ulong timeout = genesis.Header.Timestamp - 1; // lower than current timestamp

        Retryable retryable = context.ArbosState.RetryableState.CreateRetryable(
            ticketId, Address.Zero, Address.Zero, 0, Address.Zero, timeout, []
        );

        Action action = () => ArbRetryableTx.GetTimeout(context, ticketId);
        ArbitrumPrecompileException thrownException = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbRetryableTx.NoTicketWithIdSolidityError();
        thrownException.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void KeepAlive_RetryableExpiresBefore1Lifetime_ReturnsNewTimeout()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        var genesis = ArbOSInitialization.Create(worldState);

        genesis.Header.Timestamp = 100;
        ulong gasSupplied = ulong.MaxValue;
        ulong gasLeft = gasSupplied;
        PrecompileTestContextBuilder setupContext = new(worldState, gasSupplied);
        setupContext.WithArbosState();

        Hash256 ticketId = Hash256FromUlong(123);
        ulong timeout = genesis.Header.Timestamp + 1; // greater than current timestamp
        ulong calldataLength = 33;
        byte[] calldata = new byte[calldataLength];

        Retryable retryable = setupContext.ArbosState.RetryableState.CreateRetryable(
            ticketId, Address.Zero, Address.Zero, 0, Address.Zero, timeout, calldata
        );

        ulong retryableSizeBytesCost = 2 * ArbosStorage.StorageReadCost;
        gasLeft -= retryableSizeBytesCost;

        ulong byteCount = 6 * 32 + 32 + EvmPooledMemory.WordSize * Math.Utils.Div32Ceiling(calldataLength);
        ulong updateCost = Math.Utils.Div32Ceiling(byteCount) * GasCostOf.SSet / 100;
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
        newContext.WithArbosState().WithBlockExecutionContext(genesis.Header);
        newContext.ResetGasLeft(); // for gas assertion check (opening arbos and setting backlog consumes gas)

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
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);
        genesis.Header.Timestamp = 100;

        PrecompileTestContextBuilder context = new(worldState, ulong.MaxValue);
        context.WithArbosState().WithBlockExecutionContext(genesis.Header);

        Action action = () => ArbRetryableTx.KeepAlive(context, Hash256.Zero);

        ArbitrumPrecompileException thrownException = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbRetryableTx.NoTicketWithIdSolidityError();
        thrownException.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void KeepAlive_RetryableExpiresAfter1Lifetime_Throws()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);
        genesis.Header.Timestamp = 100;

        ulong gasSupplied = ulong.MaxValue;
        PrecompileTestContextBuilder context = new(worldState, gasSupplied);
        context.WithArbosState().WithBlockExecutionContext(genesis.Header);

        Hash256 ticketId = Hash256FromUlong(123);
        ulong timeout = genesis.Header.Timestamp + 1; // greater than current timestamp

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
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);
        genesis.Header.Timestamp = 100;
        PrecompileTestContextBuilder context = new(worldState, ulong.MaxValue);
        context.WithArbosState().WithBlockExecutionContext(genesis.Header);

        Hash256 ticketId = Hash256FromUlong(123);
        ulong timeout = genesis.Header.Timestamp + 1; // greater than current timestamp
        Address beneficiary = Address.SystemUser;
        context.ArbosState.RetryableState.CreateRetryable(
            ticketId, Address.Zero, Address.Zero, 0, beneficiary, timeout, []
        );

        Address returnedBeneficiary = ArbRetryableTx.GetBeneficiary(context, ticketId);
        returnedBeneficiary.Should().BeEquivalentTo(beneficiary);
    }

    [Test]
    public void GetBeneficiary_RetryableExpired_Throws()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);
        genesis.Header.Timestamp = 100;
        PrecompileTestContextBuilder context = new(worldState, ulong.MaxValue);
        context.WithArbosState().WithBlockExecutionContext(genesis.Header);

        Hash256 ticketId = Hash256FromUlong(123);
        ulong timeout = genesis.Header.Timestamp - 1; // lower than current timestamp
        context.ArbosState.RetryableState.CreateRetryable(
            ticketId, Address.Zero, Address.Zero, 0, Address.Zero, timeout, []
        );

        Action action = () => ArbRetryableTx.GetBeneficiary(context, ticketId);

        ArbitrumPrecompileException thrownException = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbRetryableTx.NoTicketWithIdSolidityError();
        thrownException.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void Cancel_RetryableExists_DeletesIt()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);
        genesis.Header.Timestamp = 100;
        ulong gasSupplied = ulong.MaxValue;
        ulong gasLeft = gasSupplied;
        PrecompileTestContextBuilder setupContext = new(worldState, gasSupplied);
        setupContext.WithArbosState().WithReleaseSpec();

        Hash256 ticketId = Hash256FromUlong(123);
        ulong timeout = genesis.Header.Timestamp + 1; // greater than current timestamp
        Address beneficiary = new(Hash256FromUlong(1));
        ulong calldataSize = 33;
        byte[] calldata = new byte[calldataSize];
        setupContext.ArbosState.RetryableState.CreateRetryable(
            ticketId, new(Hash256FromUlong(3)), new(Hash256FromUlong(4)), 30, beneficiary, timeout, calldata
        );

        Address escrowAddress = ArbitrumTransactionProcessor.GetRetryableEscrowAddress(ticketId);
        ulong amountToTransfer = 2000;
        bool transfered = worldState.AddToBalanceAndCreateIfNotExists(escrowAddress, amountToTransfer, setupContext.ReleaseSpec);
        transfered.Should().BeTrue();

        ulong getBeneficiaryCost = 2 * ArbosStorage.StorageReadCost;
        gasLeft -= getBeneficiaryCost;

        ulong clearCalldataCost =
            ArbosStorage.StorageReadCost +
            (1 + Math.Utils.Div32Ceiling(calldataSize)) * ArbosStorage.StorageWriteZeroCost;
        ulong clearRetryableCost = 7 * ArbosStorage.StorageWriteZeroCost + clearCalldataCost;
        ulong deletedRetryableCost = 2 * ArbosStorage.StorageReadCost + clearRetryableCost;
        gasLeft -= deletedRetryableCost;

        LogEntry canceledEventLog = EventsEncoder.BuildLogEntryFromEvent(
            ArbRetryableTx.CanceledEvent, ArbRetryableTx.Address, ticketId
        );
        ulong eventCost = EventsEncoder.EventCost(canceledEventLog);
        gasLeft -= eventCost;

        PrecompileTestContextBuilder newContext = new(worldState, gasSupplied)
        {
            CurrentRetryable = Hash256.Zero
        };
        newContext
            .WithArbosState()
            .WithBlockExecutionContext(genesis.Header)
            .WithReleaseSpec()
            .WithCaller(beneficiary)
            .ResetGasLeft(); // for gas assertion check (initializing context consumes gas)
        ArbRetryableTx.Cancel(newContext, ticketId);

        newContext.GasLeft.Should().Be(gasLeft);
        newContext.EventLogs.Should().BeEquivalentTo(new[] { canceledEventLog });
        worldState.GetBalance(escrowAddress).Should().Be(UInt256.Zero);
        worldState.GetBalance(beneficiary).Should().Be(amountToTransfer);

        Retryable deletedRetryable = newContext.ArbosState.RetryableState.GetRetryable(ticketId);
        deletedRetryable.NumTries.Get().Should().Be(0);
        deletedRetryable.From.Get().Should().Be(Address.Zero);
        deletedRetryable.To!.Get().Should().Be(Address.Zero);
        deletedRetryable.CallValue.Get().Should().Be(UInt256.Zero);
        deletedRetryable.Beneficiary.Get().Should().Be(Address.Zero);
        deletedRetryable.Timeout.Get().Should().Be(0);
        deletedRetryable.TimeoutWindowsLeft.Get().Should().Be(0);
        deletedRetryable.Calldata.Get().Should().BeEmpty();
    }

    [Test]
    public void Cancel_SelfModifyingRetryable_Throws()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        Hash256 ticketId = Hash256FromUlong(123);
        PrecompileTestContextBuilder context = new(worldState, ulong.MaxValue)
        {
            CurrentRetryable = ticketId
        };

        Action action = () => ArbRetryableTx.Cancel(context, ticketId);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbRetryableTx.SelfModifyingRetryableException();
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void Cancel_NotBeneficiary_Throws()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);
        genesis.Header.Timestamp = 100;
        PrecompileTestContextBuilder context = new(worldState, ulong.MaxValue)
        {
            CurrentRetryable = Hash256.Zero
        };
        context
            .WithArbosState()
            .WithBlockExecutionContext(genesis.Header)
            .WithCaller(Address.Zero);

        Hash256 ticketId = Hash256FromUlong(123);
        ulong timeout = genesis.Header.Timestamp + 1; // greater than current timestamp
        Address beneficiary = new(Hash256FromUlong(1));
        context.ArbosState.RetryableState.CreateRetryable(
            ticketId, Address.Zero, Address.Zero, 0, beneficiary, timeout, []
        );

        Action action = () => ArbRetryableTx.Cancel(context, ticketId);
        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException("Only the beneficiary may cancel a retryable");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void GetCurrentRedeemer_RedeemTransaction_ReturnsRedeemer()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        Address redeemer = new(Hash256FromUlong(123));
        PrecompileTestContextBuilder context = new(worldState, ulong.MaxValue)
        {
            CurrentRefundTo = redeemer
        };

        Address returnedRedeemer = ArbRetryableTx.GetCurrentRedeemer(context);
        returnedRedeemer.Should().BeEquivalentTo(redeemer);
    }

    [Test]
    public void GetCurrentRedeemer_NotARedeemTransaction_ReturnsZeroAddress()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, ulong.MaxValue);

        Address returnedRedeemer = ArbRetryableTx.GetCurrentRedeemer(context);
        returnedRedeemer.Should().BeEquivalentTo(Address.Zero);
    }

    [Test]
    public void SubmitRetryable_Always_Throws()
    {
        Action action = () => ArbRetryableTx.SubmitRetryable(
            null!, null!, 0, 0, 0, 0, 0, 0, null!, null!, null!, []
        );
        ArbitrumPrecompileException thrownException = action.Should().Throw<ArbitrumPrecompileException>().Which;

        ArbitrumPrecompileException expected = ArbRetryableTx.NotCallableSolidityError();
        thrownException.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    public static Hash256 Hash256FromUlong(ulong value) => new(new UInt256(value).ToBigEndian());
}
