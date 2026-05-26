// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Autofac;
using Autofac.Core;
using Nethermind.Api;
using Nethermind.Api.Extensions;
using Nethermind.Api.Steps;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Compression;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Core;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Receipts;
using Nethermind.Arbitrum.Execution.Stateless;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Modules;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Rpc;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Sequencer.Timeboost;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Blocks;
using Nethermind.Blockchain.Headers;
using Nethermind.Config;
using Nethermind.Consensus;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Arbitrum.Processing;
using Nethermind.Arbitrum.Sequencer.Queues;
using Nethermind.Consensus.Tracing;
using Nethermind.Consensus.Validators;
using Nethermind.Consensus.Stateless;
using Nethermind.Core;
using Nethermind.Core.Caching;
using Nethermind.Core.Container;
using Nethermind.Core.Specs;
using Nethermind.Db;
using Nethermind.Db.Rocks.Config;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.HealthChecks;
using Nethermind.Init;
using Nethermind.Init.Modules;
using Nethermind.State.Repositories;
using Nethermind.Init.Steps;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Modules;
using Nethermind.JsonRpc.Modules.Eth;
using Nethermind.JsonRpc.Modules.Eth.FeeHistory;
using Nethermind.JsonRpc.Modules.Eth.GasPrice;
using Nethermind.Logging;
using Nethermind.Serialization.Json;
using Nethermind.Serialization.Rlp;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Blockchain.Tracing.GethStyle.Custom.Native;
using Nethermind.Blockchain.FullPruning;
using Nethermind.Trie.Pruning;
using Nethermind.Core.Crypto;
using Nethermind.History;

namespace Nethermind.Arbitrum;

public class ArbitrumPlugin(ChainSpec chainSpec, IBlocksConfig blocksConfig, IArbitrumConfig arbitrumConfig) : IConsensusPlugin
{
    private ArbitrumNethermindApi _api = null!;
    private IJsonRpcConfig _jsonRpcConfig = null!;
    private IArbitrumSpecHelper _specHelper = null!;

    public string Name => "Arbitrum";
    public string Description => "Nethermind Arbitrum client";
    public string Author => "Nethermind";
    public bool Enabled => chainSpec.SealEngineType == ArbitrumChainSpecEngineParameters.ArbitrumEngineName;
    public IModule Module => new ArbitrumModule(chainSpec, blocksConfig, arbitrumConfig);
    public Type ApiType => typeof(ArbitrumNethermindApi);

    public Task Init(INethermindApi api)
    {
        _api = (ArbitrumNethermindApi)api;
        _jsonRpcConfig = api.Config<IJsonRpcConfig>();

        // Register polymorphic JSON converter for receipts to ensure ArbitrumReceiptForRpc
        // properties (GasUsedForL1, MultiGasUsed) are serialized correctly
        EthereumJsonSerializer.AddConverter(new ReceiptForRpcPolymorphicConverter());

        // Load Arbitrum-specific configuration from chainspec
        ArbitrumChainSpecEngineParameters chainSpecParams = chainSpec.EngineChainSpecParametersProvider
            .GetChainSpecParameters<ArbitrumChainSpecEngineParameters>();
        _specHelper = new ArbitrumSpecHelper(chainSpecParams, new DisabledArbOsVersionOverride());

        // Register Arbitrum-specific tracers
        GethLikeNativeTracerFactory.RegisterTracer(
            TxGasDimensionLoggerTracer.TracerName,
            static (options, block, tx, _) => new TxGasDimensionLoggerTracer(tx, block, options));
        GethLikeNativeTracerFactory.RegisterTracer(
            TxGasDimensionByOpcodeTracer.TracerName,
            static (options, block, tx, _) => new TxGasDimensionByOpcodeTracer(tx, block, options));

        return Task.CompletedTask;
    }

