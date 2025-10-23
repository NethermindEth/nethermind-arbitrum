// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Autofac;
using Autofac.Core;
using Nethermind.Api;
using Nethermind.Api.Extensions;
using Nethermind.Api.Steps;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Init;
using Nethermind.Arbitrum.Modules;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Blocks;
using Nethermind.Blockchain.Headers;
using Nethermind.Blockchain.Synchronization;
using Nethermind.Config;
using Nethermind.Consensus;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Container;
using Nethermind.Core.Specs;
using Nethermind.Db;
using Nethermind.Db.Blooms;
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
using Nethermind.State.Repositories;

namespace Nethermind.Arbitrum;

public class ArbitrumPlugin(ChainSpec chainSpec) : IConsensusPlugin
{
    private ArbitrumNethermindApi _api = null!;
    private IJsonRpcConfig _jsonRpcConfig = null!;
    private IArbitrumSpecHelper _specHelper = null!;

    public string Name => "Arbitrum";
    public string Description => "Nethermind Arbitrum client";
    public string Author => "Nethermind";
    public bool Enabled => chainSpec.SealEngineType == ArbitrumChainSpecEngineParameters.ArbitrumEngineName;
    public IModule Module => new ArbitrumModule(chainSpec);
    public Type ApiType => typeof(ArbitrumNethermindApi);

    public Task Init(INethermindApi api)
    {
        _api = (ArbitrumNethermindApi)api;
        _jsonRpcConfig = api.Config<IJsonRpcConfig>();

        // Load Arbitrum-specific configuration from chainspec
        ArbitrumChainSpecEngineParameters chainSpecParams = chainSpec.EngineChainSpecParametersProvider
            .GetChainSpecParameters<ArbitrumChainSpecEngineParameters>();
        _specHelper = new ArbitrumSpecHelper(chainSpecParams);

        // Only enable Arbitrum module if explicitly enabled in config
        if (_specHelper.Enabled)
            _jsonRpcConfig.EnabledModules = _jsonRpcConfig.EnabledModules.Append(Name).ToArray();

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

        ArbitrumRpcModuleFactory factory = new(
            _api.Context.Resolve<ArbitrumBlockTreeInitializer>(),
            _api.BlockTree,
            _api.ManualBlockProductionTrigger,
            new ArbitrumRpcTxSource(_api.LogManager),
            _api.ChainSpec,
            _specHelper,
            _api.LogManager,
            _api.Context.Resolve<CachedL1PriceData>(),
            _api.BlockProcessingQueue,
            _api.Config<IArbitrumConfig>(),
            _api.Config<IVerifyBlockHashConfig>(),
            _api.EthereumJsonSerializer,
            _api.ProcessExit
        );

        IArbitrumRpcModule arbitrumRpcModule = factory.Create();
        _api.RpcModuleProvider.RegisterSingle(arbitrumRpcModule);

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
            new ArbitrumGasLimitCalculator(),
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
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

public class ArbitrumGasLimitCalculator : IGasLimitCalculator
{
    public long GetGasLimit(BlockHeader parentHeader) => long.MaxValue;
}

public class ArbitrumModule(ChainSpec chainSpec) : Module
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
            .AddStep(typeof(ArbitrumInitializeGenesis))

            .AddDatabase(WasmDb.DbName)
            .AddDecorator<IRocksDbConfigFactory, ArbitrumDbConfigFactory>()
            .AddScoped<IGenesisLoader, ArbitrumNoOpGenesisLoader>()

            .AddSingleton<IWasmDb, WasmDb>()
            .AddSingleton<IWasmStore>(context =>
            {
                IWasmDb wasmDb = context.Resolve<IWasmDb>();
                return new WasmStore(wasmDb, new StylusTargetConfig(), cacheTag: 1);
            })

            .AddSingleton<IBlockTree>(ctx =>
            {
                var specHelper = ctx.Resolve<IArbitrumSpecHelper>();
                long genesisBlockNumber = (long)specHelper.GenesisBlockNum;

                var dbProvider = ctx.Resolve<IDbProvider>();

                return new BlockTree(
                    ctx.Resolve<IBlockStore>(),
                    ctx.Resolve<IHeaderStore>(),
                    dbProvider.BlockInfosDb,
                    dbProvider.MetadataDb,
                    ctx.Resolve<IBadBlockStore>(),
                    ctx.Resolve<IChainLevelInfoRepository>(),
                    ctx.Resolve<ISpecProvider>(),
                    ctx.Resolve<IBloomStorage>(),
                    ctx.Resolve<ISyncConfig>(),
                    ctx.Resolve<ILogManager>(),
                    genesisBlockNumber
                );
            })

            .AddSingleton<ArbitrumBlockTreeInitializer>()

            .AddSingleton<IBlockValidationModule, ArbitrumBlockValidationModule>()
            .AddScoped<ITransactionProcessor, ArbitrumTransactionProcessor>()
            .AddScoped<IBlockProcessor, ArbitrumBlockProcessor>()
            .AddScoped<IVirtualMachine, ArbitrumVirtualMachine>()
            .AddScoped<BlockProcessor.IBlockProductionTransactionPicker, ISpecProvider, IBlocksConfig>((specProvider, blocksConfig) =>
                new ArbitrumBlockProductionTransactionPicker(specProvider))

            .AddSingleton<IBlockProducerEnvFactory, ArbitrumBlockProducerEnvFactory>()
            .AddSingleton<IBlockProducerTxSourceFactory, ArbitrumBlockProducerTxSourceFactory>()
            .AddDecorator<ICodeInfoRepository, ArbitrumCodeInfoRepository>()
            .AddScoped<IArbosVersionProvider, ArbosStateVersionProvider>()
            .AddScoped<ISpecProvider, ArbitrumChainSpecBasedSpecProvider>()
            .AddDecorator<ISpecProvider, ArbitrumDynamicSpecProvider>()
            .AddSingleton<CachedL1PriceData>()

            // Rpcs
            .AddSingleton<ArbitrumEthModuleFactory>()
            .Bind<IRpcModuleFactory<IEthRpcModule>, ArbitrumEthModuleFactory>();
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
