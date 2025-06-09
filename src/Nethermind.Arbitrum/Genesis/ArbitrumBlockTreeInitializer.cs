using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Core;
using Nethermind.Core.Events;
using Nethermind.Core.Specs;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.State;

namespace Nethermind.Arbitrum.Genesis;

public class ArbitrumBlockTreeInitializer(
    ChainSpec chainSpec,
    ISpecProvider specProvider,
    IArbitrumSpecHelper specHelper,
    IWorldStateManager worldStateManager,
    IBlockTree blockTree,
    IBlocksConfig blocksConfig,
    ILogManager logManager)
{
    private static readonly Lock _lock = new();
    private bool _isInitialized;

    public Block Initialize(ParsedInitMessage initMessage)
    {
        lock (_lock)
        {
            if (_isInitialized)
            {
                throw new InvalidOperationException("Arbitrum block tree has already been initialized.");
            }

            ArbitrumGenesisLoader genesisLoader = new(
                chainSpec,
                specProvider,
                specHelper,
                worldStateManager.GlobalWorldState,
                initMessage,
                logManager);

            Block genesis = genesisLoader.Load();
            Task genesisProcessedTask = Wait.ForEventCondition<BlockEventArgs>(
                CancellationToken.None,
                e => blockTree.NewHeadBlock += e,
                e => blockTree.NewHeadBlock -= e,
                args => args.Block.Header.Hash == genesis.Header.Hash);

            blockTree.SuggestBlock(genesis);

            genesisProcessedTask.Wait(blocksConfig.GenesisTimeoutMs);

            _isInitialized = true;

            return genesis;
        }
    }
}
