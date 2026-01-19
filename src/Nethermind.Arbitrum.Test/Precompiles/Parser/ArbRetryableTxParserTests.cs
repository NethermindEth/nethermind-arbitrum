using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Logging;
using Nethermind.Core;
using Nethermind.Int256;
using Nethermind.Core.Extensions;
using Nethermind.Core.Crypto;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Crypto;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.Arbitrum.Precompiles.Exceptions;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

public class ArbRetryableTxParserTests
{
    private static readonly uint _cancelId = PrecompileHelper.GetMethodId("cancel(bytes32)");
    private static readonly uint _getBeneficiaryId = PrecompileHelper.GetMethodId("getBeneficiary(bytes32)");
    private static readonly uint _getCurrentRedeemerId = PrecompileHelper.GetMethodId("getCurrentRedeemer()");
    private static readonly uint _getLifetimeId = PrecompileHelper.GetMethodId("getLifetime()");
    private static readonly uint _getTimeoutId = PrecompileHelper.GetMethodId("getTimeout(bytes32)");
    private static readonly uint _keepaliveId = PrecompileHelper.GetMethodId("keepalive(bytes32)");

    private static readonly uint _redeemId = PrecompileHelper.GetMethodId("redeem(bytes32)");
    private static readonly uint _submitRetryableId = PrecompileHelper.GetMethodId("submitRetryable(bytes32,uint256,uint256,uint256,uint256,uint64,uint256,address,address,address,bytes)");
    private static readonly ILogManager Logger = LimboLogs.Instance;

