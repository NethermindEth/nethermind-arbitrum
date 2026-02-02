// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Autofac;
using Autofac.Core;
using Nethermind.Api;
using Nethermind.Api.Extensions;
using Nethermind.Api.Steps;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Core;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Stateless;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Modules;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Consensus;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Consensus.Validators;
using Nethermind.Consensus.Stateless;
using Nethermind.Core;
using Nethermind.Core.Container;
using Nethermind.Core.Specs;
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
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Blockchain.Tracing.GethStyle.Custom.Native;

namespace Nethermind.Arbitrum;

public class ArbitrumPlugin(ChainSpec chainSpec, IBlocksConfig blocksConfig) : IConsensusPlugin
{
    private ArbitrumNethermindApi _api = null!;
    private IJsonRpcConfig _jsonRpcConfig = null!;
    private IArbitrumSpecHelper _specHelper = null!;

    public string Name => "Arbitrum";
    public string Description => "Nethermind Arbitrum client";
    public string Author => "Nethermind";
    public bool Enabled => chainSpec.SealEngineType == ArbitrumChainSpecEngineParameters.ArbitrumEngineName;
    public IModule Module => new ArbitrumModule(chainSpec, blocksConfig);
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

        // in ArbitrumRpcModuleFactory:
        // _api.Context.Resolve<IArbitrumWitnessGeneratingBlockProcessingEnvFactory>(),

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

public class ArbitrumModule(ChainSpec chainSpec, IBlocksConfig blocksConfig) : Module
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

            .AddDatabase(WasmDb.DbName)
            .AddDecorator<IRocksDbConfigFactory, ArbitrumDbConfigFactory>()
            .AddSingleton<ArbitrumGenesisStateInitializer>()
            .AddScoped<IGenesisBuilder, ArbitrumGenesisBuilder>()

            .AddSingleton<IWasmDb, WasmDb>()
            .AddSingleton<IStylusTargetConfig, StylusTargetConfig>()
            .AddSingleton<IWasmStore>(context =>
            {
                IWasmDb wasmDb = context.Resolve<IWasmDb>();
                IStylusTargetConfig stylusTargetConfig = context.Resolve<IStylusTargetConfig>();
                return new WasmStore(wasmDb, stylusTargetConfig, cacheTag: 1);
            })

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
            .AddSingleton<IArbitrumExecutionEngine, ArbitrumExecutionEngine>()

            // Rpcs
            .AddSingleton<ArbitrumEthModuleFactory>()
            .Bind<IRpcModuleFactory<IEthRpcModule>, ArbitrumEthModuleFactory>()

            .AddSingleton<IArbitrumWitnessGeneratingBlockProcessingEnvFactory, ArbitrumWitnessGeneratingBlockProcessingEnvFactory>()
            .Bind<IWitnessGeneratingBlockProcessingEnvFactory, IArbitrumWitnessGeneratingBlockProcessingEnvFactory>();

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
}
