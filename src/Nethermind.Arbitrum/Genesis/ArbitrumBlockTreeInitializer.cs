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

            string baseDbPath = "/Volumes/Intenso/nethermindProjects/nethermind-arbitrum/.data";
            string genesisStatePath = Path.Combine(baseDbPath, "genesis-state.json");

            ArbitrumGenesisLoader genesisLoader = new(
                chainSpec,
                specProvider,
                specHelper,
                worldStateManager.GlobalWorldState,
                initMessage,
                logManager,
                genesisStatePath);

            Block genesisBlock = genesisLoader.Load();

            // Set total difficulty for genesis block
            genesisBlock.Header.TotalDifficulty = genesisBlock.Header.Difficulty;

            // Insert with options that skip parent checks
            blockTree.Insert(genesisBlock.Header, BlockTreeInsertHeaderOptions.BeaconBlockInsert);
            blockTree.Insert(genesisBlock);

            // Set as head
            blockTree.UpdateMainChain(new[] { genesisBlock }, true);

            return genesisBlock.Header;
        }
    }
}
