// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Blockchain.Tracing;
using Nethermind.Consensus.Processing;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Consensus.Stateless;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Logging;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Execution.Stateless;

public class ArbitrumWitnessCollector(
    WitnessGeneratingHeaderFinder headerFinder,
    WitnessGeneratingWorldState worldState,
    WitnessCapturingTrieStore trieStore,
    IBlockProcessor blockProcessor,
    ISpecProvider specProvider) : IWitnessCollector
{
    public Witness GetWitness(BlockHeader parentHeader, Block block)
    {
        using (worldState.BeginScope(parentHeader))
        {
            // Get the chain ID, both to validate and because the replay binary also gets the chain ID,
            // so we need to populate the recordingdb with preimages for retrieving the chain ID.

            ArbosState arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), NullLogger.Instance);

            UInt256 chainId = arbosState.ChainId.Get();
            ulong genesisBlockNum = arbosState.GenesisBlockNum.Get();
            byte[] chainConfig = arbosState.ChainConfigStorage.Get();

            (Block processed, TxReceipt[] receipts) = blockProcessor.ProcessOne(block, ProcessingOptions.ProducingBlock,
                NullBlockTracer.Instance, specProvider.GetSpec(block.Header));

            (byte[][] stateNodes, byte[][] codes, byte[][] keys) = worldState.GetWitness(parentHeader, trieStore.TouchedNodesRlp);

            return new Witness()
            {
                Headers = headerFinder.GetWitnessHeaders(parentHeader.Hash!),
                Codes = codes,
                State = stateNodes,
                Keys = keys
            };
        }
    }
}
