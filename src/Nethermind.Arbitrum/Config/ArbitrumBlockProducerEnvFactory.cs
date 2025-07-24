using Nethermind.Arbitrum.Execution;
using Nethermind.Blockchain;
using Nethermind.Blockchain.BeaconBlockRoot;
using Nethermind.Blockchain.Blocks;
using Nethermind.Config;
using Nethermind.Consensus.ExecutionRequests;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Consensus.Rewards;
using Nethermind.Consensus.Validators;
using Nethermind.Consensus.Withdrawals;
using Nethermind.Core.Specs;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Logging;
using Nethermind.State;
using static Nethermind.Arbitrum.Execution.ArbitrumBlockProcessor;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumBlockProducerEnvFactory : BlockProducerEnvFactory
{
    private readonly CachedL1PriceData _cachedL1PriceData;

    public ArbitrumBlockProducerEnvFactory(
        IWorldStateManager worldStateManager,
        IReadOnlyTxProcessingEnvFactory txProcessingEnvFactory,
        IBlockTree blockTree,
        ISpecProvider specProvider,
        IBlockValidator blockValidator,
        IRewardCalculatorSource rewardCalculatorSource,
        IBlockPreprocessorStep blockPreprocessorStep,
        IBlocksConfig blocksConfig,
        IBlockProducerTxSourceFactory blockProducerTxSourceFactory,
        ILogManager logManager,
        CachedL1PriceData cachedL1PriceData) : base(
        worldStateManager,
        txProcessingEnvFactory,
        blockTree,
        specProvider,
        blockValidator,
        rewardCalculatorSource,
        blockPreprocessorStep,
        blocksConfig,
        blockProducerTxSourceFactory,
        logManager)
    {
        _cachedL1PriceData = cachedL1PriceData;
    }

    protected override BlockProcessor CreateBlockProcessor(IReadOnlyTxProcessingScope readOnlyTxProcessingEnv)
    {
        var transactionExecutor = new ArbitrumBlockProductionTransactionsExecutor(
            readOnlyTxProcessingEnv.TransactionProcessor, readOnlyTxProcessingEnv.WorldState,
            new ArbitrumBlockProductionTransactionPicker(_specProvider), _logManager);

        return new ArbitrumBlockProcessor(
            _specProvider,
            _blockValidator,
            _rewardCalculatorSource.Get(readOnlyTxProcessingEnv.TransactionProcessor),
            transactionExecutor,
            readOnlyTxProcessingEnv.TransactionProcessor,
            _cachedL1PriceData,
            readOnlyTxProcessingEnv.WorldState,
            _receiptStorage,
            new BlockhashStore(_specProvider, readOnlyTxProcessingEnv.WorldState),
            new BeaconBlockRootHandler(readOnlyTxProcessingEnv.TransactionProcessor, readOnlyTxProcessingEnv.WorldState),
            _logManager,
            new BlockProductionWithdrawalProcessor(new WithdrawalProcessor(readOnlyTxProcessingEnv.WorldState, _logManager)),
            new ExecutionRequestsProcessor(readOnlyTxProcessingEnv.TransactionProcessor));
    }
}
