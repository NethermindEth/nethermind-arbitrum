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
    public void MintNativeToken_WithUnauthorizedCaller_Reverts()
    {
        TestContext testContext = CreateTestContext(owner: TestItem.AddressA, authorizeOwner: false);
        UInt256 mintAmount = 1000;

        Action action = () => ArbNativeTokenManager.MintNativeToken(testContext.Context, mintAmount);

        action.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*only native token owners can mint native token*");
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
    public void BurnNativeToken_WithUnauthorizedCaller_Reverts()
    {
        Address unauthorizedCaller = TestItem.AddressA;
        UInt256 burnAmount = 1000;
        UInt256 initialBalance = 10000;

        TestContext testContext = CreateTestContext(unauthorizedCaller, authorizeOwner: false, initialBalance: initialBalance);

        Action action = () => ArbNativeTokenManager.BurnNativeToken(testContext.Context, burnAmount);

        action.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*only native token owners can burn native token*");
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

    private static ulong CalculateEventGasCost(LogEntry logEntry)
    {
        return GasCostOf.Log +
               GasCostOf.LogTopic * (ulong)logEntry.Topics.Length +
               GasCostOf.LogData * (ulong)logEntry.Data.Length;
    }

    private sealed class TestContext
    {
        public IWorldState WorldState { get; }
        public IDisposable WorldStateDisposer { get; }
        public ArbitrumPrecompileExecutionContext Context { get; }

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
