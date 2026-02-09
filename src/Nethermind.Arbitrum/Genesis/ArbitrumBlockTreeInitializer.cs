// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Data;
using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Consensus.Processing;
using Nethermind.Core;
using Nethermind.Core.Events;
using Nethermind.Core.Specs;
using Nethermind.Evm.State;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Genesis;

public class ArbitrumBlockTreeInitializer(
    ISpecProvider specProvider,
    IMainProcessingContext mainProcessingContext,
    IBlockTree blockTree,
    IBlocksConfig blocksConfig,
    ArbitrumGenesisStateInitializer stateInitializer,
    ILogManager logManager)
{
    private readonly Lock _lock = new();

    public BlockHeader Initialize(ParsedInitMessage initMessage)
    {
        lock (_lock)
        {
            BlockHeader? genesisHeader = blockTree.Genesis;
            if (genesisHeader is not null)
            {
                return genesisHeader;
            }

            IWorldState worldState = mainProcessingContext.WorldState;
            using IDisposable worldStateCloser = worldState.BeginScope(IWorldState.PreGenesis);

            ArbitrumGenesisLoader genesisLoader = new(specProvider,
                worldState,
                initMessage,
                stateInitializer,
                logManager);

            Block genesisBlock = genesisLoader.Load();
            Task genesisProcessedTask = Wait.ForEventCondition<BlockEventArgs>(
                CancellationToken.None,
                e => blockTree.NewHeadBlock += e,
                e => blockTree.NewHeadBlock -= e,
                args => args.Block.Header.Hash == genesisBlock.Header.Hash);

            blockTree.SuggestBlock(genesisBlock);

            bool genesisLoaded = genesisProcessedTask.Wait(blocksConfig.GenesisTimeoutMs);
            if (!genesisLoaded)
            {
                throw new TimeoutException($"Genesis block processing timed out after {blocksConfig.GenesisTimeoutMs}ms");
            }

            return genesisBlock.Header;
        }
    }
}
