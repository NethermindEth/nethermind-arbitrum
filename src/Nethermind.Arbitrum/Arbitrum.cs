// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Autofac;
using Autofac.Core;
using Nethermind.Api;
using Nethermind.Api.Extensions;
using Nethermind.Api.Steps;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Modules;
using Nethermind.Config;
using Nethermind.Consensus;
using Nethermind.Consensus.Producers;
using Nethermind.Consensus.Transactions;
using Nethermind.Core;
using Nethermind.HealthChecks;
using Nethermind.Init.Steps;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Modules;
using Nethermind.Merge.Plugin.BlockProduction;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum;

public class ArbitrumPlugin(ChainSpec chainSpec) : IConsensusPlugin
{
    private const string EngineName = "Arbitrum";

    private INethermindApi _api = null!;
    private IJsonRpcConfig _jsonRpcConfig = null!;
    private ArbitrumConfig _arbitrumConfig = null!;
    private ArbitrumRpcTxSource _txSource = null!;
    private IArbitrumSpecHelper _specHelper = null!;

    public string Name => "Arbitrum";
    public string Description => "Nethermind Arbitrum client";
    public string Author => "Nethermind";
    public bool Enabled => chainSpec.SealEngineType == Core.SealEngineType.Arbitrum;
    public IModule Module => new ArbitrumModule(chainSpec);

    public IEnumerable<StepInfo> GetSteps()
    {
        yield return typeof(ArbitrumLoadGenesisBlockStep);
        yield return typeof(ArbitrumInitializeBlockchainStep);
    }

    public Task Init(INethermindApi api)
    {
        _api = api;
        _jsonRpcConfig = api.Config<IJsonRpcConfig>();
        // TODO: Remove this after we have a proper way to feed init message into genesis loader
        // Load Arbitrum-specific configuration from chainspec
        ArbitrumChainSpecEngineParameters chainSpecParams = chainSpec.EngineChainSpecParametersProvider
            .GetChainSpecParameters<ArbitrumChainSpecEngineParameters>();
        _specHelper = new ArbitrumSpecHelper(chainSpecParams);

        // Create ArbitrumConfig populated from chainspec via SpecHelper
        _arbitrumConfig = new ArbitrumConfig
        {
            Enabled = _specHelper.Enabled,
            InitialArbOSVersion = _specHelper.InitialArbOSVersion,
            InitialChainOwner = _specHelper.InitialChainOwner,
            GenesisBlockNum = _specHelper.GenesisBlockNum,
            AllowDebugPrecompiles = _specHelper.AllowDebugPrecompiles,
            DataAvailabilityCommittee = _specHelper.DataAvailabilityCommittee,
            MaxCodeSize = _specHelper.MaxCodeSize,
            MaxInitCodeSize = _specHelper.MaxInitCodeSize
        };

        // Only enable Arbitrum module if explicitly enabled in config
        if (_arbitrumConfig.Enabled)
        {
            _jsonRpcConfig.EnabledModules = _jsonRpcConfig.EnabledModules.Append(ModuleType.Arbitrum).ToArray();
        }

        _txSource = new ArbitrumRpcTxSource(_api.LogManager.GetClassLogger<ArbitrumRpcTxSource>());

        // Send initialization message to configure ArbOS with chainspec parameters
        // The init message configures ArbOS with essential chain parameters
        _ = _api.Context.Resolve<ArbitrumRpcBroker>().SendAsync([new ParsedInitMessage(
            _api.ChainSpec.ChainId,
            92,
            null,
            Convert.FromHexString("7b22636861696e4964223a3431323334362c22686f6d657374656164426c6f636b223a302c2264616f466f726b537570706f7274223a747275652c22656970313530426c6f636b223a302c2265697031353048617368223a22307830303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030222c22656970313535426c6f636b223a302c22656970313538426c6f636b223a302c2262797a616e7469756d426c6f636b223a302c22636f6e7374616e74696e6f706c65426c6f636b223a302c2270657465727362757267426c6f636b223a302c22697374616e62756c426c6f636b223a302c226d756972476c6163696572426c6f636b223a302c226265726c696e426c6f636b223a302c226c6f6e646f6e426c6f636b223a302c22636c69717565223a7b22706572696f64223a302c2265706f6368223a307d2c22617262697472756d223a7b22456e61626c654172624f53223a747275652c22416c6c6f774465627567507265636f6d70696c6573223a747275652c2244617461417661696c6162696c697479436f6d6d6974746565223a66616c73652c22496e697469616c4172624f5356657273696f6e223a33322c22496e697469616c436861696e4f776e6572223a22307835453134393764443166303843383762326438464532336539414142366331446538333344393237222c2247656e65736973426c6f636b4e756d223a307d7d"))]);

        return Task.CompletedTask;
    }

    public Task InitRpcModules()
    {
        // Only initialize RPC modules if Arbitrum is enabled
        if (!_arbitrumConfig.Enabled)
        {
            return Task.CompletedTask;
        }

        ArgumentNullException.ThrowIfNull(_api.RpcModuleProvider);
        ArgumentNullException.ThrowIfNull(_api.BlockTree);

        ModuleFactoryBase<IArbitrumRpcModule> arbitrumRpcModule = new ArbitrumRpcModuleFactory(
            _api.BlockTree,
            _api.ManualBlockProductionTrigger,
            _txSource,
            _api.ChainSpec,
            _arbitrumConfig,
            _api.LogManager.GetClassLogger<IArbitrumRpcModule>());

        _api.RpcModuleProvider.RegisterBounded(arbitrumRpcModule, 1, _jsonRpcConfig.Timeout);
        _api.RpcCapabilitiesProvider = new EngineRpcCapabilitiesProvider(_api.SpecProvider);

        return Task.CompletedTask;
    }

    public IBlockProducer InitBlockProducer(ITxSource? additionalTxSource = null)
    {
        StepDependencyException.ThrowIfNull(_api);
        StepDependencyException.ThrowIfNull(_api.WorldStateManager);
        StepDependencyException.ThrowIfNull(_api.BlockTree);
        StepDependencyException.ThrowIfNull(_api.SpecProvider);
        StepDependencyException.ThrowIfNull(_api.BlockValidator);
        StepDependencyException.ThrowIfNull(_api.RewardCalculatorSource);
        StepDependencyException.ThrowIfNull(_api.ReceiptStorage);
        StepDependencyException.ThrowIfNull(_api.TxPool);
        StepDependencyException.ThrowIfNull(_api.TransactionComparerProvider);
        StepDependencyException.ThrowIfNull(_txSource);

        _api.BlockProducerEnvFactory = new BlockProducerEnvFactory(
            _api.WorldStateManager,
            _api.BlockTree,
            _api.SpecProvider,
            _api.BlockValidator,
            _api.RewardCalculatorSource,
            _api.ReceiptStorage,
            _api.BlockPreprocessor,
            _api.TxPool,
            _api.TransactionComparerProvider,
            _api.Config<IBlocksConfig>(),
            _api.LogManager);

        var producerEnv = _api.BlockProducerEnvFactory.Create();

        return new PostMergeBlockProducer(
            _txSource,
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
            .AddSingleton<ArbitrumRpcBroker>()
            .AddSingleton(chainSpecParams)
            .AddSingleton<IArbitrumSpecHelper, ArbitrumSpecHelper>();
    }
}
