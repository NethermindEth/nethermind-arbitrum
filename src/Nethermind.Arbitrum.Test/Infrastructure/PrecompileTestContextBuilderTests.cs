using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.State;

namespace Nethermind.Arbitrum.Test.Infrastructure;

[TestFixture]
public class PrecompileTestContextBuilderTests
{
    [Test]
    public void ExtendedMethods_WithValidParameters_ConfigureContextCorrectly()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        ulong gasSupplied = 1_000_000;

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, gasSupplied)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Forty)
            .WithBlockNumber(123)
            .WithCallDepth(2)
            .WithOrigin(TestItem.AddressA.ToHash())
            .WithGrandCaller(TestItem.AddressB)
            .WithValue(UInt256.One)
            .WithTopLevelTxType(ArbitrumTxType.ArbitrumRetry)
            .WithNativeTokenOwners(TestItem.AddressC);

        context.ArbosState.Should().NotBeNull();
        context.ArbosState.CurrentArbosVersion.Should().Be(ArbosVersion.Forty);
        context.BlockExecutionContext.Header.Number.Should().Be(123);
        context.CallDepth.Should().Be(2);
        context.Origin.Should().Be(TestItem.AddressA.ToHash());
        context.GrandCaller.Should().Be(TestItem.AddressB);
        context.Value.Should().Be(UInt256.One);
        context.TopLevelTxType.Should().Be(ArbitrumTxType.ArbitrumRetry);
        context.ArbosState.NativeTokenOwners.Size().Should().BeGreaterThan(0);
    }

    [Test]
    public void WithBlockHashProvider_WithTestHashes_ReturnsCorrectHashes()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        Hash256 expectedHash = TestItem.KeccakA;
        long blockNumber = 100;

        IBlockhashProvider provider = PrecompileTestContextBuilder.CreateTestBlockHashProvider(
            (blockNumber, expectedHash)
        );

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithBlockHashProvider(provider);

        context.BlockHashProvider.Should().NotBeNull();
        Hash256? actualHash = context.BlockHashProvider.GetBlockhash(Build.A.BlockHeader.TestObject, blockNumber);
        actualHash.Should().Be(expectedHash);
    }

    [Test]
    public void WithArbosVersion_WithoutExistingArbosState_CreatesArbosStateFirst()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosVersion(ArbosVersion.Thirty);

        context.ArbosState.Should().NotBeNull();
        context.ArbosState.CurrentArbosVersion.Should().Be(ArbosVersion.Thirty);
    }
}
