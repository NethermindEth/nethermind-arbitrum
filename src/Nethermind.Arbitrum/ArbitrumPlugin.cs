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
using Nethermind.Arbitrum.Execution.Stateless;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Modules;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Sequencer.Timeboost;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Consensus;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Arbitrum.Processing;
using Nethermind.Arbitrum.Sequencer.Queues;
using Nethermind.Consensus.Validators;
using Nethermind.Consensus.Stateless;
using Nethermind.Core;
using Nethermind.Core.Caching;
using Nethermind.Core.Container;
using Nethermind.Core.Specs;
using Nethermind.Crypto;
using Nethermind.Db;
using Nethermind.Db.Rocks.Config;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.HealthChecks;
using Nethermind.Init.Modules;
using Nethermind.Init.Steps;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Modules;
using Nethermind.JsonRpc.Modules.Eth;
using Nethermind.JsonRpc.Modules.Eth.FeeHistory;
using Nethermind.JsonRpc.Modules.Eth.GasPrice;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Blockchain.Tracing.GethStyle.Custom.Native;

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

        // Load Arbitrum-specific configuration from chainspec
        ArbitrumChainSpecEngineParameters chainSpecParams = chainSpec.EngineChainSpecParametersProvider
            .GetChainSpecParameters<ArbitrumChainSpecEngineParameters>();
        _specHelper = new ArbitrumSpecHelper(chainSpecParams);

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
        INitroExecutionRpcModule nitroRpcModule = new NitroExecutionRpcModule(engine);
        _api.RpcModuleProvider.RegisterSingle(nitroRpcModule);

        _api.RpcModuleProvider.RegisterBounded(
            _api.Context.Resolve<IRpcModuleFactory<IEthRpcModule>>(),
            _jsonRpcConfig.EthModuleConcurrentInstances ?? Environment.ProcessorCount,
            _jsonRpcConfig.Timeout);

        // Register Arbitrum debug module for MemDb mode (system testing)
        IInitConfig initConfig = _api.Config<IInitConfig>();
        if (initConfig.DiagnosticMode == DiagnosticMode.MemDb)
        {
            IDbProvider dbProvider = _api.Context.Resolve<IDbProvider>();

            if (_api.BlockTree is not IResettableBlockTree resettableBlockTree)
                throw new InvalidOperationException(
                    $"BlockTree must implement IResettableBlockTree for MemDb debug mode. " +
                    $"Actual type: {_api.BlockTree?.GetType().Name ?? "null"}. " +
                    $"Ensure ArbitrumBlockTree is registered in DI.");

            // Resolve all IClearableCache services for auto-discovery
            IEnumerable<IClearableCache> cacheAwareServices = _api.Context.Resolve<IEnumerable<IClearableCache>>();

            // Resolve optional caches not managed by IClearableCache
            IBlockhashCache? blockhashCache = _api.Context.ResolveOptional<IBlockhashCache>();
            PreBlockCaches? preBlockCaches = _api.Context.ResolveOptional<PreBlockCaches>();

            IArbitrumDebugRpcModule debugModule = new ArbitrumDebugRpcModule(
                dbProvider,
                resettableBlockTree,
                cacheAwareServices,
                _api.LogManager,
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

        IBlockProducerEnv producerEnv = _api.BlockProducerEnvFactory.Create();

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
            .AddSingleton<IArbitrumSpecHelper, ArbitrumSpecHelper>()
            .AddSingleton<IClHealthTracker, NoOpClHealthTracker>()
            .AddSingleton<IEngineRequestsTracker, NoOpClHealthTracker>()

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

            .AddSingleton<IBlockTree, ArbitrumBlockTree>()

            .AddSingleton<ArbitrumBlockTreeInitializer>()

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
            // IClearableCache wrapper services for static caches (auto-discovered by debug_reinitialize)
            .AddSingleton<IClearableCache, L1BlockHashCacheService>()
            .AddSingleton<IClearableCache, CalldataUnitsCacheService>()
            .AddSingleton<ArbitrumBlockFactory>()
            .AddSingleton<IArbitrumExecutionEngine, ArbitrumExecutionEngine>()

            .AddScoped<IProcessingStats, ArbitrumProcessingStats>()

            // Rpcs
            .AddSingleton<IFeeHistoryOracle, ArbitrumFeeHistoryOracle>()
            .AddDecorator<IGasPriceOracle, ArbitrumGasPriceOracle>()
            .AddSingleton<ArbitrumEthModuleFactory>()
            .Bind<IRpcModuleFactory<IEthRpcModule>, ArbitrumEthModuleFactory>()

            .AddSingleton<IArbitrumWitnessGeneratingBlockProcessingEnvFactory, ArbitrumWitnessGeneratingBlockProcessingEnvFactory>()
            .Bind<IWitnessGeneratingBlockProcessingEnvFactory, IArbitrumWitnessGeneratingBlockProcessingEnvFactory>()

            .AddSingleton<ArbitrumStatelessBlockProcessingEnvFactory>();

        builder
            .AddModule(new ArbitrumSequencerModule(arbitrumConfig));

        if (blocksConfig.BuildBlocksOnMainState)
            builder.AddSingleton<IBlockProducerEnvFactory, ArbitrumGlobalWorldStateBlockProducerEnvFactory>();
        else
            builder.AddSingleton<IBlockProducerEnvFactory, ArbitrumBlockProducerEnvFactory>();
    }

    private class ArbitrumBlockValidationModule : Module, IBlockValidationModule
    {
        protected override void Load(ContainerBuilder builder) => builder
            .AddScoped((ctx) =>
            {
                return new BlockProcessor.BlockValidationTransactionsExecutor(new BuildUpTransactionProcessorAdapter(ctx.Resolve<ITransactionProcessor>()),
                    ctx.Resolve<IWorldState>(),
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