    public Task InitRpcModules()
    {
        ArgumentNullException.ThrowIfNull(_api.RpcModuleProvider);
        ArgumentNullException.ThrowIfNull(_api.BlockTree);
        ArgumentNullException.ThrowIfNull(_api.SpecProvider);
        ArgumentNullException.ThrowIfNull(_api.BlockProcessingQueue);

        // Only initialize RPC modules if Arbitrum is enabled
        if (!_specHelper.Enabled)
            return Task.CompletedTask;

        IArbitrumExecutionEngine engine = _api.Context.Resolve<IArbitrumExecutionEngine>();

        if (arbitrumConfig.SequencerEnabled)
        {
            SequencerState sequencerState = _api.Context.Resolve<SequencerState>();
            sequencerState.Activate();
        }

        // Wrap engine with comparison decorator if verification is enabled
        IVerifyBlockHashConfig verifyBlockHashConfig = _api.Config<IVerifyBlockHashConfig>();
        if (verifyBlockHashConfig.Enabled)
        {
            if (string.IsNullOrWhiteSpace(verifyBlockHashConfig.ArbNodeRpcUrl))
                throw new InvalidOperationException("Block hash verification is enabled but ArbNodeRpcUrl is not specified. Please configure VerifyBlockHash.ArbNodeRpcUrl or disable verification.");

            ILogger logger = _api.LogManager.GetClassLogger<ArbitrumPlugin>();
            if (logger.IsInfo)
                logger.Info($"Block hash verification enabled: verify every {verifyBlockHashConfig.VerifyEveryNBlocks} blocks, url={verifyBlockHashConfig.ArbNodeRpcUrl}");

            engine = new ArbitrumExecutionEngineWithComparison(
                engine,
                verifyBlockHashConfig,
                _api.EthereumJsonSerializer,
                _api.LogManager,
                _api.ProcessExit);
        }

        // Register Arbitrum RPC module
        IArbitrumRpcModule arbitrumRpcModule = new ArbitrumRpcModule(engine);
        _api.RpcModuleProvider.RegisterSingle(arbitrumRpcModule);

        // Register nitroexecution namespace
        ArbitrumClHealthTracker clHealthTracker = _api.Context.Resolve<ArbitrumClHealthTracker>();
        _ = clHealthTracker.StartAsync();
        INitroExecutionRpcModule nitroRpcModule = new NitroExecutionRpcModule(engine, clHealthTracker);
        _api.RpcModuleProvider.RegisterSingle(nitroRpcModule);

        _api.RpcModuleProvider.RegisterBounded(
            _api.Context.Resolve<IRpcModuleFactory<IArbitrumEthRpcModule>>(),
            _jsonRpcConfig.EthModuleConcurrentInstances ?? Environment.ProcessorCount,
            _jsonRpcConfig.Timeout);

        // Register Arbitrum debug module for system/comparison testing
        if (arbitrumConfig.EnableTestReset)
        {
            IDbProvider dbProvider = _api.Context.Resolve<IDbProvider>();

            if (_api.BlockTree is not IArbitrumResettableBlockTree resettableBlockTree)
                throw new InvalidOperationException(
                    $"BlockTree must implement IArbitrumResettableBlockTree for MemDb debug mode. " +
                    $"Actual type: {_api.BlockTree?.GetType().Name ?? "null"}. " +
                    $"Ensure ArbitrumBlockTree is registered in DI.");

            // Resolve all IClearableCache services for auto-discovery
            IEnumerable<IClearableCache> cacheAwareServices = _api.Context.Resolve<IEnumerable<IClearableCache>>();

            // Resolve optional caches not managed by IClearableCache
            IBlockhashCache? blockhashCache = _api.Context.ResolveOptional<IBlockhashCache>();
            PreBlockCaches? preBlockCaches = _api.Context.ResolveOptional<PreBlockCaches>();
            IArbOSVersionOverride arbosVersionOverride = _api.Context.Resolve<IArbOSVersionOverride>();

            IArbitrumDebugRpcModule debugModule = new ArbitrumDebugRpcModule(
                dbProvider,
                resettableBlockTree,
                cacheAwareServices,
                _api.LogManager,
                arbosVersionOverride,
                _api.Context.ResolveOptional<IHistoryPruner>(),
                blockhashCache,
                preBlockCaches);
            _api.RpcModuleProvider.RegisterSingle(debugModule);
        }

        return Task.CompletedTask;
    }

