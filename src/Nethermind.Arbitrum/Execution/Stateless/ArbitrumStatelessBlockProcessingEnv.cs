// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Blockchain;
using Nethermind.Blockchain.BeaconBlockRoot;
using Nethermind.Blockchain.Blocks;
using Nethermind.Blockchain.Receipts;
using Nethermind.Consensus.ExecutionRequests;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Rewards;
using Nethermind.Consensus.Validators;
using Nethermind.Consensus.Withdrawals;
using Nethermind.Core.Specs;
using Nethermind.Evm.State;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Trie;
using Nethermind.Consensus.Stateless;
using Nethermind.Consensus;

namespace Nethermind.Arbitrum.Execution.Stateless;

public class ArbitrumStatelessBlockProcessingEnv(
    Witness witness,
    ISpecProvider specProvider,
    ISealValidator sealValidator,
    IWasmStore wasmStore,
    IArbosVersionProvider arbosVersionProvider,
    ILogManager logManager)
{
    private IBlockProcessor? _blockProcessor;
    public IBlockProcessor BlockProcessor
    {
        get => _blockProcessor ??= GetProcessor();
    }

    private IWorldState? _worldState;
    public IWorldState WorldState
    {
        get => _worldState ??= new WorldState(
            new TrieStoreScopeProvider(new RawTrieStore(witness.NodeStorage),
            witness.CodeDb, logManager), logManager);
    }

    private IBlockProcessor GetProcessor()
    {
        StatelessBlockTree statelessBlockTree = new(witness.DecodedHeaders);
        ITransactionProcessor txProcessor = CreateTransactionProcessor(WorldState, statelessBlockTree);
        IBlockProcessor.IBlockTransactionsExecutor txExecutor =
            new BlockProcessor.BlockValidationTransactionsExecutor(
                new ExecuteTransactionProcessorAdapter(txProcessor),
                WorldState);

        IHeaderValidator headerValidator = new HeaderValidator(statelessBlockTree, sealValidator, specProvider, logManager);
        IBlockValidator blockValidator = new BlockValidator(new TxValidator(specProvider.ChainId), headerValidator,
            new UnclesValidator(statelessBlockTree, headerValidator, logManager), specProvider, logManager);

        return new ArbitrumBlockProcessor(
            specProvider,
            blockValidator,
            NoBlockRewards.Instance,
            txExecutor,
            txProcessor,
            new CachedL1PriceData(logManager),
            WorldState,
            NullReceiptStorage.Instance,
            new BlockhashStore(WorldState),
            wasmStore,
            new BeaconBlockRootHandler(txProcessor, WorldState),
            logManager,
            new WithdrawalProcessor(WorldState, logManager),
            new ExecutionRequestsProcessor(txProcessor)
        );
    }


    private ITransactionProcessor CreateTransactionProcessor(IWorldState state, StatelessBlockTree blockFinder)
    {
        BlockhashProvider blockhashProvider = new(blockFinder, state, logManager);
        ArbitrumVirtualMachine vm = new(blockhashProvider, wasmStore, specProvider, logManager);
        return new ArbitrumTransactionProcessor(BlobBaseFeeCalculator.Instance, specProvider, state, wasmStore, vm, blockFinder, logManager, new ArbitrumCodeInfoRepository(new EthereumCodeInfoRepository(state), arbosVersionProvider));
    }
}
