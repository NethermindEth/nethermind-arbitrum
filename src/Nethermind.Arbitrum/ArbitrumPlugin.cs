// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Autofac;
using Autofac.Core;
using Nethermind.Api;
using Nethermind.Api.Extensions;
using Nethermind.Api.Steps;
using Nethermind.Arbitrum.Arbos.Stylus;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Modules;
using Nethermind.Config;
using Nethermind.Consensus;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.HealthChecks;
using Nethermind.Init.Steps;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Modules;
using Nethermind.JsonRpc.Modules.Eth.FeeHistory;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;
using Nethermind.Specs.ChainSpecStyle;

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

    public IEnumerable<StepInfo> GetSteps()
    {
        yield return typeof(ArbitrumLoadGenesisBlockStep);
        yield return typeof(ArbitrumInitializeBlockchain);
    }

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
            _jsonRpcConfig.EnabledModules = _jsonRpcConfig.EnabledModules.Append(ModuleType.Arbitrum).ToArray();

        StylusTargets.PopulateStylusTargetCache(new StylusTargetConfig()); // TODO: Load StylusTargetConfig from ArbitrumConfig file

        return Task.CompletedTask;
    }

    public Task InitRpcModules()
    {
        ArgumentNullException.ThrowIfNull(_api.RpcModuleProvider);
        ArgumentNullException.ThrowIfNull(_api.BlockTree);
        ArgumentNullException.ThrowIfNull(_api.SpecProvider);
        ArgumentNullException.ThrowIfNull(_api.BlockProcessingQueue);

        // Only initialize RPC modules if Arbitrum is enabled
        if (!_specHelper.Enabled) return Task.CompletedTask;

        ModuleFactoryBase<IArbitrumRpcModule> arbitrumRpcModule = new ArbitrumRpcModuleFactory(
            _api.Context.Resolve<ArbitrumBlockTreeInitializer>(),
            _api.BlockTree,
            _api.ManualBlockProductionTrigger,
            new ArbitrumRpcTxSource(_api.LogManager),
            _api.ChainSpec,
            _specHelper,
            _api.LogManager,
            _api.Context.Resolve<CachedL1PriceData>(),
            _api.BlockProcessingQueue,
            _api.Config<IArbitrumConfig>()
        );

        _api.RpcModuleProvider.RegisterBounded(arbitrumRpcModule, 1, _jsonRpcConfig.Timeout);

        FeeHistoryOracle feeHistoryOracle = new FeeHistoryOracle(
            _api.BlockTree, _api.ReceiptStorage, _api.SpecProvider);

        ArbitrumEthModuleFactory arbitrumEthFactory = new(
            _api.TxPool,
            _api.TxSender,
            _api.Wallet,
            _api.BlockTree,
            _jsonRpcConfig,
            _api.LogManager,
            _api.StateReader,
            _api,
            _api.SpecProvider,
            _api.ReceiptStorage,
            _api.GasPriceOracle,
            _api.EthSyncingInfo,
            feeHistoryOracle,
            _api.ProtocolsManager,
            _api.Config<IBlocksConfig>().SecondsPerSlot);

        _api.RpcModuleProvider.RegisterBounded(arbitrumEthFactory,
            _jsonRpcConfig.EthModuleConcurrentInstances ?? Environment.ProcessorCount,
            _jsonRpcConfig.Timeout);

        _api.RpcCapabilitiesProvider = new EngineRpcCapabilitiesProvider(_api.SpecProvider);

        return Task.CompletedTask;
    }

    public IBlockProducer InitBlockProducer()
    {
        StepDependencyException.ThrowIfNull(_api);
        StepDependencyException.ThrowIfNull(_api.WorldStateManager);
        StepDependencyException.ThrowIfNull(_api.BlockTree);
        StepDependencyException.ThrowIfNull(_api.SpecProvider);
        StepDependencyException.ThrowIfNull(_api.BlockValidator);
        StepDependencyException.ThrowIfNull(_api.RewardCalculatorSource);
        StepDependencyException.ThrowIfNull(_api.TransactionComparerProvider);

        BlockProducerEnv producerEnv = _api.BlockProducerEnvFactory.Create();

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
            .AddSingleton<ArbitrumBlockTreeInitializer>()
            .AddScoped<ITransactionProcessor, ArbitrumTransactionProcessor>()
            .AddScoped<IVirtualMachine, ArbitrumVirtualMachine>()
            .AddSingleton<IBlockProducerEnvFactory, ArbitrumBlockProducerEnvFactory>()
            .AddSingleton<IBlockProducerTxSourceFactory, ArbitrumBlockProducerTxSourceFactory>()

            .AddWithAccessToPreviousRegistration<ISpecProvider>((ctx, factory) =>
            {
                ArbosState? arbosState = ctx.ResolveOptional<ArbosState>();
                if (arbosState is not null)
                {
                    IArbosVersionProvider arbosVersionProviderFactory() => arbosState;
                    return new ArbitrumChainSpecBasedSpecProvider(chainSpec, arbosVersionProviderFactory, ctx.Resolve<ILogManager>());
                }
                else
                {
                    ArbitrumChainSpecEngineParameters chainSpecParams = ctx.Resolve<ArbitrumChainSpecEngineParameters>();
                    IArbosVersionProvider arbosVersionProviderFactory() => chainSpecParams;
                    return new ArbitrumChainSpecBasedSpecProvider(chainSpec, arbosVersionProviderFactory, ctx.Resolve<ILogManager>());
                }
            })

            .AddSingleton<CachedL1PriceData>();
    }
}
