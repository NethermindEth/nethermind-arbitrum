using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Blockchain;
using Nethermind.Blockchain.BeaconBlockRoot;
using Nethermind.Blockchain.Blocks;
using Nethermind.Consensus.ExecutionRequests;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Consensus.Withdrawals;
using Nethermind.Evm;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Init.Steps;
using Nethermind.State;
using System.Collections.Concurrent;
using static Nethermind.Arbitrum.Execution.ArbitrumBlockProcessor;
using static Nethermind.State.PreBlockCaches;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumInitializeBlockchain(ArbitrumNethermindApi api) : InitializeBlockchain(api)
{
    protected override IBlockProductionPolicy CreateBlockProductionPolicy() => AlwaysStartBlockProductionPolicy.Instance;

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

        ArbitrumVirtualMachine virtualMachine = new(
            blockhashProvider,
            api.SpecProvider,
            api.LogManager);

        return virtualMachine;
    }

    protected override ITransactionProcessor CreateTransactionProcessor(ICodeInfoRepository codeInfoRepository, IVirtualMachine virtualMachine, IWorldState worldState)
    {
        if (api.SpecProvider is null) throw new StepDependencyException(nameof(api.SpecProvider));
        if (api.BlockTree is null) throw new StepDependencyException(nameof(api.BlockTree));

        return new ArbitrumTransactionProcessor(
            api.SpecProvider,
            worldState,
            virtualMachine,
            api.BlockTree,
            api.LogManager,
            codeInfoRepository
        );
    }

    protected override BlockProcessor CreateBlockProcessor(BlockCachePreWarmer? preWarmer, ITransactionProcessor transactionProcessor, IWorldState worldState)
    {
        if (api.DbProvider is null) throw new StepDependencyException(nameof(api.DbProvider));
        if (api.RewardCalculatorSource is null) throw new StepDependencyException(nameof(api.RewardCalculatorSource));
        if (api.SpecProvider is null) throw new StepDependencyException(nameof(api.SpecProvider));
        if (api.BlockTree is null) throw new StepDependencyException(nameof(api.BlockTree));
        if (api.ReceiptStorage is null) throw new StepDependencyException(nameof(api.ReceiptStorage));

        return new ArbitrumBlockProcessor(
            api.SpecProvider,
            api.BlockValidator,
            api.RewardCalculatorSource.Get(transactionProcessor),
            //TODO: should use production or validation executor?
            new ArbitrumBlockProductionTransactionsExecutor(transactionProcessor, worldState, new ArbitrumBlockProductionTransactionPicker(api.SpecProvider), api.LogManager),
            worldState,
            api.ReceiptStorage,
            new BlockhashStore(api.SpecProvider, worldState),
            new BeaconBlockRootHandler(transactionProcessor, worldState),
            api.LogManager,
            new WithdrawalProcessor(api.WorldStateManager!.GlobalWorldState, api.LogManager),
            new ExecutionRequestsProcessor(transactionProcessor),
            preWarmer: preWarmer);
    }
}
