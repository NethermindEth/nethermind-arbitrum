// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.State;

namespace Nethermind.Arbitrum.Test.Genesis;

/// <summary>
/// Tests that genesis state initialization does not leave stale pre-genesis entries
/// in the prewarmer's shared PreBlockCaches (see the invariant comment in
/// ArbitrumGenesisStateInitializer.InitializeAndBuildGenesisBlock).
/// </summary>
public class GenesisPrewarmerCacheTests
{
    private const ulong InitialArbosVersion = 32;

    [Test]
    public void Build_WithPreBlockCaches_ClearsCachesAfterStateInit()
    {
        PreBlockCaches preBlockCaches = new();
        IWorldState worldState = CreateWorldState(preBlockCaches);
        ArbitrumGenesisBuilder builder = CreateBuilder(worldState);

        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);
        builder.Build();

        AddressAsKey systemAccountKey = ArbosAddresses.ArbosSystemAccount;
        preBlockCaches.StateCache.TryGetValue(in systemAccountKey, out _).Should().BeFalse(
            "the pre-init guard's empty-state read of the system account must not survive genesis build");

        ReadArbosVersion(worldState).Should().Be(InitialArbosVersion,
            "this mirrors ArbitrumDynamicSpecProvider.GetSpec during genesis block processing — " +
            "a stale cache serves version 0 here, which is the production 'ArbOS uninitialized' failure");
    }

    [Test]
    public void Build_WithAndWithoutPreBlockCaches_ProducesIdenticalGenesisBlocks()
    {
        IWorldState plainWorldState = CreateWorldState(preBlockCaches: null);
        ArbitrumGenesisBuilder plainBuilder = CreateBuilder(plainWorldState);

        PreBlockCaches preBlockCaches = new();
        IWorldState prewarmedWorldState = CreateWorldState(preBlockCaches);
        ArbitrumGenesisBuilder prewarmedBuilder = CreateBuilder(prewarmedWorldState);

        Block plainGenesis;
        using (plainWorldState.BeginScope(IWorldState.PreGenesis))
        {
            plainGenesis = plainBuilder.Build();
        }

        Block prewarmedGenesis;
        using (prewarmedWorldState.BeginScope(IWorldState.PreGenesis))
        {
            prewarmedGenesis = prewarmedBuilder.Build();
        }

        plainGenesis.Header.Should().BeEquivalentTo(prewarmedGenesis.Header);
    }

    private static ArbitrumGenesisBuilder CreateBuilder(IWorldState worldState)
    {
        ChainSpec chainSpec = FullChainSimulationChainSpecProvider.Create(InitialArbosVersion);
        ArbitrumChainSpecEngineParameters parameters = chainSpec.EngineChainSpecParametersProvider
            .GetChainSpecParameters<ArbitrumChainSpecEngineParameters>();
        parameters.SerializedChainConfig = Encoding.UTF8.GetString(
            FullChainSimulationInitMessage.GetSerializedChainConfigBase64Bytes());

        IArbitrumSpecHelper specHelper = new ArbitrumSpecHelper(parameters, new DisabledArbOsVersionOverride());
        ISpecProvider specProvider = FullChainSimulationChainSpecProvider.CreateDynamicSpecProvider(chainSpec);
        ArbitrumGenesisStateInitializer stateInitializer = new(chainSpec, specHelper, new ArbitrumConfig(), LimboLogs.Instance);

        return new ArbitrumGenesisBuilder(
            chainSpec, specProvider, specHelper, worldState, stateInitializer, LimboLogs.Instance);
    }

    private static IWorldState CreateWorldState(PreBlockCaches? preBlockCaches)
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        return preBlockCaches is null
            ? worldState
            : new WorldState(
                new PrewarmerScopeProvider(worldState.ScopeProvider, preBlockCaches, LimboLogs.Instance, isPrewarmer: false),
                LimboLogs.Instance);
    }

    private static ulong ReadArbosVersion(IWorldState worldState)
    {
        ArbosStorage rootStorage = new(worldState, new SystemBurner(readOnly: false), ArbosAddresses.ArbosSystemAccount);
        return new ArbosStorageBackedULong(rootStorage, ArbosStateOffsets.VersionOffset).Get();
    }
}