    public IBlockProducer InitBlockProducer()
    {
        StepDependencyException.ThrowIfNull(_api);
        StepDependencyException.ThrowIfNull(_api.WorldStateManager);
        StepDependencyException.ThrowIfNull(_api.BlockTree);
        StepDependencyException.ThrowIfNull(_api.SpecProvider);
        StepDependencyException.ThrowIfNull(_api.TransactionComparerProvider);

        IBlockProducerEnv producerEnv = _api.BlockProducerEnvFactory.CreatePersistent();

        return new ArbitrumBlockProducer(
            producerEnv.TxSource,
            producerEnv.ChainProcessor,
            producerEnv.BlockTree,
            producerEnv.ReadOnlyStateProvider,
            new ArbitrumGasPolicyLimitCalculator(),
            NullSealEngine.Instance,
            new ManualTimestamper(),
            _api.SpecProvider,
            _api.LogManager,
            _api.Config<IBlocksConfig>());
    }

    public IBlockProducerRunner InitBlockProducerRunner(IBlockProducer blockProducer)
    {
        StepDependencyException.ThrowIfNull(_api.BlockTree);

        return new StandardBlockProducerRunner(_api.ManualBlockProductionTrigger, _api.BlockTree, blockProducer);
    }

    public void InitTxTypesAndRlpDecoders(INethermindApi api)
    {
        // Register Arbitrum-specific RLP decoders (receipts with MultiGas support)
        Rlp.RegisterDecoders(typeof(ArbitrumReceiptStorageDecoder).Assembly, true);

        TxDecoder.Instance.RegisterDecoder(new ArbitrumInternalTxDecoder());
        TxDecoder.Instance.RegisterDecoder(new ArbitrumSubmitRetryableTxDecoder());
        TxDecoder.Instance.RegisterDecoder(new ArbitrumRetryTxDecoder());
        TxDecoder.Instance.RegisterDecoder(new ArbitrumDepositTxDecoder());
        TxDecoder.Instance.RegisterDecoder(new ArbitrumUnsignedTxDecoder());
        TxDecoder.Instance.RegisterDecoder(new ArbitrumContractTxDecoder());

        api.RegisterTxType<ArbitrumInternalTransactionForRpc>(new ArbitrumInternalTxDecoder(), Always.Valid);
        api.RegisterTxType<ArbitrumDepositTransactionForRpc>(new ArbitrumDepositTxDecoder(), Always.Valid);
        api.RegisterTxType<ArbitrumUnsignedTransactionForRpc>(new ArbitrumUnsignedTxDecoder(), Always.Valid);
        api.RegisterTxType<ArbitrumRetryTransactionForRpc>(new ArbitrumRetryTxDecoder(), Always.Valid);
        api.RegisterTxType<ArbitrumSubmitRetryableTransactionForRpc>(new ArbitrumSubmitRetryableTxDecoder(), Always.Valid);
        api.RegisterTxType<ArbitrumContractTransactionForRpc>(new ArbitrumContractTxDecoder(), Always.Valid);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

public class ArbitrumGasPolicyLimitCalculator : IGasLimitCalculator
{
    public long GetGasLimit(BlockHeader parentHeader) => long.MaxValue;
}

public class ArbitrumModule(ChainSpec chainSpec, IBlocksConfig blocksConfig, IArbitrumConfig arbitrumConfig) : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        ArbitrumChainSpecEngineParameters chainSpecParams = chainSpec.EngineChainSpecParametersProvider
            .GetChainSpecParameters<ArbitrumChainSpecEngineParameters>();

        builder
            .AddSingleton<NethermindApi, ArbitrumNethermindApi>()
            .AddSingleton(chainSpecParams)
            .AddSingleton<IArbOSVersionOverride, ArbOSVersionOverride>()
            .AddSingleton<IArbitrumSpecHelper, ArbitrumSpecHelper>()
            .AddSingleton<ArbitrumClHealthTracker>()
            .Bind<IClHealthTracker, ArbitrumClHealthTracker>()

            .AddStep(typeof(ArbitrumInitializeBlockchain))
            .AddStep(typeof(ArbitrumInitializeWasmDb))
            .AddStep(typeof(ArbitrumInitializeStylusNative))
            .AddStep(typeof(StartExpressLaneTracker))

            .AddDatabase(WasmDb.DbName)
            .AddDecorator<IRocksDbConfigFactory, ArbitrumDbConfigFactory>()
            .AddSingleton<ArbitrumGenesisStateInitializer>()
            .AddScoped<IGenesisBuilder, ArbitrumGenesisBuilder>();

        // When GenesisStateUnavailable=true (comparison mode), use no-op loader to skip genesis
        // Otherwise, use the standard loader chain without the wrapper overhead
        if (chainSpec.GenesisStateUnavailable)
        {
            builder.AddSingleton<IGenesisLoader, NoOpGenesisLoader>();
        }

        builder
            .AddSingleton<IWasmDb, WasmDb>()
            .AddSingleton<IStylusTargetConfig, StylusTargetConfig>()
            .AddScoped<IWasmStore, IWasmDb, IStylusTargetConfig>((db, config) => new WasmStore(db, config, cacheTag: 1))

            .AddSingleton<ArbitrumBlockTreeInitializer>()

            // IBlockhashStore is used only in ArbitrumBlockProcessor and we pass a NoOp because ApplyBlockhashStateChanges
            // should not get called as disabled in nitro and is taken care of in tx processor ProcessParentBlockHash()
            .AddScoped<IBlockhashStore, NoOpBlockhashStore>()
            .AddScoped<IBlockhashProvider, ArbitrumBlockhashProvider>()
            .AddSingleton<IBlockValidationModule, ArbitrumBlockValidationModule>()
            .AddScoped<ITransactionProcessor, ArbitrumTransactionProcessor>()
            .AddScoped<IBlockProcessor, ArbitrumBlockProcessor>()
            .AddScoped<IL1BlockCache, L1BlockCache>()
            .AddScoped<IVirtualMachine<ArbitrumGasPolicy>, ArbitrumVirtualMachine>()
            .AddScoped<BlockProcessor.IBlockProductionTransactionPicker, ISpecProvider, IBlocksConfig>((specProvider, blocksConfig) =>
                new ArbitrumBlockProductionTransactionPicker(specProvider))

            .AddSingleton<IBlockProducerTxSourceFactory, ArbitrumBlockProducerTxSourceFactory>()
            .AddDecorator<ICodeInfoRepository, ArbitrumCodeInfoRepository>()
            .AddScoped<IArbosVersionProvider>(ctx =>
            {
                ArbitrumChainSpecEngineParameters parameters = ctx.Resolve<ArbitrumChainSpecEngineParameters>();
                IWorldStateScopeProvider? scopeProvider = ctx.ResolveOptional<IWorldStateScopeProvider>();
                if (scopeProvider is null)
                    return new ArbosStateVersionProvider(parameters);

                IWorldState worldState = ctx.Resolve<IWorldState>();
                return new ArbosStateVersionProvider(parameters, worldState);
            })
            .AddScoped<ISpecProvider, ArbitrumChainSpecBasedSpecProvider>()
            .AddDecorator<ISpecProvider, ArbitrumDynamicSpecProvider>()
            .AddSingleton<CachedL1PriceData>()
            // IClearableCache services (auto-discovered by debug_reinitialize)
            .AddSingleton<IClearableCache, L1BlockHashCacheService>()
            .AddSingleton<IClearableCache, CalldataUnitsCacheService>()
            //.AddSingleton<IClearableCache, MainPruningTrieStoreFactory>(factory => (IClearableCache)factory.PruningTrieStore)
            .AddSingleton<IClearableCache, CacheCodeInfoRepository.CacheClearService>()
            .AddSingleton<IClearableCache, IHeaderStore>(store => (IClearableCache)store)
            .AddSingleton<IClearableCache, IBlockStore>(store => (IClearableCache)store)
            .AddSingleton<IClearableCache, IChainLevelInfoRepository>(repo => (IClearableCache)repo)
            .AddSingleton<ArbitrumBlockFactory>()
            .AddSingleton<IArbitrumExecutionEngine, ArbitrumExecutionEngine>()
            .AddDecorator<IHistoryPruner, ArbitrumHistoryPruner>()

            .AddScoped<IProcessingStats, ArbitrumProcessingStats>()

            // Rpcs
            .AddSingleton<IFeeHistoryOracle, ArbitrumFeeHistoryOracle>()
            .AddDecorator<IGasPriceOracle, ArbitrumGasPriceOracle>();

        if (arbitrumConfig.ConsensusNodeRpcEnabled)
        {
            if (!Uri.TryCreate(arbitrumConfig.ConsensusNodeRpcUrl, UriKind.Absolute, out Uri? consensusUri) ||
                (consensusUri.Scheme != Uri.UriSchemeHttp && consensusUri.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException(
                    $"{nameof(ArbitrumConfig.ConsensusNodeRpcUrl)} must be a valid absolute http/https URL when {nameof(ArbitrumConfig.ConsensusNodeRpcEnabled)} is true. " +
                    $"Configured value: '{arbitrumConfig.ConsensusNodeRpcUrl}'.");

            builder.AddSingleton<IArbitrumConsensusClient, ArbitrumConsensusClient>();
        }
        else
            builder.AddSingleton<IArbitrumConsensusClient, DisabledArbitrumConsensusClient>();

        builder.AddSingleton<IBlockMetadataProvider, BlockMetadataProvider>();

        builder
            .AddSingleton<ArbitrumEthModuleFactory>()
            .Bind<IRpcModuleFactory<IArbitrumEthRpcModule>, ArbitrumEthModuleFactory>()
            .Bind<IRpcModuleFactory<IEthRpcModule>, ArbitrumEthModuleFactory>()
            .AddScoped<IGethStyleTracer, ArbitrumGethStyleTracer>();

        builder
            .AddModule(new ArbitrumValidatorModule(arbitrumConfig))
            .AddModule(new ArbitrumSequencerModule(arbitrumConfig));

        if (arbitrumConfig.EnableTestReset)
        {
            // Test/comparison mode: wrap in resettable decorator for debug_reinitialize.
            // Must be transient (not singleton) so Func<ArbitrumBlockTree> creates fresh instances on reset.
            builder.RegisterType<ArbitrumBlockTree>().AsSelf();
            builder.Register(c => new ResettableArbitrumBlockTree(c.Resolve<Func<ArbitrumBlockTree>>()))
                .As<IBlockTree>()
                .As<IArbitrumResettableBlockTree>()
                .SingleInstance();
        }
        else
        {
            // Production mode: use ArbitrumBlockTree directly
            builder.AddSingleton<IBlockTree, ArbitrumBlockTree>();
        }

        if (blocksConfig.BuildBlocksOnMainState)
            builder.AddSingleton<IBlockProducerEnvFactory, ArbitrumGlobalWorldStateBlockProducerEnvFactory>();
        else
            builder.AddSingleton<IBlockProducerEnvFactory, ArbitrumBlockProducerEnvFactory>();
    }

    private sealed class NoOpBlockhashStore : IBlockhashStore
    {
        public void ApplyBlockhashStateChanges(BlockHeader blockHeader, IReleaseSpec spec) { }
        public Hash256? GetBlockHashFromState(BlockHeader currentBlockHeader, long requiredBlockNumber, IReleaseSpec spec) => null;
    }

    private class ArbitrumValidatorModule(IArbitrumConfig arbitrumConfig) : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder
                // Always needed: witness factory for debug_executionWitness endpoint
                .AddSingleton<ReconstructedStateTrieStore>(ctx => new ReconstructedStateTrieStore(new MemDb(), ctx.Resolve<MainPruningTrieStoreFactory>().PruningTrieStore.AsReadOnly()))
                .AddSingleton<IArbitrumWitnessGeneratingBlockProcessingEnvFactory, ArbitrumWitnessGeneratingBlockProcessingEnvFactory>()
                .Bind<IWitnessGeneratingBlockProcessingEnvFactory, IArbitrumWitnessGeneratingBlockProcessingEnvFactory>()
                .AddSingleton<ArbitrumStatelessBlockProcessingEnvFactory>();

            if (arbitrumConfig.ValidationEnabled)
                builder
                    .AddSingleton<ArbitrumStateReconstructionBlockProcessingEnvFactory>()
                    .AddSingleton<IStateReconstructor, StateReconstructor>()
                    .AddSingleton<IFullPrunerFactory, ArbitrumFullPrunerFactory>();
            else
                builder
                    .AddSingleton<IStateReconstructor, NoOpStateReconstructor>();
        }
    }

