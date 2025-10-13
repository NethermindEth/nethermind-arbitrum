using System.Text.Json;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Trie;
using Nethermind.Trie.Pruning;
using Nethermind.Core.Extensions;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Genesis;

public class ArbitrumGenesisStateImporter
{
    private readonly IWorldState _worldState;
    private readonly INodeStorage _nodeStorage;
    private readonly ILogger _logger;

    public ArbitrumGenesisStateImporter(IWorldState worldState, INodeStorage nodeStorage, ILogManager logManager)
    {
        _worldState = worldState;
        _nodeStorage = nodeStorage;
        _logger = logManager.GetClassLogger();
    }

    public void ImportIfNeeded(string genesisStatePath)
    {
        if (!File.Exists(genesisStatePath))
        {
            _logger.Info($"No genesis state file found at {genesisStatePath}, skipping import");
            return;
        }

        _logger.Info($"Importing raw genesis state from {genesisStatePath}");

        // Get direct access to state tree
        StateTree? stateTree = null;
        if (_worldState is WorldState ws)
        {
            var stateProviderField = ws.GetType().GetField("_stateProvider",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var stateProvider = stateProviderField?.GetValue(ws);

            if (stateProvider != null)
            {
                var treeField = stateProvider.GetType().GetField("_tree",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                stateTree = treeField?.GetValue(stateProvider) as StateTree;
            }
        }

        if (stateTree == null)
        {
            _logger.Error("Could not access StateTree");
            return;
        }

        using var fileStream = File.OpenRead(genesisStatePath);
        var accounts = JsonSerializer.Deserialize<ExportedAccount[]>(fileStream);

        if (accounts == null || accounts.Length == 0)
        {
            _logger.Warn("No accounts found in genesis state file");
            return;
        }

        int accountCount = 0;
        int storageCount = 0;

        foreach (var account in accounts)
        {
            storageCount += ImportAccount(account, stateTree);
            accountCount++;

            if (accountCount % 10000 == 0)
            {
                _logger.Info($"Imported {accountCount} accounts, {storageCount} storage slots...");
            }
        }

        _logger.Info($"Import complete: {accountCount} accounts, {storageCount} storage slots");
    }

    private int ImportAccount(ExportedAccount account, StateTree stateTree)
    {
        if (string.IsNullOrWhiteSpace(account.address))
        {
            return 0;
        }

        // Parse the 32-byte trie path hash - pass as raw bytes
        var triePathBytes = Bytes.FromHexString(account.address);

        // Parse the raw RLP-encoded account data
        var accountRlp = Bytes.FromHexString(account.accountRlp);

        // Write raw account RLP directly - PatriciaTree converts to nibbles internally
        stateTree.Set(triePathBytes.AsSpan(), accountRlp);

        // Store contract code if present
        if (!string.IsNullOrEmpty(account.code))
        {
            var codeHash = new Hash256(account.codeHash);
            var code = Bytes.FromHexString(account.code);
            _nodeStorage.Set(null, TreePath.Empty, codeHash, code);
        }

        // Import storage if present
        int storageSlots = 0;
        if (account.storage != null && account.storage.Count > 0)
        {
            var triePathHash = new Hash256(triePathBytes);
            var storageTreeStore = new RawScopedTrieStore(_nodeStorage, triePathHash);
            var storageTree = new StorageTree(storageTreeStore, Keccak.EmptyTreeHash, NullLogManager.Instance);

            foreach (var kvp in account.storage)
            {
                try
                {
                    var keyBytes = Bytes.FromHexString(kvp.Key);
                    var valueBytes = Bytes.FromHexString(kvp.Value);

                    // Pass raw 32-byte key directly
                    storageTree.Set(keyBytes.AsSpan(), valueBytes);
                    storageSlots++;
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to import storage for {account.address}: {ex.Message}");
                }
            }

            storageTree.Commit(writeFlags: WriteFlags.DisableWAL);
            storageTree.UpdateRootHash();
        }

        return storageSlots;
    }

    public class ExportedAccount
    {
        public string address { get; set; } = string.Empty;
        public ulong nonce { get; set; }
        public string balance { get; set; } = "0";
        public string? code { get; set; }
        public string codeHash { get; set; } = string.Empty;
        public Dictionary<string, string>? storage { get; set; }
        public string storageRoot { get; set; } = string.Empty;
        public string accountRlp { get; set; } = string.Empty;
    }
}
