// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Evm;
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
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Db;
using Nethermind.Evm.State;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Trie;
using Nethermind.Consensus.Stateless;
using Nethermind.Consensus;
using Nethermind.Arbitrum.Config;

namespace Nethermind.Arbitrum.Execution.Stateless;

public class ArbitrumStatelessBlockProcessingEnv(
    ArbitrumWitness arbWitness,
    ISpecProvider specProvider,
    ISealValidator sealValidator,
    IStylusTargetConfig stylusTargetConfig,
    IArbosVersionProvider arbosVersionProvider,
    ILogManager logManager,
    IArbitrumConfig config)
{
    private IBlockProcessor? _blockProcessor;
    public IBlockProcessor BlockProcessor
    {
        get => _blockProcessor ??= GetBlockProcessor();
    }

    private IWorldState? _worldState;
    public IWorldState WorldState
    {
        get => _worldState ??= new WorldState(
            new TrieStoreScopeProvider(new RawTrieStore(arbWitness.Witness.NodeStorage),
            arbWitness.Witness.CodeDb, logManager), logManager);
    }

    private IWasmStore? _wasmStore;
    public IWasmStore WasmStore
    {
        get => _wasmStore ??= CreateWasmStore();
    }

    private IWasmStore CreateWasmStore()
    {
        WasmDb wasmDb = new(new MemDb());
        WasmStore store = new(wasmDb, stylusTargetConfig, cacheTag: 1);

        // For info, pre-activation is not even needed !
        // If we omit this, wasm store will lazily load wasms from codeDB and compile them to asms when needed during execution.
        //
        // Btw, that's what nitro's debug execution witness endpoint might be doing (didn't see wasms passed there when I last checked).
        // To check but not priority for now.
        if (arbWitness.UserWasms is not null)
        {
            foreach ((ValueHash256 moduleHash, IReadOnlyDictionary<string, byte[]> asmMap) in arbWitness.UserWasms)
            {
                store.ActivateWasm(in moduleHash, asmMap);
            }
        }

        return store;
    }

    private IBlockProcessor GetBlockProcessor()
    {
        StatelessBlockTree statelessBlockTree = new(arbWitness.Witness.DecodedHeaders);
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
            WasmStore,
            new BeaconBlockRootHandler(txProcessor, WorldState),
            logManager,
            new WithdrawalProcessor(WorldState, logManager),
            new ExecutionRequestsProcessor(txProcessor),
            config
        );
    }

    private ITransactionProcessor CreateTransactionProcessor(IWorldState state, StatelessBlockTree blockFinder)
    {
        BlockhashProvider blockhashProvider = new(blockFinder, state, logManager);
        ArbitrumVirtualMachine vm = new(blockhashProvider, WasmStore, specProvider, logManager);
        return new ArbitrumTransactionProcessor(BlobBaseFeeCalculator.Instance, specProvider, state, WasmStore, vm, logManager, new ArbitrumCodeInfoRepository(new EthereumCodeInfoRepository(state), arbosVersionProvider));
    }
}
