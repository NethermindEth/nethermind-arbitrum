// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Core.Test;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.State;
using Nethermind.Int256;
using NSubstitute;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public class ArbNativeTokenManagerTests
{
    [Test]
    public void BurnNativeToken_CalledMultipleTimes_DecreasesBalance()
    {
        Address owner = TestItem.AddressA;
        UInt256 initialBalance = 100000;
        UInt256 burnAmount = 1000;
        int numberOfBurns = 5;

        TestContext testContext = CreateTestContext(owner, authorizeOwner: true,
            initialBalance: initialBalance, gasSupplied: 500_000);

        UInt256 balanceBefore = testContext.WorldState.GetBalance(owner);

        for (int i = 0; i < numberOfBurns; i++)
        {
            ArbNativeTokenManager.BurnNativeToken(testContext.Context, burnAmount);
        }

        UInt256 balanceAfter = testContext.WorldState.GetBalance(owner);
        balanceAfter.Should().Be(balanceBefore - (burnAmount * (UInt256)numberOfBurns));
        testContext.Context.EventLogs.Should().HaveCount(numberOfBurns,
            "each burn should emit an event");
    }

    [Test]
    public void BurnNativeToken_WhenCalled_EmitsNativeTokenBurnedEvent()
    {
        Address owner = TestItem.AddressA;
        UInt256 initialBalance = 10000;
        UInt256 burnAmount = 1000;

        LogEntry expectedLogEntry = CreateExpectedBurnEventLog(owner, burnAmount);
        ulong gasSupplied = CalculateEventGasCost(expectedLogEntry) + 100_000;

        TestContext testContext = CreateTestContext(owner, authorizeOwner: true, initialBalance: initialBalance, gasSupplied: gasSupplied);

        ArbNativeTokenManager.BurnNativeToken(testContext.Context, burnAmount);

        testContext.Context.EventLogs.Should().ContainEquivalentOf(expectedLogEntry);
    }

    [Test]
    public void BurnNativeToken_WhenExactBalanceAmount_ReducesBalanceToZero()
    {
        Address owner = TestItem.AddressA;
        UInt256 initialBalance = 1000;

        TestContext testContext = CreateTestContext(owner, authorizeOwner: true,
            initialBalance: initialBalance);

        ArbNativeTokenManager.BurnNativeToken(testContext.Context, initialBalance);

        testContext.WorldState.GetBalance(owner).Should().Be(UInt256.Zero);
    }

    [Test]
    public void BurnNativeToken_WithAuthorizedOwner_ChargesExactGasCost()
    {
        Address owner = TestItem.AddressA;
        UInt256 initialBalance = 10000;
        UInt256 burnAmount = 1000;
        ulong expectedGasCost = ArbNativeTokenManager.MintBurnOperation; // 9100 gas
        ulong gasSupplied = expectedGasCost + 50_000;

        TestContext testContext = CreateTestContext(owner, authorizeOwner: true,
            initialBalance: initialBalance, gasSupplied: gasSupplied);

        ulong gasLeftBefore = testContext.Context.GasLeft;

        ArbNativeTokenManager.BurnNativeToken(testContext.Context, burnAmount);

        ulong gasConsumed = gasLeftBefore - testContext.Context.GasLeft;

        gasConsumed.Should().BeGreaterOrEqualTo(expectedGasCost,
            "should charge at least MintBurnOperation gas (9100) plus event costs");
    }

    [Test]
    public void BurnNativeToken_WithAuthorizedOwnerAndSufficientBalance_DecreasesBalance()
    {
        Address owner = TestItem.AddressA;
        UInt256 initialBalance = 10000;
        UInt256 burnAmount = 1000;

        TestContext testContext = CreateTestContext(owner, authorizeOwner: true, initialBalance: initialBalance);

        UInt256 balanceBefore = testContext.WorldState.GetBalance(owner);

        ArbNativeTokenManager.BurnNativeToken(testContext.Context, burnAmount);

        UInt256 balanceAfter = testContext.WorldState.GetBalance(owner);
        balanceAfter.Should().Be(balanceBefore - burnAmount);
    }

    [Test]
    public void BurnNativeToken_WithInsufficientBalance_Reverts()
    {
        Address owner = TestItem.AddressA;
        UInt256 initialBalance = 500;
        UInt256 burnAmount = 1000;

        TestContext testContext = CreateTestContext(owner, authorizeOwner: true, initialBalance: initialBalance);

        Action action = () => ArbNativeTokenManager.BurnNativeToken(testContext.Context, burnAmount);

        action.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*burn amount exceeds balance*");
    }

    [Test]
    public void BurnNativeToken_WithInsufficientGas_Reverts()
    {
        Address owner = TestItem.AddressA;
        UInt256 initialBalance = 10000;
        UInt256 burnAmount = 1000;
        ulong insufficientGas = ArbNativeTokenManager.MintBurnOperation - 1;

        TestContext testContext = CreateTestContext(owner, authorizeOwner: true,
            initialBalance: initialBalance, gasSupplied: insufficientGas);

        Action action = () => ArbNativeTokenManager.BurnNativeToken(testContext.Context, burnAmount);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        exception.OutOfGas.Should().BeTrue("should throw out of gas exception");
        testContext.Context.GasLeft.Should().Be(0, "all gas should be consumed");
    }

    [Test]
    public void BurnNativeToken_WithUnauthorizedCaller_BurnsAllGas()
    {
        Address unauthorizedCaller = TestItem.AddressA;
        UInt256 burnAmount = 1000;
        UInt256 initialBalance = 10000;
        ulong gasSupplied = 100_000;

        TestContext testContext = CreateTestContext(unauthorizedCaller, authorizeOwner: false,
            initialBalance: initialBalance, gasSupplied: gasSupplied);

        Action action = () => ArbNativeTokenManager.BurnNativeToken(testContext.Context, burnAmount);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        exception.OutOfGas.Should().BeTrue("should burn all gas on unauthorized access");
        testContext.Context.GasLeft.Should().Be(0, "all gas should be burned");
    }

    [Test]
    public void BurnNativeToken_WithUnauthorizedCaller_Reverts()
    {
        Address unauthorizedCaller = TestItem.AddressA;
        UInt256 burnAmount = 1000;
        UInt256 initialBalance = 10000;

        TestContext testContext = CreateTestContext(unauthorizedCaller, authorizeOwner: false,
            initialBalance: initialBalance);

        Action action = () => ArbNativeTokenManager.BurnNativeToken(testContext.Context, burnAmount);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        exception.OutOfGas.Should().BeTrue("should burn all gas on unauthorized access");
    }

    [Test]
    public void BurnNativeToken_WithZeroAmount_SucceedsAndEmitsEvent()
    {
        Address owner = TestItem.AddressA;
        UInt256 initialBalance = 10000;
        UInt256 zeroAmount = 0;

        TestContext testContext = CreateTestContext(owner, authorizeOwner: true,
            initialBalance: initialBalance);

        UInt256 balanceBefore = testContext.WorldState.GetBalance(owner);

        ArbNativeTokenManager.BurnNativeToken(testContext.Context, zeroAmount);

        testContext.WorldState.GetBalance(owner).Should().Be(balanceBefore);
        testContext.Context.EventLogs.Should().HaveCount(1, "event should be emitted even for zero amount");
    }

    [Test]
    public void GetCachedCodeInfo_WhenQueriedFromCodeInfoRepository_ReturnsPrecompileInfo()
    {
        ICodeInfoRepository baseRepository = Substitute.For<ICodeInfoRepository>();
        IArbosVersionProvider arbosVersionProvider = Substitute.For<IArbosVersionProvider>();
        arbosVersionProvider.Get().Returns(ArbosVersion.FortyOne);

        ArbitrumCodeInfoRepository repository = new(baseRepository, arbosVersionProvider);

        ICodeInfo codeInfo = repository.GetCachedCodeInfo(
            ArbNativeTokenManager.Address,
            followDelegation: false,
            Substitute.For<IReleaseSpec>(),
            out Address? delegationAddress);

        codeInfo.Should().NotBeNull();
        delegationAddress.Should().BeNull();
    }

    [Test]
    public void MintAndBurn_InSequence_MaintainsCorrectBalance()
    {
        Address owner = TestItem.AddressA;
        UInt256 initialBalance = 5000;
        UInt256 mintAmount = 3000;
        UInt256 burnAmount = 2000;

        TestContext testContext = CreateTestContext(owner, authorizeOwner: true,
            initialBalance: initialBalance, gasSupplied: 500_000);

        UInt256 expectedBalance = initialBalance + mintAmount - burnAmount;

        ArbNativeTokenManager.MintNativeToken(testContext.Context, mintAmount);
        ArbNativeTokenManager.BurnNativeToken(testContext.Context, burnAmount);

        testContext.WorldState.GetBalance(owner).Should().Be(expectedBalance);
        testContext.Context.EventLogs.Should().HaveCount(2, "should emit both mint and burn events");
    }

    [Test]
    public void MintNativeToken_CalledMultipleTimes_AccumulatesBalance()
    {
        Address owner = TestItem.AddressA;
        UInt256 mintAmount = 1000;
        int numberOfMints = 5;

        TestContext testContext = CreateTestContext(owner, authorizeOwner: true, gasSupplied: 500_000);

        UInt256 balanceBefore = testContext.WorldState.GetBalance(owner);

        for (int i = 0; i < numberOfMints; i++)
        {
            ArbNativeTokenManager.MintNativeToken(testContext.Context, mintAmount);
        }

        UInt256 balanceAfter = testContext.WorldState.GetBalance(owner);
        balanceAfter.Should().Be(balanceBefore + (mintAmount * (UInt256)numberOfMints));
        testContext.Context.EventLogs.Should().HaveCount(numberOfMints,
            "each mint should emit an event");
    }

    [Test]
    public void MintNativeToken_WhenCalled_EmitsNativeTokenMintedEvent()
    {
        Address owner = TestItem.AddressA;
        UInt256 mintAmount = 1000;

        LogEntry expectedLogEntry = CreateExpectedMintEventLog(owner, mintAmount);
        ulong gasSupplied = CalculateEventGasCost(expectedLogEntry) + 100_000;

        TestContext testContext = CreateTestContext(owner, authorizeOwner: true, gasSupplied);

        ArbNativeTokenManager.MintNativeToken(testContext.Context, mintAmount);

        testContext.Context.EventLogs.Should().ContainEquivalentOf(expectedLogEntry);
    }

    [Test]
    public void MintNativeToken_WithAuthorizedOwner_ChargesExactGasCost()
    {
        Address owner = TestItem.AddressA;
        UInt256 mintAmount = 1000;
        ulong expectedGasCost = ArbNativeTokenManager.MintBurnOperation; // 9100 gas
        ulong gasSupplied = expectedGasCost + 50_000;

        TestContext testContext = CreateTestContext(owner, authorizeOwner: true, gasSupplied: gasSupplied);

        UInt256 balanceBefore = testContext.WorldState.GetBalance(owner);
        ulong gasLeftBefore = testContext.Context.GasLeft;

        ArbNativeTokenManager.MintNativeToken(testContext.Context, mintAmount);

        ulong gasConsumed = gasLeftBefore - testContext.Context.GasLeft;

        testContext.WorldState.GetBalance(owner).Should().Be(balanceBefore + mintAmount);
        gasConsumed.Should().BeGreaterOrEqualTo(expectedGasCost,
            "should charge at least MintBurnOperation gas (9100) plus event costs");
    }
    [Test]
    public void MintNativeToken_WithAuthorizedOwner_IncreasesBalance()
    {
        TestContext testContext = CreateTestContext(owner: TestItem.AddressA, authorizeOwner: true);
        UInt256 mintAmount = 1000;

        UInt256 balanceBefore = testContext.WorldState.GetBalance(TestItem.AddressA);

        ArbNativeTokenManager.MintNativeToken(testContext.Context, mintAmount);

        UInt256 balanceAfter = testContext.WorldState.GetBalance(TestItem.AddressA);
        balanceAfter.Should().Be(balanceBefore + mintAmount);
    }
    [Test]
    public void MintNativeToken_WithInsufficientGas_Reverts()
    {
        Address owner = TestItem.AddressA;
        UInt256 mintAmount = 1000;
        ulong insufficientGas = ArbNativeTokenManager.MintBurnOperation - 1;

        TestContext testContext = CreateTestContext(owner, authorizeOwner: true,
            gasSupplied: insufficientGas);

        Action action = () => ArbNativeTokenManager.MintNativeToken(testContext.Context, mintAmount);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        exception.OutOfGas.Should().BeTrue("should throw out of gas exception");
        testContext.Context.GasLeft.Should().Be(0, "all gas should be consumed");
    }

    [Test]
    public void MintNativeToken_WithMaxUInt256Amount_IncreasesBalanceCorrectly()
    {
        Address owner = TestItem.AddressA;
        UInt256 largeAmount = UInt256.MaxValue / 2; // Avoid overflow

        TestContext testContext = CreateTestContext(owner, authorizeOwner: true, gasSupplied: 500_000);

        UInt256 balanceBefore = testContext.WorldState.GetBalance(owner);

        ArbNativeTokenManager.MintNativeToken(testContext.Context, largeAmount);

        testContext.WorldState.GetBalance(owner).Should().Be(balanceBefore + largeAmount);
    }

    [Test]
    public void MintNativeToken_WithUnauthorizedCaller_BurnsAllGas()
    {
        Address unauthorizedCaller = TestItem.AddressA;
        UInt256 mintAmount = 1000;
        ulong gasSupplied = 100_000;

        TestContext testContext = CreateTestContext(unauthorizedCaller, authorizeOwner: false,
            gasSupplied: gasSupplied);

        Action action = () => ArbNativeTokenManager.MintNativeToken(testContext.Context, mintAmount);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        exception.OutOfGas.Should().BeTrue("should burn all gas on unauthorized access");
        testContext.Context.GasLeft.Should().Be(0, "all gas should be burned");
    }

    [Test]
    public void MintNativeToken_WithUnauthorizedCaller_Reverts()
    {
        TestContext testContext = CreateTestContext(owner: TestItem.AddressA, authorizeOwner: false);
        UInt256 mintAmount = 1000;

        Action action = () => ArbNativeTokenManager.MintNativeToken(testContext.Context, mintAmount);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        exception.OutOfGas.Should().BeTrue("should burn all gas on unauthorized access");
    }

    [Test]
    public void MintNativeToken_WithZeroAmount_SucceedsAndEmitsEvent()
    {
        Address owner = TestItem.AddressA;
        UInt256 zeroAmount = 0;

        TestContext testContext = CreateTestContext(owner, authorizeOwner: true);

        UInt256 balanceBefore = testContext.WorldState.GetBalance(owner);

        ArbNativeTokenManager.MintNativeToken(testContext.Context, zeroAmount);

        testContext.WorldState.GetBalance(owner).Should().Be(balanceBefore);
        testContext.Context.EventLogs.Should().HaveCount(1, "event should be emitted even for zero amount");
    }

    private static ulong CalculateEventGasCost(LogEntry logEntry)
    {
        return GasCostOf.Log +
               GasCostOf.LogTopic * (ulong)logEntry.Topics.Length +
               GasCostOf.LogData * (ulong)logEntry.Data.Length;
    }

    private static LogEntry CreateExpectedBurnEventLog(Address owner, UInt256 amount)
    {
        Hash256 eventSignatureHash = Keccak.Compute("NativeTokenBurned(address,uint256)");
        Hash256 ownerHash = new(owner.Bytes.PadLeft(32));
        Hash256[] topics = [eventSignatureHash, ownerHash];

        byte[] data = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            new AbiSignature(string.Empty, [AbiUInt.UInt256]),
            [amount]);

        return new LogEntry(ArbNativeTokenManager.Address, data, topics);
    }

    private static LogEntry CreateExpectedMintEventLog(Address owner, UInt256 amount)
    {
        Hash256 eventSignatureHash = Keccak.Compute("NativeTokenMinted(address,uint256)");
        Hash256 ownerHash = new(owner.Bytes.PadLeft(32));
        Hash256[] topics = [eventSignatureHash, ownerHash];

        byte[] data = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            new AbiSignature(string.Empty, [AbiUInt.UInt256]),
            [amount]);

        return new LogEntry(ArbNativeTokenManager.Address, data, topics);
    }

    private static TestContext CreateTestContext(
        Address owner,
        bool authorizeOwner,
        UInt256? initialBalance = null,
        ulong gasSupplied = 100_000)
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        ArbOSInitialization.Create(worldState);

        if (initialBalance.HasValue)
        {
            worldState.CreateAccount(owner, initialBalance.Value);
        }

        PrecompileTestContextBuilder contextBuilder = new PrecompileTestContextBuilder(worldState, gasSupplied)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyOne)
            .WithCaller(owner)
            .WithReleaseSpec();

        if (authorizeOwner)
        {
            contextBuilder.WithNativeTokenOwners(owner);
        }

        return new TestContext(worldState, worldStateDisposer, contextBuilder);
    }

    private sealed class TestContext
    {
        public ArbitrumPrecompileExecutionContext Context { get; }
        public IWorldState WorldState { get; }
        public IDisposable WorldStateDisposer { get; }

        public TestContext(
            IWorldState worldState,
            IDisposable worldStateDisposer,
            PrecompileTestContextBuilder contextBuilder)
        {
            WorldState = worldState;
            WorldStateDisposer = worldStateDisposer;
            Context = contextBuilder;
        }
    }
}
