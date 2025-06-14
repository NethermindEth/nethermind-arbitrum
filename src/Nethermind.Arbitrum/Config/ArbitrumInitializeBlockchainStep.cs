using Nethermind.Api;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Blockchain.BeaconBlockRoot;
using Nethermind.Blockchain.Blocks;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Consensus.Withdrawals;
using Nethermind.Evm;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Init.Steps;
using Nethermind.Serialization.Rlp;
using Nethermind.State;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumInitializeBlockchainStep(INethermindApi api) : InitializeBlockchain(api)
{
    protected override async Task InitBlockchain()
    {
        await base.InitBlockchain();

        TxDecoder.Instance.RegisterDecoder(new ArbitrumInternalTxDecoder<Transaction>());
        TxDecoder.Instance.RegisterDecoder(new ArbitrumSubmitRetryableTxDecoder<Transaction>());
    }

    protected override IBlockProductionPolicy CreateBlockProductionPolicy() => AlwaysStartBlockProductionPolicy.Instance;

    protected override ITransactionProcessor CreateTransactionProcessor(CodeInfoRepository codeInfoRepository, IVirtualMachine virtualMachine, IWorldState worldState)
    {
        if (api.SpecProvider is null) throw new StepDependencyException(nameof(api.SpecProvider));

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

        return new ArbitrumBlockProcessor(
            api.SpecProvider,
            api.BlockValidator,
            api.RewardCalculatorSource.Get(transactionProcessor),
            //TODO: should use production or validation executor?
            new BlockProcessor.BlockProductionTransactionsExecutor(transactionProcessor, worldState, new ArbitrumBlockProductionTransactionPicker(api.SpecProvider), api.LogManager),
            worldState,
            api.ReceiptStorage,
            transactionProcessor,
            new BlockhashStore(api.SpecProvider, worldState),
            new BeaconBlockRootHandler(transactionProcessor, worldState),
            api.LogManager,
            new WithdrawalProcessor(api.WorldStateManager!.GlobalWorldState, api.LogManager),
            preWarmer: preWarmer);
    }
}
