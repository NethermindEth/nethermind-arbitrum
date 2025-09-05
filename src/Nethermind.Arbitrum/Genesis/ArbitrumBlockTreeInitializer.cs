using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Core;
using Nethermind.Core.Events;
using Nethermind.Core.Specs;
using Nethermind.Evm.State;
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

            using IDisposable worldStateCloser = worldStateManager.GlobalWorldState.BeginScope(IWorldState.PreGenesis);

            ArbitrumGenesisLoader genesisLoader = new(
                chainSpec,
                specProvider,
                specHelper,
                worldStateManager.GlobalWorldState,
                initMessage,
                logManager);

            Block genesisBlock = genesisLoader.Load();
            Task genesisProcessedTask = Wait.ForEventCondition<BlockEventArgs>(
                CancellationToken.None,
                e => blockTree.NewHeadBlock += e,
                e => blockTree.NewHeadBlock -= e,
                args => args.Block.Header.Hash == genesisBlock.Header.Hash);

            blockTree.SuggestBlock(genesisBlock);

            var genesisLoaded = genesisProcessedTask.Wait(blocksConfig.GenesisTimeoutMs);
            if (!genesisLoaded)
            {
                throw new TimeoutException($"Genesis block processing timed out after {blocksConfig.GenesisTimeoutMs}ms");
            }

            return genesisBlock.Header;
        }
    }
}
