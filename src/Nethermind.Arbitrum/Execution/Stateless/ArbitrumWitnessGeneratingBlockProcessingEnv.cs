// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Blockchain;
using Nethermind.Consensus;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Stateless;
using Nethermind.Arbitrum.Config;
using Nethermind.Core.Specs;
using Nethermind.Logging;
using Nethermind.Core;
using Nethermind.Consensus.Transactions;
using Nethermind.Config;

namespace Nethermind.Arbitrum.Execution.Stateless;

public interface IWitnessGeneratingPolyvalentEnv: IWitnessGeneratingBlockProcessingEnv
{
    IBlockBuildingWitnessCollector CreateBlockBuildingWitnessCollector();
}

public class ArbitrumWitnessGeneratingBlockProcessingEnv(
    ITxSource txSource,
    IBlockchainProcessor chainProcessor,
    IReadOnlyBlockTree blockTree,
    WitnessGeneratingWorldState witnessGeneratingWorldState,
    IBlocksConfig blocksConfig,
    ISpecProvider specProvider,
    IArbitrumSpecHelper specHelper,
    WitnessGeneratingHeaderFinder witnessGenHeaderFinder,
    WitnessCapturingTrieStore witnessCapturingTrieStore,
    ILogManager logManager) : IWitnessGeneratingPolyvalentEnv
{
    public IExistingBlockWitnessCollector CreateExistingBlockWitnessCollector()
    {
        // This is used for overriding the base NMC's debug_executionWitness implementation endpoint
        // Not priority for now
        throw new NotSupportedException($"{nameof(ArbitrumWitnessGeneratingBlockProcessingEnv)} does not support generating witnesses for already existing blocks.");
    }

    public IBlockBuildingWitnessCollector CreateBlockBuildingWitnessCollector()
    {
        Console.WriteLine("--- In Arb WitnessGeneratingBlockProcessingEnv.CreateBlockBuildingWitnessCollector() ---");

        ArbitrumBlockProducer blockProducer = new(
                txSource,
                chainProcessor,
                blockTree,
                witnessGeneratingWorldState,
                new ArbitrumGasPolicyLimitCalculator(),
                NullSealEngine.Instance,
                new ManualTimestamper(),
                specProvider,
                logManager,
                blocksConfig);

        return new ArbitrumWitnessCollector(witnessGenHeaderFinder, witnessGeneratingWorldState, witnessCapturingTrieStore, blockProducer, specProvider, specHelper);
    }
}
