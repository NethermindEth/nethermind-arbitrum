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
using Nethermind.State.Healing;
using Nethermind.Trie.Pruning;

namespace Nethermind.Arbitrum.Genesis;

public class ArbitrumBlockTreeInitializer(
    ChainSpec chainSpec,
    ISpecProvider specProvider,
    IArbitrumSpecHelper specHelper,
    IWorldStateManager worldStateManager,
    IBlockTree blockTree,
    IBlocksConfig blocksConfig,
    INodeStorage nodeStorage,
    IReadOnlyKeyValueStore codeDb,
    IStateReader stateReader,
    ILogManager logManager)
{
    private readonly Lock _lock = new();
    private readonly ILogger _logger = logManager.GetClassLogger();
    private readonly IWorldStateManager _worldStateManager;
    private readonly IBlockTree _blockTree;
    private readonly IBlocksConfig _blocksConfig;
    private readonly INodeStorage _nodeStorage;
    private readonly IReadOnlyKeyValueStore _codeDb;
    private readonly IStateReader _stateReader;
    private readonly ILogManager _logManager;

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
                blockTree.UpdateMainChain(new[] { existingGenesisBlock }, true, true);
                blockTree.UpdateHeadBlock(existingGenesisBlock.Hash!);
                return existingGenesisBlock.Header;
            }

            // No existing block - create genesis from scratch
            _logger.Info($"No existing block found at {expectedGenesisBlockNum}, creating new genesis");

            Block genesisBlock;

            IWorldState actualWorldState = worldStateManager.GlobalWorldState;

            // If it's a HealingWorldState, unwrap it to get the real WorldState
            if (actualWorldState is HealingWorldState healingWs)
            {
                // Use reflection to get the inner WorldState
                var innerField = healingWs.GetType().GetField("_baseWorldState",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (innerField?.GetValue(healingWs) is IWorldState innerWorldState)
                {
                    actualWorldState = innerWorldState;
                    _logger.Info($"Unwrapped HealingWorldState to: {actualWorldState.GetType().FullName}");
                }
            }

            // CRITICAL: Use scope for WorldState operations, but DON'T dispose until after export
            using (IDisposable worldStateCloser = actualWorldState.BeginScope(IWorldState.PreGenesis))
            {
                string baseDbPath = "/Volumes/Intenso/nethermindProjects/nethermind-arbitrum/.data";
                string genesisStatePath = Path.Combine(baseDbPath, "genesis-state.json");

                // Don't pass SnapServer to genesis loader - we'll export here instead
                ArbitrumGenesisLoader genesisLoader = new(
                    chainSpec,
                    specProvider,
                    specHelper,
                    actualWorldState,
                    initMessage,
                    logManager,
                    nodeStorage,
                    null, // Don't export inside the loader
                    codeDb,
                    stateReader,
                    worldStateManager,
                    genesisStatePath);

                genesisBlock = genesisLoader.Load();

                // CRITICAL: Export BEFORE closing the scope!
                _logger.Info("Exporting state BEFORE closing scope...");

                // === DIAGNOSTIC - use actualWorldState ===
                _logger.Info($"Actual WorldState type: {actualWorldState.GetType().FullName}");
                _logger.Info($"SnapServer type: {worldStateManager.SnapServer!.GetType().FullName}");
                _logger.Info($"StateRoot being exported: {actualWorldState.StateRoot}");  // ← Use actualWorldState

                // Try to inspect the internal trie store
                if (actualWorldState is WorldState wsDebug)  // ← Use actualWorldState
                {
                    var stateProviderField = wsDebug.GetType().GetField("_stateProvider",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var stateProvider = stateProviderField?.GetValue(wsDebug);

                    if (stateProvider != null)
                    {
                        var treeField = stateProvider.GetType().GetField("_tree",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var tree = treeField?.GetValue(stateProvider);

                        _logger.Info($"StateProvider type: {stateProvider.GetType().FullName}");
                        _logger.Info($"StateTree type: {tree?.GetType().FullName}");

                        // Get the trie store
                        if (tree != null)
                        {
                            var trieStoreField = tree.GetType().GetProperty("TrieStore",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            var trieStore = trieStoreField?.GetValue(tree);
                            _logger.Info($"TrieStore type: {trieStore?.GetType().FullName}");
                        }
                    }
                }
                // === END DIAGNOSTIC CODE ===

                var exporter = new ArbitrumStateExporter(logManager);
                string exportPath = Path.Combine(baseDbPath, "nethermind-export.json");
                exporter.ExportState(exportPath, actualWorldState);
                _logger.Info($"Direct state export complete");

                // DIAGNOSTIC: Verify accounts exist BEFORE scope closes
                _logger.Info("=== DIAGNOSTIC: Accounts BEFORE scope closes ===");
                if (actualWorldState is WorldState worlds)  // ← Use actualWorldState
                {
                    var testAddresses = new[]
                    {
                        new Address("0x99a0bfdf85951e048954d7c0b55f2e1983a94cbe"),
                        new Address("0x2c42e4a95c0aeb7290a60c8abb110c2e638341c2"),
                        new Address("0x4a79b1932cb93b61db15d6db79cd9e9672bdb84e"),
                    };

                    foreach (var addr in testAddresses)
                    {
                        var account = worlds.GetAccount(addr);
                        _logger.Info($"Account {addr}: Balance={account.Balance}, Nonce={account.Nonce}, IsEmpty={account.IsEmpty}");
                    }
                }

            } // Scope closes here - but state should already be committed to disk

            // DIAGNOSTIC: Verify accounts AFTER scope closes by reading from committed state
            _logger.Info("=== DIAGNOSTIC: Accounts AFTER scope closes ===");
            using (IDisposable readScope = actualWorldState.BeginScope(genesisBlock.Header))  // ← Use actualWorldState
            {
                if (actualWorldState is WorldState ws)  // ← Use actualWorldState
                {
                    var testAddresses = new[]
                    {
                        new Address("0x99a0bfdf85951e048954d7c0b55f2e1983a94cbe"),
                        new Address("0x2c42e4a95c0aeb7290a60c8abb110c2e638341c2"),
                        new Address("0x4a79b1932cb93b61db15d6db79cd9e9672bdb84e"),
                    };

                    foreach (var addr in testAddresses)
                    {
                        var account = ws.GetAccount(addr);
                        _logger.Info($"Account {addr}: Balance={account.Balance}, Nonce={account.Nonce}, IsEmpty={account.IsEmpty}");
                    }
                }
            }

            // Set total difficulty for genesis block
            genesisBlock.Header.TotalDifficulty = genesisBlock.Header.Difficulty;

            _logger.Info($"Inserting genesis block {genesisBlock.Header.Number} into BlockTree...");

            // Insert header first with options that skip parent checks
            blockTree.Insert(genesisBlock.Header, BlockTreeInsertHeaderOptions.BeaconBlockInsert);

            // Insert the full block
            AddBlockResult result = blockTree.Insert(genesisBlock);
            _logger.Info($"Block insert result: {result}");

            // Set as head
            blockTree.UpdateMainChain(new[] { genesisBlock }, true, true);
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

            _logger.Info($"Genesis initialization complete");

            return genesisBlock.Header;
        }
    }
}
