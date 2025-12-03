using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public class ArbNativeTokenManagerTests
{
    [Test]
    public void MintNativeToken_WithAuthorizedOwner_IncreasesBalance()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        Address owner = TestItem.AddressA;
        UInt256 mintAmount = 1000;

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 100_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyOne)
            .WithCaller(owner)
            .WithNativeTokenOwners(owner)
            .WithReleaseSpec();

        UInt256 balanceBefore = worldState.GetBalance(owner);

        ArbNativeTokenManager.MintNativeToken(context, mintAmount);

        UInt256 balanceAfter = worldState.GetBalance(owner);
        balanceAfter.Should().Be(balanceBefore + mintAmount);
    }

    [Test]
    public void MintNativeToken_WithUnauthorizedCaller_Reverts()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        Address unauthorizedCaller = TestItem.AddressA;
        UInt256 mintAmount = 1000;

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 100_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyOne)
            .WithCaller(unauthorizedCaller)
            .WithReleaseSpec();

        Action action = () => ArbNativeTokenManager.MintNativeToken(context, mintAmount);

        action.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*only native token owners can mint native token*");
    }

    [Test]
    public void MintNativeToken_EmitsNativeTokenMintedEvent()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        Address owner = TestItem.AddressA;
        UInt256 mintAmount = 1000;

        // Calculate expected gas cost for an event
        Hash256 eventSignatureHash = Keccak.Compute("NativeTokenMinted(address,uint256)");
        Hash256 ownerHash = new(owner.Bytes.PadLeft(32));
        Hash256[] expectedEventTopics = [eventSignatureHash, ownerHash];

        byte[] expectedEventData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            new AbiSignature(string.Empty, [AbiUInt.UInt256]),
            [mintAmount]);

        LogEntry expectedLogEntry = new(ArbNativeTokenManager.Address, expectedEventData, expectedEventTopics);

        ulong gasSupplied =
            GasCostOf.Log +
            GasCostOf.LogTopic * (ulong)expectedEventTopics.Length +
            GasCostOf.LogData * (ulong)expectedEventData.Length +
            100_000; // Extra gas for mint operation

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, gasSupplied)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyOne)
            .WithCaller(owner)
            .WithNativeTokenOwners(owner)
            .WithReleaseSpec();

        ArbNativeTokenManager.MintNativeToken(context, mintAmount);

        context.EventLogs.Should().ContainEquivalentOf(expectedLogEntry);
    }

    [Test]
    public void BurnNativeToken_WithAuthorizedOwner_DecreasesBalance()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        Address owner = TestItem.AddressA;
        UInt256 initialBalance = 10000;
        UInt256 burnAmount = 1000;

        // Give the owner some initial balance
        worldState.CreateAccount(owner, initialBalance);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 100_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyOne)
            .WithCaller(owner)
            .WithNativeTokenOwners(owner)
            .WithReleaseSpec();

        UInt256 balanceBefore = worldState.GetBalance(owner);

        ArbNativeTokenManager.BurnNativeToken(context, burnAmount);

        UInt256 balanceAfter = worldState.GetBalance(owner);
        balanceAfter.Should().Be(balanceBefore - burnAmount);
    }

    [Test]
    public void BurnNativeToken_WithInsufficientBalance_Reverts()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        Address owner = TestItem.AddressA;
        UInt256 initialBalance = 500;
        UInt256 burnAmount = 1000; // More than balance

        // Give the owner some initial balance
        worldState.CreateAccount(owner, initialBalance);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 100_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyOne)
            .WithCaller(owner)
            .WithNativeTokenOwners(owner)
            .WithReleaseSpec();

        Action action = () => ArbNativeTokenManager.BurnNativeToken(context, burnAmount);

        action.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*burn amount exceeds balance*");
    }

    [Test]
    public void BurnNativeToken_WithUnauthorizedCaller_Reverts()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        Address unauthorizedCaller = TestItem.AddressA;
        UInt256 burnAmount = 1000;

        // Give the caller some balance
        worldState.CreateAccount(unauthorizedCaller, 10000);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 100_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyOne)
            .WithCaller(unauthorizedCaller)
            .WithReleaseSpec();

        Action action = () => ArbNativeTokenManager.BurnNativeToken(context, burnAmount);

        action.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*only native token owners can burn native token*");
    }

    [Test]
    public void BurnNativeToken_EmitsNativeTokenBurnedEvent()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        Address owner = TestItem.AddressA;
        UInt256 initialBalance = 10000;
        UInt256 burnAmount = 1000;

        // Give the owner some initial balance
        worldState.CreateAccount(owner, initialBalance);

        // Calculate expected gas cost for an event
        Hash256 eventSignatureHash = Keccak.Compute("NativeTokenBurned(address,uint256)");
        Hash256 ownerHash = new(owner.Bytes.PadLeft(32));
        Hash256[] expectedEventTopics = [eventSignatureHash, ownerHash];

        byte[] expectedEventData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            new AbiSignature(string.Empty, [AbiUInt.UInt256]),
            [burnAmount]);

        LogEntry expectedLogEntry = new(ArbNativeTokenManager.Address, expectedEventData, expectedEventTopics);

        ulong gasSupplied =
            GasCostOf.Log +
            GasCostOf.LogTopic * (ulong)expectedEventTopics.Length +
            GasCostOf.LogData * (ulong)expectedEventData.Length +
            100_000; // Extra gas for burn operation

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, gasSupplied)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyOne)
            .WithCaller(owner)
            .WithNativeTokenOwners(owner)
            .WithReleaseSpec();

        ArbNativeTokenManager.BurnNativeToken(context, burnAmount);

        context.EventLogs.Should().ContainEquivalentOf(expectedLogEntry);
    }
}
