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
    private readonly ILogger _logger = logManager.GetClassLogger();

    public BlockHeader Initialize(ParsedInitMessage initMessage)
    {
        lock (_lock)
        {
            // If genesis is already set, return it
            BlockHeader? genesisHeader = blockTree.Genesis;
            if (genesisHeader is not null)
            {
                _logger.Info($"Genesis already initialized at block {genesisHeader.Number}");
                return genesisHeader;
            }

            // Check if we're loading from a snapshot that already has the genesis block
            long expectedGenesisBlockNum = (long)specHelper.GenesisBlockNum;

            // Try to find the existing block in the database
            Block? existingGenesisBlock = blockTree.FindBlock(expectedGenesisBlockNum, BlockTreeLookupOptions.None);

            if (existingGenesisBlock is not null)
            {
                _logger.Info($"Found existing block {expectedGenesisBlockNum} in database (hash: {existingGenesisBlock.Hash}), using as genesis");

                // Make sure it's set as the head
                blockTree.UpdateMainChain(new[] { existingGenesisBlock }, true, true);
                blockTree.UpdateHeadBlock(existingGenesisBlock.Hash!);

                // Verify head is set
                Block? headAfterExisting = blockTree.Head;
                if (headAfterExisting == null || headAfterExisting.Number != expectedGenesisBlockNum)
                {
                    _logger.Warn($"Head not set correctly after UpdateMainChain. Current head: {headAfterExisting?.Number}");
                }
                else
                {
                    _logger.Info($"BlockTree head set to block {headAfterExisting.Number}");
                }

                // For Arbitrum, we might need to explicitly mark this as processed
                // The BlockTree.Genesis property might be null if it's not block 0
                // That's okay - what matters is that the block exists and is the head

                return existingGenesisBlock.Header;
            }

            // No existing block - create genesis from scratch
            _logger.Info($"No existing block found at {expectedGenesisBlockNum}, creating new genesis");

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

            _logger.Info($"Inserting genesis block {genesisBlock.Header.Number} into BlockTree...");

            // Insert header first with options that skip parent checks
            blockTree.Insert(genesisBlock.Header, BlockTreeInsertHeaderOptions.BeaconBlockInsert);

            // Insert the full block
            AddBlockResult result = blockTree.Insert(genesisBlock);
            _logger.Info($"Block insert result: {result}");

            // CRITICAL: Set as head - this makes it the current chain tip
            blockTree.UpdateMainChain(new[] { genesisBlock }, true, true);

            // Update head explicitly
            blockTree.UpdateHeadBlock(genesisBlock.Hash!);

            // Verify head is set correctly
            Block? headAfterInsert = blockTree.Head;
            if (headAfterInsert == null)
            {
                throw new InvalidOperationException("Failed to set head - blockTree.Head is null after UpdateMainChain!");
            }

            if (headAfterInsert.Number != expectedGenesisBlockNum)
            {
                throw new InvalidOperationException($"Head block number mismatch: expected {expectedGenesisBlockNum}, got {headAfterInsert.Number}");
            }

            _logger.Info($"BlockTree head successfully set to block {headAfterInsert.Number}, hash: {headAfterInsert.Hash}");

            // Note: For Arbitrum, blockTree.Genesis might be null since genesis is not block 0
            // This is expected behavior - what matters is that block 22207817 exists as the head
            BlockHeader? verifyGenesis = blockTree.Genesis;
            if (verifyGenesis != null)
            {
                _logger.Info($"BlockTree.Genesis is set: {verifyGenesis.Hash}");
            }
            else
            {
                _logger.Info($"BlockTree.Genesis is null (expected for Arbitrum where genesis is block {expectedGenesisBlockNum}, not 0)");

                // Verify the block exists even if Genesis property is null
                Block? verifyBlock = blockTree.FindBlock(expectedGenesisBlockNum, BlockTreeLookupOptions.None);
                if (verifyBlock == null)
                {
                    throw new InvalidOperationException($"Block {expectedGenesisBlockNum} was inserted but cannot be found!");
                }
                _logger.Info($"Verified block {expectedGenesisBlockNum} exists with hash: {verifyBlock.Hash}");
            }

            // ✅ CRITICAL: Ensure state is properly persisted and available
            _logger.Info($"Ensuring genesis state is persisted...");

            // The state should already be committed from ArbitrumGenesisLoader
            // But we need to ensure the WorldStateManager tracks it
            try
            {
                // Force a state root calculation to ensure everything is in the DB
                var stateRoot = worldStateManager.GlobalWorldState.StateRoot;
                _logger.Info($"Genesis state root: {stateRoot}");

                if (stateRoot != genesisBlock.Header.StateRoot)
                {
                    _logger.Error($"State root mismatch! Expected: {genesisBlock.Header.StateRoot}, Got: {stateRoot}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to verify genesis state: {ex.Message}", ex);
            }

            _logger.Info($"Genesis state verification complete");

            return genesisBlock.Header;
        }
    }
}
