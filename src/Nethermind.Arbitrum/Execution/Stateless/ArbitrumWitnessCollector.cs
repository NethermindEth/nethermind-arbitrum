// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

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
    Task<(Block Block, ArbitrumWitness Witness)> BuildBlockAndGetWitness(BlockHeader parentHeader, PayloadAttributes payloadAttributes);
}

public class ArbitrumWitnessCollector(
    WitnessGeneratingWorldState worldState,
    IBlockProducer blockProducer,
    ArbitrumUserWasmsRecorder wasmsRecorder,
    ISpecProvider specProvider,
    IArbitrumSpecHelper specHelper) : IBlockBuildingWitnessCollector
{
    public async Task<(Block Block, ArbitrumWitness Witness)> BuildBlockAndGetWitness(BlockHeader parentHeader, PayloadAttributes payloadAttributes)
    {
        Console.WriteLine($"--- In Arb ArbitrumWitnessCollector.BuildBlockAndGetWitness() for block {parentHeader.Number + 1} ---");

        using (worldState.BeginScope(parentHeader))
        {
            ArbosState arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), NullLogger.Instance);

            UInt256 chainId = arbosState.ChainId.Get();
            ulong genesisBlockNum = arbosState.GenesisBlockNum.Get();
            // Chain config not used but still necessary to read to ensure they are included in the witness
            byte[] _ = arbosState.ChainConfigStorage.Get();

            if (chainId != specProvider.ChainId)
                throw new InvalidOperationException($"ArbOS chainId mismatch. ArbOS={chainId}, local={specProvider.ChainId}.");

            if (genesisBlockNum != specHelper.GenesisBlockNum)
                throw new InvalidOperationException($"ArbOS genesisBlockNum mismatch. ArbOS={genesisBlockNum}, local={specHelper.GenesisBlockNum}.");
        }

        Block? producedBlock = await blockProducer.BuildBlock(parentHeader: parentHeader, payloadAttributes: payloadAttributes);
        if (producedBlock?.Hash is null)
            throw new NullReferenceException($"Failed to build block with parent header number: {parentHeader.Number} and hash: {parentHeader.Hash}");

        Witness witness = worldState.GetWitness(parentHeader);
        ArbitrumWitness arbitrumWitness = new(witness, wasmsRecorder.UserWasms);

        return (producedBlock, arbitrumWitness);
    }
}
