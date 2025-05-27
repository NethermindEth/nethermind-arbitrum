// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Api;
using Nethermind.Api.Extensions;
using Nethermind.Api.Steps;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Modules;
using Nethermind.Blockchain;
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

public class Arbitrum(ChainSpec chainSpec, IArbitrumConfig arbitrumConfig) : IConsensusPlugin
{
    private const string EngineName = "Arbitrum";

    private INethermindApi _api = null!;
    private IJsonRpcConfig _jsonRpcConfig = null!;
    private ArbitrumRpcTxSource _txSource = null!;

    public string Name => "Arbitrum";
    public string Description => "Nethermind Arbitrum client";
    public string Author => "Nethermind";
    public bool Enabled => chainSpec.SealEngineType == ArbitrumChainSpecEngineParameters.ArbitrumEngineName;

    public IEnumerable<StepInfo> GetSteps()
    {
        yield return typeof(ArbitrumInitializeBlockchainStep);
    }

    public IGenesisLoader GenesisLoader => new ArbitrumGenesisLoader(
        _api.ChainSpec,
        _api.SpecProvider!,
        _api.MainProcessingContext!.WorldState,
        _api.LogManager);

    public Task Init(INethermindApi api)
    {
        _api = api;
        _jsonRpcConfig = api.Config<IJsonRpcConfig>();
        _jsonRpcConfig.EnabledModules = _jsonRpcConfig.EnabledModules.Append(ModuleType.Arbitrum).ToArray();
        _txSource = new ArbitrumRpcTxSource(_api.LogManager.GetClassLogger<ArbitrumRpcTxSource>());

        return Task.CompletedTask;
    }

    public Task InitRpcModules()
    {
        ArgumentNullException.ThrowIfNull(_api.RpcModuleProvider);
        ArgumentNullException.ThrowIfNull(_api.BlockTree);

        ModuleFactoryBase<IArbitrumRpcModule> arbitrumRpcModule = new ArbitrumRpcModuleFactory(
            _api.BlockTree,
            _api.ManualBlockProductionTrigger,
            _txSource,
            _api.ChainSpec,
            arbitrumConfig,
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
