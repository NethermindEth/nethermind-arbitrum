using System.Text.Json;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Core.Specs;
using Nethermind.Evm.State;
using Nethermind.Serialization.Rlp;
using Nethermind.State;
using Nethermind.Trie.Pruning;
using Nethermind.Trie;

namespace Nethermind.Arbitrum.Genesis;

public class ArbitrumGenesisStateImporter
{
    private readonly IWorldState _worldState;
    private readonly INodeStorage _nodeStorage;
    private readonly ILogManager _logManager;
    private readonly ILogger _logger;

    public ArbitrumGenesisStateImporter(IWorldState worldState, INodeStorage nodeStorage, ILogManager logManager)
    {
        _worldState = worldState;
        _nodeStorage = nodeStorage;
        _logManager = logManager;
        _logger = logManager.GetClassLogger();
    }

    public void ImportIfNeeded(string genesisStatePath, IReleaseSpec spec)
    {
        if (!File.Exists(genesisStatePath))
        {
            _logger.Info($"No Arbitrum genesis state file found at {genesisStatePath}, skipping import");
            return;
        }

        _logger.Info($"Importing Arbitrum genesis state from {genesisStatePath}");

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
            storageCount += ImportAccount(account, spec);
            accountCount++;

            if (accountCount % 10000 == 0)
            {
                _logger.Info($"Imported {accountCount} accounts, {storageCount} storage slots...");
            }
        }

        _logger.Info($"Import complete: {accountCount} accounts, {storageCount} storage slots");
    }

    private int ImportAccount(ExportedAccount account, IReleaseSpec spec)
    {
        if (string.IsNullOrWhiteSpace(account.address))
        {
            _logger.Warn($"Skipping account with null/empty address");
            return 0;
        }

        var address = new Address(account.address);

        // Create account if needed
        if (!_worldState.AccountExists(address))
        {
            _worldState.CreateAccount(address, UInt256.Zero);
        }

        // Set nonce
        for (ulong i = 0; i < account.nonce; i++)
        {
            _worldState.IncrementNonce(address);
        }

        // Set balance
        if (!string.IsNullOrEmpty(account.balance) && account.balance != "0")
        {
            var balance = UInt256.Parse(account.balance);
            _worldState.AddToBalance(address, balance, spec);
        }

        // Set code
        if (!string.IsNullOrEmpty(account.code))
        {
            var code = Convert.FromHexString(account.code.StartsWith("0x") ? account.code[2..] : account.code);
            _worldState.InsertCode(address, code, spec);
        }

        // Import storage using snap sync approach
        int storageSlots = 0;
        if (account.storage != null && account.storage.Count > 0)
        {
            var accountPath = Keccak.Compute(address.Bytes).ValueHash256;
            var storageTreeStore = new RawScopedTrieStore(_nodeStorage, accountPath.ToCommitment());

            // For genesis import, start with empty tree
            var storageTree = new StorageTree(storageTreeStore, Keccak.EmptyTreeHash, _logManager);

            foreach (var kvp in account.storage)
            {
                try
                {
                    var storagePath = new ValueHash256(kvp.Key);
                    var valueHex = kvp.Value.StartsWith("0x") ? kvp.Value[2..] : kvp.Value;

                    // Values are already RLP-encoded in the JSON, store as-is
                    var rlpEncodedValue = Convert.FromHexString(valueHex);
                    storageTree.Set(storagePath, rlpEncodedValue);

                    storageSlots++;
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to set storage for {address}: key={kvp.Key}, value={kvp.Value}, error={ex.Message}");
                }
            }

            // Commit with WriteFlags.DisableWAL like snap sync does
            storageTree.Commit(writeFlags: WriteFlags.DisableWAL);
            storageTree.UpdateRootHash();
            var storageRoot = storageTree.RootHash;

            _logger.Info($"Storage tree for {address}: root={storageRoot}, slots={storageSlots}");

            // Update the account's storage root
            if (_worldState is WorldState ws)
            {
                ws.UpdateStorageRoot(address, storageRoot);
                _logger.Info($"Updated storage root for {address} to {storageRoot}");
            }
            else
            {
                _logger.Error($"Failed to cast IWorldState to WorldState for {address} - storage root NOT updated!");
            }
        }

        return storageSlots;
    }

    public class ExportedAccount
    {
        public string address { get; set; } = string.Empty;
        public ulong nonce { get; set; }
        public string balance { get; set; } = "0";
        public string? code { get; set; }
        public Dictionary<string, string>? storage { get; set; }
    }
}
