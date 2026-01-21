// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Consensus.Stateless;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
using Nethermind.Logging;
using Nethermind.Int256;
using Nethermind.Consensus;
using Nethermind.Consensus.Producers;
using Nethermind.Core.Specs;

namespace Nethermind.Arbitrum.Execution.Stateless;

public interface IBlockBuildingWitnessCollector
{
    Task<(Block Block, Witness Witness)> BuildBlockAndGetWitness(BlockHeader parentHeader, PayloadAttributes payloadAttributes);
}

public class ArbitrumWitnessCollector(
    WitnessGeneratingHeaderFinder headerFinder,
    WitnessGeneratingWorldState worldState,
    WitnessCapturingTrieStore trieStore,
    IBlockProducer blockProducer,
    ISpecProvider specProvider,
    IArbitrumSpecHelper specHelper) : IBlockBuildingWitnessCollector
{
    public async Task<(Block Block, Witness Witness)> BuildBlockAndGetWitness(BlockHeader parentHeader, PayloadAttributes payloadAttributes)
    {
        using (worldState.BeginScope(parentHeader))
        {
            ArbosState arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), NullLogger.Instance);

            UInt256 chainId = arbosState.ChainId.Get();
            ulong genesisBlockNum = arbosState.GenesisBlockNum.Get();
            byte[] chainConfig = arbosState.ChainConfigStorage.Get();

            if (chainId != specProvider.ChainId)
                throw new InvalidOperationException($"ArbOS chainId mismatch. ArbOS={chainId}, local={specProvider.ChainId}.");

            if (genesisBlockNum != specHelper.GenesisBlockNum)
                throw new InvalidOperationException($"ArbOS genesisBlockNum mismatch. ArbOS={genesisBlockNum}, local={specHelper.GenesisBlockNum}.");
        }

        Block? producedBlock = await blockProducer.BuildBlock(parentHeader: parentHeader, payloadAttributes: payloadAttributes);
        if (producedBlock?.Hash is null)
            throw new NullReferenceException($"Failed to build block with parent header number: {parentHeader.Number} and hash: {parentHeader.Hash}");

        (byte[][] stateNodes, byte[][] codes, byte[][] keys) = worldState.GetWitness(parentHeader, trieStore.TouchedNodesRlp);

        Witness witness = new()
        {
            Headers = headerFinder.GetWitnessHeaders(parentHeader.Hash!),
            Codes = codes,
            State = stateNodes,
            Keys = keys
        };

        return (producedBlock, witness);
    }
}
