using FluentAssertions;
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
using Nethermind.State;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

public class ArbRetryableTxParserTests
{
    private static readonly ILogManager Logger = LimboLogs.Instance;

    [Test]
    public void ParsesRedeem_ValidInputData_ReturnsCreatedRetryTxHash()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

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

        // Setup input data
        string redeemMethodId = "0xeda1122c";
        string ticketIdStr = ticketIdHash.ToString(withZeroX: false);
        byte[] inputData = Bytes.FromHexString($"{redeemMethodId}{ticketIdStr}");

        ArbRetryableTxParser arbRetryableTxParser = new();
        byte[] result = arbRetryableTxParser.RunAdvanced(newContext, inputData);

        result.Should().BeEquivalentTo(expectedTxHash.BytesToArray());
    }

    [Test]
    public void ParsesRedeem_WithInvalidInputData_Throws()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        byte[] redeemMethodId = Bytes.FromHexString("0xeda1122c");
        // too small ticketId parameter
        Span<byte> invalidInputData = stackalloc byte[redeemMethodId.Length + Keccak.Size - 1];
        redeemMethodId.CopyTo(invalidInputData);

        PrecompileTestContextBuilder context = new(worldState, 0);
        byte[] invalidInputDataBytes = invalidInputData.ToArray();

        ArbRetryableTxParser arbRetryableTxParser = new();
        Action action = () => arbRetryableTxParser.RunAdvanced(context, invalidInputDataBytes);
        action.Should().Throw<EndOfStreamException>();
    }

    [Test]
    public void ParsesGetLifetime_Always_ReturnsDefaultLifetime()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, 0);

        byte[] getLifetimeMethodId = Bytes.FromHexString("0x81e6e083");

        ArbRetryableTxParser arbRetryableTxParser = new();
        byte[] result = arbRetryableTxParser.RunAdvanced(context, getLifetimeMethodId);

        UInt256 expectedResult = new(Retryable.RetryableLifetimeSeconds);
        result.Should().BeEquivalentTo(expectedResult.ToBigEndian());
    }

    [Test]
    public void ParsesGetTimeout_RetryableExists_ReturnsCalculatedTimeout()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

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

        string getTimeoutMethodId = "0x9f1025c6";
        string ticketIdStr = ticketId.ToString(withZeroX: false);
        byte[] inputData = Bytes.FromHexString($"{getTimeoutMethodId}{ticketIdStr}");

        ArbRetryableTxParser arbRetryableTxParser = new();
        byte[] result = arbRetryableTxParser.RunAdvanced(context, inputData);

        UInt256 expectedCalculatedTimeout = new(timeout + timeoutWindowsLeft * Retryable.RetryableLifetimeSeconds);
        result.Should().BeEquivalentTo(expectedCalculatedTimeout.ToBigEndian());
    }

    [Test]
    public void ParsesGetTimeout_WithInvalidInputData_Throws()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        byte[] getTimeoutMethodId = Bytes.FromHexString("0x9f1025c6");
        // too small ticketId parameter
        Span<byte> invalidInputData = stackalloc byte[getTimeoutMethodId.Length + Keccak.Size - 1];
        getTimeoutMethodId.CopyTo(invalidInputData);

        PrecompileTestContextBuilder context = new(worldState, 0);
        byte[] invalidInputDataBytes = invalidInputData.ToArray();

        ArbRetryableTxParser arbRetryableTxParser = new();
        Action action = () => arbRetryableTxParser.RunAdvanced(context, invalidInputDataBytes);
        action.Should().Throw<EndOfStreamException>();
    }

    [Test]
    public void ParsesKeepAlive_RetryableExpiresBefore1Lifetime_ReturnsNewTimeout()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

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

        string keepAliveMethodId = "0xf0b21a41";
        string ticketIdStr = ticketId.ToString(withZeroX: false);
        byte[] inputData = Bytes.FromHexString($"{keepAliveMethodId}{ticketIdStr}");

        ArbRetryableTxParser arbRetryableTxParser = new();
        byte[] result = arbRetryableTxParser.RunAdvanced(newContext, inputData);

        UInt256 expectedNewTimeout = timeout + Retryable.RetryableLifetimeSeconds;
        result.Should().BeEquivalentTo(expectedNewTimeout.ToBigEndian());
    }

    [Test]
    public void ParsesKeepAlive_WithInvalidInputData_Throws()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        byte[] keepAliveMethodId = Bytes.FromHexString("0xf0b21a41");
        // too small ticketId parameter
        Span<byte> invalidInputData = stackalloc byte[keepAliveMethodId.Length + Keccak.Size - 1];
        keepAliveMethodId.CopyTo(invalidInputData);

        PrecompileTestContextBuilder context = new(worldState, 0);
        byte[] invalidInputDataBytes = invalidInputData.ToArray();

        ArbRetryableTxParser arbRetryableTxParser = new();
        Action action = () => arbRetryableTxParser.RunAdvanced(context, invalidInputDataBytes);
        action.Should().Throw<EndOfStreamException>();
    }

    [Test]
    public void ParsesGetBeneficiary_RetryableExists_ReturnsBeneficiary()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

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

        string getBeneficiaryMethodId = "0xba20dda4";
        string ticketIdStr = ticketId.ToString(withZeroX: false);
        byte[] inputData = Bytes.FromHexString($"{getBeneficiaryMethodId}{ticketIdStr}");

        ArbRetryableTxParser arbRetryableTxParser = new();
        byte[] result = arbRetryableTxParser.RunAdvanced(context, inputData);

        byte[] expectedAbiEncodedAddress = Address.SystemUser.Bytes.PadLeft(Hash256.Size);

        result.Should().BeEquivalentTo(expectedAbiEncodedAddress);
    }

    [Test]
    public void ParsesGetBeneficiary_WithInvalidInputData_Throws()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        byte[] getBeneficiaryMethodId = Bytes.FromHexString("0xba20dda4");
        // too small ticketId parameter
        Span<byte> invalidInputData = stackalloc byte[getBeneficiaryMethodId.Length + Keccak.Size - 1];
        getBeneficiaryMethodId.CopyTo(invalidInputData);

        PrecompileTestContextBuilder context = new(worldState, 0);
        byte[] invalidInputDataBytes = invalidInputData.ToArray();

        ArbRetryableTxParser arbRetryableTxParser = new();
        Action action = () => arbRetryableTxParser.RunAdvanced(context, invalidInputDataBytes);
        action.Should().Throw<EndOfStreamException>();
    }

    [Test]
    public void ParsesCancel_RetryableExists_ReturnsEmptyOutput()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

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

        string cancelMethodId = "0xc4d252f5";
        string ticketIdStr = ticketId.ToString(withZeroX: false);
        byte[] inputData = Bytes.FromHexString($"{cancelMethodId}{ticketIdStr}");

        ArbRetryableTxParser arbRetryableTxParser = new();
        byte[] result = arbRetryableTxParser.RunAdvanced(newContext, inputData);

        result.Should().BeEmpty();
    }

    [Test]
    public void ParsesCancel_WithInvalidInputData_Throws()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        byte[] cancelMethodId = Bytes.FromHexString("0xc4d252f5");
        // too small ticketId parameter
        Span<byte> invalidInputData = stackalloc byte[cancelMethodId.Length + Keccak.Size - 1];
        cancelMethodId.CopyTo(invalidInputData);

        PrecompileTestContextBuilder context = new(worldState, 0);
        byte[] invalidInputDataBytes = invalidInputData.ToArray();

        ArbRetryableTxParser arbRetryableTxParser = new();
        Action action = () => arbRetryableTxParser.RunAdvanced(context, invalidInputDataBytes);
        action.Should().Throw<EndOfStreamException>();
    }

    [Test]
    public void ParsesGetCurrentRedeemer_Always_ReturnsRedeemerOrZeroAddress()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        Address redeemer = new(ArbRetryableTxTests.Hash256FromUlong(123));
        PrecompileTestContextBuilder context = new(worldState, ulong.MaxValue)
        {
            CurrentRefundTo = redeemer
        };

        byte[] getCurrentRedeemerMethodId = Bytes.FromHexString("0xde4ba2b3");

        ArbRetryableTxParser arbRetryableTxParser = new();
        byte[] result = arbRetryableTxParser.RunAdvanced(context, getCurrentRedeemerMethodId);

        byte[] expectedAbiEncodedAddress = redeemer.Bytes.PadLeft(Hash256.Size);

        result.Should().BeEquivalentTo(expectedAbiEncodedAddress);
    }

    [Test]
    public void ParsesSubmitRetryable_ValidInputData_ThrowsSolidityError()
    {
        byte[] submitRetryableMethodId = Bytes.FromHexString("0xc9f95d32");
        // SubmitRetryable takes 10 static parameters (no data for last dynamic parameter)
        Span<byte> inputData = stackalloc byte[submitRetryableMethodId.Length + 10 * Hash256.Size];
        submitRetryableMethodId.CopyTo(inputData);

        byte[] inputDataBytes = inputData.ToArray();

        ArbRetryableTxParser arbRetryableTxParser = new();
        Action action = () => arbRetryableTxParser.RunAdvanced(null!, inputDataBytes);

        PrecompileSolidityError expectedError = ArbRetryableTx.NotCallableSolidityError();

        PrecompileSolidityError thrownException = action.Should().Throw<PrecompileSolidityError>().Which;
        thrownException.ErrorData.Should().BeEquivalentTo(expectedError.ErrorData);
    }

    [Test]
    public void ParsesSubmitRetryable_WithInvalidInputData_Throws()
    {
        byte[] submitRetryableMethodId = Bytes.FromHexString("0xc9f95d32");
        // too small ticketId parameter
        Span<byte> invalidInputData = stackalloc byte[submitRetryableMethodId.Length + Hash256.Size - 1];
        submitRetryableMethodId.CopyTo(invalidInputData);

        byte[] invalidInputDataBytes = invalidInputData.ToArray();

        ArbRetryableTxParser arbRetryableTxParser = new();
        Action action = () => arbRetryableTxParser.RunAdvanced(null!, invalidInputDataBytes);

        action.Should().Throw<EndOfStreamException>();
    }
}
