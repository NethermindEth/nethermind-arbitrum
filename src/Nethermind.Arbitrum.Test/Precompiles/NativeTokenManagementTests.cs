using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Test;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm.State;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public class NativeTokenManagementTests
{
    [Test]
    public void GetAllNativeTokenOwners_ByDefault_ReturnsEmpty()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyOne)
            .WithReleaseSpec();

        // Verify no native token owners are set by default
        Address[] owners = ArbOwner.GetAllNativeTokenOwners(context);
        owners.Should().BeEmpty("Native token management should be disabled by default");

        Address testAddress = TestItem.AddressA;
        bool isOwner = ArbOwner.IsNativeTokenOwner(context, testAddress);
        isOwner.Should().BeFalse("Address should not be a native token owner by default");
    }
    [Test]
    public void EnableNativeTokenManagement_BeforeDelay_Reverts()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        Address chainOwner = TestItem.AddressA;

        // Calculate timestamps - 6 days from now (less than required 7 days)
        ulong currentTime = 1700000000;
        ulong sixDaysFromNow = currentTime + 6 * 24 * 60 * 60;

        // Create block header with current timestamp
        BlockHeader blockHeader = Build.A.BlockHeader
            .WithNumber(1)
            .WithTimestamp(currentTime)
            .TestObject;

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyOne)
            .WithCaller(chainOwner)
            .WithBlockExecutionContext(blockHeader)
            .WithReleaseSpec();

        // Attempting to enable before the 7-day delay should revert,
        // Note: SetNativeTokenManagementFrom validates timestamp >= now + 7 days
        Action action = () => ArbOwner.SetNativeTokenManagementFrom(context, sixDaysFromNow);

        action.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*must be enabled at least 7 days in the future*");
    }

    [Test]
    public void MintBurn_BeforeFeatureEnabled_Reverts()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        Address caller = TestItem.AddressA;
        UInt256 amount = 1000;

        // Give caller some balance for burn test
        worldState.CreateAccount(caller, 10000);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 100_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyOne)
            .WithCaller(caller)
            .WithReleaseSpec();
        // Note: NOT adding caller to NativeTokenOwners - feature is disabled by default

        // Verify native token management is disabled (no owners)
        Address[] owners = ArbOwner.GetAllNativeTokenOwners(context);
        owners.Should().BeEmpty();

        // Attempting to mint without being a native token owner should revert
        Action mintAction = () => ArbNativeTokenManager.MintNativeToken(context, amount);

        ArbitrumPrecompileException exception = mintAction.Should().Throw<ArbitrumPrecompileException>().Which;
        exception.OutOfGas.Should().BeTrue("should burn all gas when not authorized");

        Action burnAction = () => ArbNativeTokenManager.BurnNativeToken(context, amount);

        exception = burnAction.Should().Throw<ArbitrumPrecompileException>().Which;
        exception.OutOfGas.Should().BeTrue("should burn all gas when not authorized");
    }

    [Test]
    public void EnableNativeTokenManagement_ExactlySevenDaysInFuture_Succeeds()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        Address chainOwner = TestItem.AddressA;
        ulong currentTime = 1700000000;
        ulong sevenDaysFromNow = currentTime + 7 * 24 * 60 * 60;

        BlockHeader blockHeader = Build.A.BlockHeader
            .WithNumber(1)
            .WithTimestamp(currentTime)
            .TestObject;

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyOne)
            .WithCaller(chainOwner)
            .WithBlockExecutionContext(blockHeader)
            .WithReleaseSpec();

        Action action = () => ArbOwner.SetNativeTokenManagementFrom(context, sevenDaysFromNow);

        action.Should().NotThrow("exactly 7 days in the future should be valid");
    }

    [Test]
    public void AddNativeTokenOwner_BeforeFeatureEnabled_Reverts()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        Address chainOwner = TestItem.AddressA;
        Address newOwner = TestItem.AddressB;
        ulong currentTime = 1700000000;
        ulong enableTime = currentTime + 8 * 24 * 60 * 60;

        BlockHeader blockHeaderNow = Build.A.BlockHeader
            .WithNumber(1)
            .WithTimestamp(currentTime)
            .TestObject;

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyOne)
            .WithCaller(chainOwner)
            .WithBlockExecutionContext(blockHeaderNow)
            .WithReleaseSpec();

        ArbOwner.SetNativeTokenManagementFrom(context, enableTime);

        Action action = () => ArbOwner.AddNativeTokenOwner(context, newOwner);

        action.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*native token feature is not enabled yet*");
    }

    [Test]
    public void AddNativeTokenOwner_AfterFeatureEnabled_AddsOwnerSuccessfully()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        Address chainOwner = TestItem.AddressA;
        Address newOwner = TestItem.AddressB;
        ulong currentTime = 1700000000;
        ulong enableTime = currentTime + 8 * 24 * 60 * 60;

        BlockHeader blockHeaderSetup = Build.A.BlockHeader
            .WithNumber(1)
            .WithTimestamp(currentTime)
            .TestObject;

        PrecompileTestContextBuilder contextSetup = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyOne)
            .WithCaller(chainOwner)
            .WithBlockExecutionContext(blockHeaderSetup)
            .WithReleaseSpec();

        ArbOwner.SetNativeTokenManagementFrom(contextSetup, enableTime);

        BlockHeader blockHeaderAfterEnable = Build.A.BlockHeader
            .WithNumber(2)
            .WithTimestamp(enableTime + 1)
            .TestObject;

        PrecompileTestContextBuilder contextAfter = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyOne)
            .WithCaller(chainOwner)
            .WithBlockExecutionContext(blockHeaderAfterEnable)
            .WithReleaseSpec();

        ArbOwner.AddNativeTokenOwner(contextAfter, newOwner);

        bool isOwner = ArbOwner.IsNativeTokenOwner(contextAfter, newOwner);
        isOwner.Should().BeTrue();

        Address[] owners = ArbOwner.GetAllNativeTokenOwners(contextAfter);
        owners.Should().Contain(newOwner);
    }

    [Test]
    public void RemoveNativeTokenOwner_AfterFeatureEnabled_RemovesOwnerSuccessfully()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        Address chainOwner = TestItem.AddressA;
        Address ownerToRemove = TestItem.AddressB;
        ulong currentTime = 1700000000;
        ulong enableTime = currentTime + 8 * 24 * 60 * 60;

        BlockHeader blockHeaderSetup = Build.A.BlockHeader
            .WithNumber(1)
            .WithTimestamp(currentTime)
            .TestObject;

        PrecompileTestContextBuilder contextSetup = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyOne)
            .WithCaller(chainOwner)
            .WithBlockExecutionContext(blockHeaderSetup)
            .WithReleaseSpec();

        ArbOwner.SetNativeTokenManagementFrom(contextSetup, enableTime);

        BlockHeader blockHeaderAfterEnable = Build.A.BlockHeader
            .WithNumber(2)
            .WithTimestamp(enableTime + 1)
            .TestObject;

        PrecompileTestContextBuilder contextAfter = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyOne)
            .WithCaller(chainOwner)
            .WithBlockExecutionContext(blockHeaderAfterEnable)
            .WithReleaseSpec();

        ArbOwner.AddNativeTokenOwner(contextAfter, ownerToRemove);
        bool isOwnerBefore = ArbOwner.IsNativeTokenOwner(contextAfter, ownerToRemove);
        isOwnerBefore.Should().BeTrue();

        ArbOwner.RemoveNativeTokenOwner(contextAfter, ownerToRemove);

        bool isOwnerAfter = ArbOwner.IsNativeTokenOwner(contextAfter, ownerToRemove);
        isOwnerAfter.Should().BeFalse();

        Address[] owners = ArbOwner.GetAllNativeTokenOwners(contextAfter);
        owners.Should().NotContain(ownerToRemove);
    }

    [Test]
    public void GetAllNativeTokenOwners_WithMultipleOwners_ReturnsAllOwners()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        Address chainOwner = TestItem.AddressA;
        Address[] newOwners = [TestItem.AddressB, TestItem.AddressC, TestItem.AddressD];
        ulong currentTime = 1700000000;
        ulong enableTime = currentTime + 8 * 24 * 60 * 60;

        BlockHeader blockHeaderSetup = Build.A.BlockHeader
            .WithNumber(1)
            .WithTimestamp(currentTime)
            .TestObject;

        PrecompileTestContextBuilder contextSetup = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyOne)
            .WithCaller(chainOwner)
            .WithBlockExecutionContext(blockHeaderSetup)
            .WithReleaseSpec();

        ArbOwner.SetNativeTokenManagementFrom(contextSetup, enableTime);

        BlockHeader blockHeaderAfterEnable = Build.A.BlockHeader
            .WithNumber(2)
            .WithTimestamp(enableTime + 1)
            .TestObject;

        PrecompileTestContextBuilder contextAfter = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyOne)
            .WithCaller(chainOwner)
            .WithBlockExecutionContext(blockHeaderAfterEnable)
            .WithReleaseSpec();

        foreach (Address owner in newOwners)
        {
            ArbOwner.AddNativeTokenOwner(contextAfter, owner);
        }

        Address[] retrievedOwners = ArbOwner.GetAllNativeTokenOwners(contextAfter);

        retrievedOwners.Should().HaveCount(newOwners.Length);
        foreach (Address owner in newOwners)
        {
            retrievedOwners.Should().Contain(owner);
        }
    }

    [Test]
    public void IsNativeTokenOwner_ForNonExistentOwner_ReturnsFalse()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyOne)
            .WithReleaseSpec();

        Address nonExistentOwner = TestItem.AddressE;

        bool isOwner = ArbOwner.IsNativeTokenOwner(context, nonExistentOwner);

        isOwner.Should().BeFalse();
    }
}
