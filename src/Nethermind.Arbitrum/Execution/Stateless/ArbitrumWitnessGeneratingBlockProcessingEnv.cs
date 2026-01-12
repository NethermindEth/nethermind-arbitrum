// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Blockchain;
using Nethermind.Blockchain.BeaconBlockRoot;
using Nethermind.Blockchain.Blocks;
using Nethermind.Blockchain.Headers;
using Nethermind.Blockchain.Receipts;
using Nethermind.Consensus;
using Nethermind.Consensus.ExecutionRequests;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Rewards;
using Nethermind.Consensus.Stateless;
using Nethermind.Consensus.Validators;
using Nethermind.Consensus.Withdrawals;
using Nethermind.Core.Specs;
using Nethermind.Evm.State;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Logging;
using Nethermind.State;
using static Nethermind.Arbitrum.Execution.ArbitrumBlockProcessor;

namespace Nethermind.Arbitrum.Execution.Stateless;

public class ArbitrumWitnessGeneratingBlockProcessingEnv(
    ISpecProvider specProvider,
    WorldState baseWorldState,
    IStateReader stateReader,
    // ArbitrumBlockProductionTransactionsExecutor txExecutor,
    WitnessCapturingTrieStore witnessCapturingTrieStore,
    IReadOnlyBlockTree blockTree,
    ISealValidator sealValidator,
    IRewardCalculator rewardCalculator,
    IHeaderStore headerStore,
    IWasmStore wasmStore,
    IArbosVersionProvider arbosVersionProvider,
    ILogManager logManager) : IWitnessGeneratingBlockProcessingEnv
{
    private ITransactionProcessor CreateTransactionProcessor(IWorldState state, IHeaderFinder witnessGeneratingHeaderFinder)
    {
        BlockhashProvider blockhashProvider = new(new BlockhashCache(witnessGeneratingHeaderFinder, logManager), state, logManager);
        // We don't give any l1BlockCache to the vm so that it forces querying the world state
        ArbitrumVirtualMachine vm = new(blockhashProvider, wasmStore, specProvider, logManager);

        return new ArbitrumTransactionProcessor(
            BlobBaseFeeCalculator.Instance, specProvider, state, wasmStore,
            vm, blockTree, logManager,
            new ArbitrumCodeInfoRepository(new EthereumCodeInfoRepository(state), arbosVersionProvider));
    }

    public IWitnessCollector CreateWitnessCollector()
    {
        Console.WriteLine("--- In Arb WitnessGeneratingBlockProcessingEnv.CreateWitnessCollector() ---");
        WitnessGeneratingWorldState state = new(baseWorldState, stateReader);
        WitnessGeneratingHeaderFinder witnessGenHeaderFinder = new(headerStore);
        ITransactionProcessor txProcessor = CreateTransactionProcessor(state, witnessGenHeaderFinder);
        IBlockProcessor.IBlockTransactionsExecutor txExecutor =
            new BlockProcessor.BlockValidationTransactionsExecutor(
                new ExecuteTransactionProcessorAdapter(txProcessor), state);

        // TODO: Might have to create one to use custom/empty wasm store
        // IBlockProcessor.IBlockTransactionsExecutor txExecutor =
        //     new ArbitrumBlockProductionTransactionsExecutor(
        //         txProcessor, state, wasmStore, null, logManager, specProvider);

        IHeaderValidator headerValidator = new HeaderValidator(blockTree, sealValidator, specProvider, logManager);
        IBlockValidator blockValidator = new BlockValidator(new TxValidator(specProvider.ChainId), headerValidator,
            new UnclesValidator(blockTree, headerValidator, logManager), specProvider, logManager);

        ArbitrumBlockProcessor blockProcessor = new(
            specProvider,
            blockValidator,
            rewardCalculator,
            txExecutor,
            txProcessor,
            new CachedL1PriceData(logManager),
            state,
            NullReceiptStorage.Instance,
            new BlockhashStore(state),
            wasmStore,
            new BeaconBlockRootHandler(txProcessor, state),
            logManager,
            new WithdrawalProcessor(state, logManager),
            new ExecutionRequestsProcessor(txProcessor));

        return new ArbitrumWitnessCollector(witnessGenHeaderFinder, state, witnessCapturingTrieStore, blockProcessor, specProvider);
    }
}
