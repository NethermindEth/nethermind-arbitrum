// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Consensus;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Execution;

public sealed class ArbitrumBlockProducerFactory(
    IBlockProducerEnvFactory blockProducerEnvFactory,
    IBlockTree blockTree,
    ISpecProvider specProvider,
    IBlocksConfig blocksConfig,
    IManualBlockProductionTrigger manualBlockProductionTrigger,
    ILogManager logManager)
    : IBlockProducerFactory, IBlockProducerRunnerFactory
{
    public IBlockProducer InitBlockProducer()
    {
        IBlockProducerEnv producerEnv = blockProducerEnvFactory.CreatePersistent();

        return new ArbitrumBlockProducer(
            producerEnv.TxSource,
            producerEnv.ChainProcessor,
            producerEnv.BlockTree,
            producerEnv.ReadOnlyStateProvider,
            new ArbitrumGasPolicyLimitCalculator(),
            NullSealEngine.Instance,
            new ManualTimestamper(),
            specProvider,
            logManager,
            blocksConfig);
    }

    public IBlockProducerRunner InitBlockProducerRunner(IBlockProducer blockProducer) =>
        new StandardBlockProducerRunner(manualBlockProductionTrigger, blockTree, blockProducer);
}