    [Test]
    public void ParsesCancel_RetryableExists_ReturnsEmptyOutput()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);
        genesis.Header.Timestamp = 100;

        ulong gasSupplied = ulong.MaxValue;
        PrecompileTestContextBuilder setupContext = new(worldState, gasSupplied);
        setupContext.WithArbosState().WithReleaseSpec();

        Hash256 ticketId = ArbRetryableTxTests.Hash256FromUlong(123);
        ulong timeout = genesis.Header.Timestamp + 1; // retryable not expired
        Address beneficiary = new(ArbRetryableTxTests.Hash256FromUlong(1));

        ulong calldataSize = 33;
        byte[] calldata = new byte[calldataSize];

        setupContext.ArbosState.RetryableState.CreateRetryable(
            ticketId, new(ArbRetryableTxTests.Hash256FromUlong(3)),
            new(ArbRetryableTxTests.Hash256FromUlong(4)), 30, beneficiary, timeout, calldata
        );

        PrecompileTestContextBuilder newContext = new(worldState, gasSupplied)
        {
            CurrentRetryable = Hash256.Zero
        };
        newContext
            .WithArbosState()
            .WithBlockExecutionContext(genesis.Header)
            .WithReleaseSpec()
            .WithCaller(beneficiary);

        bool exists = ArbRetryableTxParser.PrecompileImplementation.TryGetValue(_cancelId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbRetryableTxParser.PrecompileFunctionDescription[_cancelId].AbiFunctionDescription;
        byte[] inputData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            ticketId
        );

        byte[] result = implementation!(newContext, inputData);

        result.Should().BeEmpty();
    }

    [Test]
    public void ParsesCancel_WithInvalidInputData_Throws()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        // too small ticketId parameter
        byte[] invalidInputData = new byte[Keccak.Size - 1];

        PrecompileTestContextBuilder context = new(worldState, 0);

        bool exists = ArbRetryableTxParser.PrecompileImplementation.TryGetValue(_cancelId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        Action action = () => implementation!(context, invalidInputData);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateRevertException("", true);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesGetBeneficiary_RetryableExists_ReturnsBeneficiary()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);
        genesis.Header.Timestamp = 100;
        PrecompileTestContextBuilder context = new(worldState, ulong.MaxValue);
        context.WithArbosState().WithBlockExecutionContext(genesis.Header);

        Hash256 ticketId = ArbRetryableTxTests.Hash256FromUlong(123);
        ulong timeout = genesis.Header.Timestamp + 1;
        Address beneficiary = Address.SystemUser;
        context.ArbosState.RetryableState.CreateRetryable(
            ticketId, Address.Zero, Address.Zero, 0, beneficiary, timeout, []
        );

        bool exists = ArbRetryableTxParser.PrecompileImplementation.TryGetValue(_getBeneficiaryId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbRetryableTxParser.PrecompileFunctionDescription[_getBeneficiaryId].AbiFunctionDescription;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            ticketId
        );

        byte[] result = implementation!(context, calldata);

        byte[] expectedAbiEncodedAddress = Address.SystemUser.Bytes.PadLeft(Hash256.Size);

        result.Should().BeEquivalentTo(expectedAbiEncodedAddress);
    }

    [Test]
    public void ParsesGetBeneficiary_WithInvalidInputData_ThrowsRevertException()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, 0);

        bool exists = ArbRetryableTxParser.PrecompileImplementation.TryGetValue(_getBeneficiaryId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        // too small ticketId parameter
        byte[] invalidInputData = new byte[Keccak.Size - 1];
        Action action = () => implementation!(context, invalidInputData);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateRevertException("", true);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesGetCurrentRedeemer_Always_ReturnsRedeemerOrZeroAddress()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        Address redeemer = new(ArbRetryableTxTests.Hash256FromUlong(123));
        PrecompileTestContextBuilder context = new(worldState, ulong.MaxValue)
        {
            CurrentRefundTo = redeemer
        };

        bool exists = ArbRetryableTxParser.PrecompileImplementation.TryGetValue(_getCurrentRedeemerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(context, []);

        byte[] expectedAbiEncodedAddress = redeemer.Bytes.PadLeft(Hash256.Size);

        result.Should().BeEquivalentTo(expectedAbiEncodedAddress);
    }

    [Test]
    public void ParsesGetLifetime_Always_ReturnsDefaultLifetime()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, 0);

        bool exists = ArbRetryableTxParser.PrecompileImplementation.TryGetValue(_getLifetimeId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(context, []);

        UInt256 expectedResult = new(Retryable.RetryableLifetimeSeconds);
        result.Should().BeEquivalentTo(expectedResult.ToBigEndian());
    }

    [Test]
    public void ParsesGetTimeout_RetryableExists_ReturnsCalculatedTimeout()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);
        genesis.Header.Timestamp = 100;

        PrecompileTestContextBuilder context = new(worldState, ulong.MaxValue);
        context.WithArbosState().WithBlockExecutionContext(genesis.Header);

        Hash256 ticketId = ArbRetryableTxTests.Hash256FromUlong(123);
        ulong timeout = genesis.Header.Timestamp + 1; // retryable not expired
        Retryable retryable = context.ArbosState.RetryableState.CreateRetryable(
            ticketId, Address.Zero, Address.Zero, 0, Address.Zero, timeout, []
        );

        ulong timeoutWindowsLeft = 2;
        retryable.TimeoutWindowsLeft.Set(timeoutWindowsLeft);

        bool exists = ArbRetryableTxParser.PrecompileImplementation.TryGetValue(_getTimeoutId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbRetryableTxParser.PrecompileFunctionDescription[_getTimeoutId].AbiFunctionDescription;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            ticketId
        );

        byte[] result = implementation!(context, calldata);

        UInt256 expectedCalculatedTimeout = new(timeout + timeoutWindowsLeft * Retryable.RetryableLifetimeSeconds);
        result.Should().BeEquivalentTo(expectedCalculatedTimeout.ToBigEndian());
    }

    [Test]
    public void ParsesGetTimeout_WithInvalidInputData_ThrowsRevertException()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        // too small ticketId parameter
        byte[] invalidInputData = new byte[Keccak.Size - 1];

        PrecompileTestContextBuilder context = new(worldState, 0);

        bool exists = ArbRetryableTxParser.PrecompileImplementation.TryGetValue(_getTimeoutId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        Action action = () => implementation!(context, invalidInputData);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateRevertException("", true);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesKeepAlive_RetryableExpiresBefore1Lifetime_ReturnsNewTimeout()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);
        genesis.Header.Timestamp = 100;

        ulong gasSupplied = ulong.MaxValue;
        PrecompileTestContextBuilder setupContext = new(worldState, gasSupplied);
        setupContext.WithArbosState();

        Hash256 ticketId = ArbRetryableTxTests.Hash256FromUlong(123);
        ulong timeout = genesis.Header.Timestamp + 1; // retryable not expired
        ulong calldataLength = 33;
        byte[] calldata = new byte[calldataLength];

        setupContext.ArbosState.RetryableState.CreateRetryable(
            ticketId, Address.Zero, Address.Zero, 0, Address.Zero, timeout, calldata
        );

        PrecompileTestContextBuilder newContext = new(worldState, gasSupplied);
        newContext.WithArbosState().WithBlockExecutionContext(genesis.Header);

        bool exists = ArbRetryableTxParser.PrecompileImplementation.TryGetValue(_keepaliveId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbRetryableTxParser.PrecompileFunctionDescription[_keepaliveId].AbiFunctionDescription;
        byte[] inputdata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            ticketId
        );

        byte[] result = implementation!(newContext, inputdata);

        UInt256 expectedNewTimeout = timeout + Retryable.RetryableLifetimeSeconds;
        result.Should().BeEquivalentTo(expectedNewTimeout.ToBigEndian());
    }

    [Test]
    public void ParsesKeepAlive_WithInvalidInputData_ThrowsRevertException()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        // too small ticketId parameter
        byte[] invalidInputData = new byte[Keccak.Size - 1];

        PrecompileTestContextBuilder context = new(worldState, 0);

        bool exists = ArbRetryableTxParser.PrecompileImplementation.TryGetValue(_keepaliveId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        Action action = () => implementation!(context, invalidInputData);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateRevertException("", true);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesRedeem_ValidInputData_ReturnsCreatedRetryTxHash()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);
        genesis.Header.Timestamp = 100;

        Hash256 ticketIdHash = ArbRetryableTxTests.Hash256FromUlong(123);
        ulong gasSupplied = ulong.MaxValue;

        PrecompileTestContextBuilder setupContext = new(worldState, gasSupplied);
        setupContext.WithArbosState().WithBlockExecutionContext(genesis.Header);

        ulong calldataSize = 65;
        byte[] calldata = new byte[calldataSize];
        ulong timeout = genesis.Header.Timestamp + 1; // retryable not expired
        Retryable retryable = setupContext.ArbosState.RetryableState.CreateRetryable(
            ticketIdHash, Address.Zero, Address.Zero, 0, Address.Zero, timeout, calldata
        );

        ArbRetryableTxTests.ComputeRedeemCost(out ulong gasToDonate, gasSupplied, calldataSize);
        ulong nonce = retryable.NumTries.Get(); // 0
        UInt256 maxRefund = UInt256.MaxValue;

        ArbitrumRetryTransaction expectedRetryTx = new ArbitrumRetryTransaction
        {
            ChainId = setupContext.ChainId,
            Nonce = nonce,
            SenderAddress = retryable.From.Get(),
            DecodedMaxFeePerGas = setupContext.BlockExecutionContext.Header.BaseFeePerGas,
            GasFeeCap = setupContext.BlockExecutionContext.Header.BaseFeePerGas,
            Gas = gasToDonate,
            GasLimit = (long)gasToDonate,
            To = retryable.To?.Get(),
            Value = retryable.CallValue.Get(),
            Data = retryable.Calldata.Get(),
            TicketId = ticketIdHash,
            RefundTo = setupContext.Caller!,
            MaxRefund = maxRefund,
            SubmissionFeeRefund = 0
        };

        Hash256 expectedTxHash = expectedRetryTx.CalculateHash();

        // Setup context
        PrecompileTestContextBuilder newContext = new(worldState, gasSupplied)
        {
            CurrentRetryable = Hash256.Zero
        };
        newContext.WithArbosState().WithBlockExecutionContext(genesis.Header);
        newContext.ArbosState.L2PricingState.GasBacklogStorage.Set(System.Math.Min(long.MaxValue, gasToDonate) + 1);
        // Reset gas for correct retry tx hash computation (context initialization consumes gas)
        newContext.ResetGasLeft();

        bool exists = ArbRetryableTxParser.PrecompileImplementation.TryGetValue(_redeemId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbRetryableTxParser.PrecompileFunctionDescription[_redeemId].AbiFunctionDescription;
        byte[] inputdata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            ticketIdHash
        );

        byte[] result = implementation!(newContext, inputdata);

        result.Should().BeEquivalentTo(expectedTxHash.BytesToArray());
    }

    [Test]
    public void ParsesRedeem_WithInvalidInputData_ThrowsRevertException()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        // too small ticketId parameter
        byte[] invalidInputData = new byte[Keccak.Size - 1];

        PrecompileTestContextBuilder context = new(worldState, 0);

        bool exists = ArbRetryableTxParser.PrecompileImplementation.TryGetValue(_redeemId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        Action action = () => implementation!(context, invalidInputData);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateRevertException("", true);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesSubmitRetryable_ValidInputData_ThrowsSolidityError()
    {
        AbiSignature signature = new AbiSignature("submitRetryable",
            AbiType.Bytes32, AbiType.UInt256, AbiType.UInt256, AbiType.UInt256,
            AbiType.UInt256, AbiType.UInt64, AbiType.UInt256, AbiType.Address,
            AbiType.Address, AbiType.Address, AbiType.DynamicBytes);

        byte[] encodedParams = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            signature,
            Hash256.Zero, UInt256.Zero, UInt256.Zero, UInt256.Zero,
            UInt256.Zero, (ulong)0, UInt256.Zero, Address.Zero,
            Address.Zero, Address.Zero, Array.Empty<byte>()
        );

        bool exists = ArbRetryableTxParser.PrecompileImplementation.TryGetValue(_submitRetryableId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        Action action = () => implementation!(null!, encodedParams);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbRetryableTx.NotCallableSolidityError();
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesSubmitRetryable_WithInvalidInputData_ThrowsRevertException()
    {
        bool exists = ArbRetryableTxParser.PrecompileImplementation.TryGetValue(_submitRetryableId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        // too small ticketId parameter
        byte[] invalidInputData = new byte[Hash256.Size - 1];
        Action action = () => implementation!(null!, invalidInputData);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateRevertException("", true);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }
}