    private class ArbitrumBlockValidationModule : Module, IBlockValidationModule
    {
        protected override void Load(ContainerBuilder builder) => builder
            .AddScoped<IBlockProcessor.IBlockTransactionsExecutor>((ctx) =>
            {
                return new ArbitrumBlockValidationTransactionsExecutor(
                    new BuildUpTransactionProcessorAdapter(ctx.Resolve<ITransactionProcessor>()),
                    ctx.Resolve<IWorldState>(),
                    ctx.Resolve<ILogManager>(),
                    ctx.ResolveOptional<BlockProcessor.BlockValidationTransactionsExecutor.ITransactionProcessedEventHandler>());
            });
    }

    private class ArbitrumSequencerModule(IArbitrumConfig arbitrumConfig) : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            if (arbitrumConfig.TimeboostEnabled && string.IsNullOrWhiteSpace(arbitrumConfig.TimeboostAuctionContractAddress))
                throw new InvalidOperationException(
                    "Timeboost is enabled but TimeboostAuctionContractAddress is not configured. " +
                    "Please set Arbitrum.TimeboostAuctionContractAddress or disable Timeboost.");

            builder
                .AddSingleton<SequencerState>()
                .AddSingleton<DelayedMessageQueue>()
                .AddSingleton<TransactionQueue>(c => new TransactionQueue(
                    c.Resolve<IArbitrumConfig>(),
                    c.Resolve<IExpressLaneTracker>(),
                    timeProvider: TimeProvider.System))
                .AddSingleton<IRoundTimingInfo>(c => new RoundTimingInfo(
                    c.Resolve<IArbitrumConfig>(),
                    offset: DateTime.UnixEpoch,
                    timeProvider: TimeProvider.System));

