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
    public void NativeTokenManagement_DisabledByDefault()
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
        mintAction.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*only native token owners can mint native token*");

        // Attempting to burn without being a native token owner should revert
        Action burnAction = () => ArbNativeTokenManager.BurnNativeToken(context, amount);
        burnAction.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*only native token owners can burn native token*");
    }
}
