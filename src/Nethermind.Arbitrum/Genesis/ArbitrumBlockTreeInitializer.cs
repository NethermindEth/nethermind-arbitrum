using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Db;
using Nethermind.Evm.State;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.State;

namespace Nethermind.Arbitrum.Genesis;

/// <summary>
/// Initializes BlockTree for Arbitrum with a non-zero genesis block
/// Assumes BlockTree was constructed with the correct genesisBlockNumber parameter
/// </summary>
public class ArbitrumBlockTreeInitializer(
    ChainSpec chainSpec,
    ISpecProvider specProvider,
    IArbitrumSpecHelper specHelper,
    IWorldStateManager worldStateManager,
    IBlockTree blockTree,
    INodeStorage nodeStorage,
    IDb codeDb,
    ILogManager logManager)
{
    private readonly ILogger _logger = logManager.GetClassLogger();

    public BlockHeader Initialize(ParsedInitMessage initMessage)
    {
        long expectedGenesisBlockNum = (long)specHelper.GenesisBlockNum;

        // Check if genesis already exists
        BlockHeader? existingGenesis = blockTree.Genesis;
        if (existingGenesis is not null)
        {
            _logger.Info($"Genesis already initialized at block {existingGenesis.Number}");
            return existingGenesis;
        }

        // Check if block already exists in database
        Block? existingBlock = blockTree.FindBlock(expectedGenesisBlockNum, BlockTreeLookupOptions.None);
        if (existingBlock is not null)
        {
            _logger.Info($"Found existing block {expectedGenesisBlockNum} in database");
            // Just set it as the head and we're done
            blockTree.UpdateMainChain(new[] { existingBlock }, true, true);
            return existingBlock.Header;
        }

        _logger.Info($"Creating new genesis at block {expectedGenesisBlockNum}...");

        // Load genesis state and create block
        IWorldState worldState = worldStateManager.GlobalWorldState;

        ArbitrumGenesisLoader genesisLoader = new(
            chainSpec,
            specProvider,
            specHelper,
            worldState,
            initMessage,
            logManager,
            nodeStorage);

        Block genesisBlock = genesisLoader.Load();

        _logger.Info($"Genesis block created: {genesisBlock.Number}, hash: {genesisBlock.Hash}");

        // Use BlockTree's normal SuggestBlock - it now handles non-zero genesis
        AddBlockResult result = blockTree.SuggestBlock(genesisBlock, BlockTreeSuggestOptions.ShouldProcess);

        if (result == AddBlockResult.Added || result == AddBlockResult.AlreadyKnown)
        {
            _logger.Info($"Genesis block suggested successfully: {result}");
            blockTree.UpdateMainChain(new[] { genesisBlock }, true, true);

            // CRITICAL: Flush the state cache to disk
            _logger.Info("Flushing WorldStateManager cache to disk...");

            worldStateManager.FlushCache(CancellationToken.None);

            _logger.Info("✓ WorldStateManager cache flushed");

            // Also flush code DB
            _logger.Info("Flushing code DB...");
            codeDb.Flush();
            _logger.Info("✓ Code DB flushed");

            // Wait for filesystem
            System.Threading.Thread.Sleep(1000);

            _logger.Info("✓ All data persisted - safe to snapshot now");

            return genesisBlock.Header;
        }
        else
        {
            _logger.Info($"Genesis block failed: {result}");
            throw new InvalidOperationException($"Failed to suggest genesis block: {result}");
        }
    }
}