            if (arbitrumConfig.SequencerEnabled)
                builder
                    .AddSingleton<IArbitrumSequencerEngine, ArbitrumSequencerEngine>()
                    .AddSingleton<ArbitrumSequencerBlockSuggester>()
                    .AddSingleton<IArbitrumSequencerBlockSuggester>(c => c.Resolve<ArbitrumSequencerBlockSuggester>())
                    .AddSingleton<IProducedBlockSuggester>(c => c.Resolve<ArbitrumSequencerBlockSuggester>());
            else
                builder
                    .AddSingleton<IArbitrumSequencerEngine, DisabledArbitrumSequencerEngine>()
                    .AddSingleton<DisabledArbitrumSequencerBlockSuggester>()
                    .AddSingleton<IArbitrumSequencerBlockSuggester>(c => c.Resolve<DisabledArbitrumSequencerBlockSuggester>());

            if (arbitrumConfig.TimeboostEnabled)
                builder
                    .AddSingleton<IAuctionContract, AuctionContract>()
                    .AddSingleton<IExpressLaneTracker, ExpressLaneTracker>()
                    .AddSingleton<IAuctionResolutionQueue, AuctionResolutionQueue>()
                    .AddSingleton<IExpressLaneService, ExpressLaneService>();
            else
                builder
                    .AddSingleton<IExpressLaneTracker, DisabledExpressLaneTracker>()
                    .AddSingleton<IAuctionResolutionQueue, DisabledAuctionResolutionQueue>()
                    .AddSingleton<IExpressLaneService, DisabledExpressLaneService>();
        }
    }
}
