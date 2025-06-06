using System.Collections.Concurrent;
using Nethermind.Arbitrum.Evm;
using Nethermind.Blockchain;
using Nethermind.Consensus.Producers;
using Nethermind.Evm;
using Nethermind.Init.Steps;
using Nethermind.State;
using static Nethermind.State.PreBlockCaches;
using Nethermind.Arbitrum.Precompiles;

namespace Nethermind.Arbitrum.Config;

public class InitializeBlockchainArbitrum(ArbitrumNethermindApi api) : InitializeBlockchain(api)
{
    protected override ICodeInfoRepository CreateCodeInfoRepository(
        ConcurrentDictionary<PrecompileCacheKey, (byte[], bool)>? precompileCache
    )
    {
        return new ArbitrumCodeInfoRepository(new CodeInfoRepository(precompileCache));
    }

    protected override IVirtualMachine CreateVirtualMachine(IWorldState worldState)
    {
        if (api.BlockTree is null) throw new StepDependencyException(nameof(api.BlockTree));
        if (api.SpecProvider is null) throw new StepDependencyException(nameof(api.SpecProvider));
        if (api.WorldStateManager is null) throw new StepDependencyException(nameof(api.WorldStateManager));

        BlockhashProvider blockhashProvider = new(
            api.BlockTree, api.SpecProvider, worldState, api.LogManager);

        ArbVirtualMachine virtualMachine = new(
            blockhashProvider,
            api.SpecProvider,
            api.LogManager);

        return virtualMachine;
    }

    protected override IBlockProductionPolicy CreateBlockProductionPolicy() => AlwaysStartBlockProductionPolicy.Instance;
}
