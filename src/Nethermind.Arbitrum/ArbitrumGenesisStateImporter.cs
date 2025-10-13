using System.Text.Json;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Trie.Pruning;

public class ArbitrumGenesisStateImporter
{
    private readonly IWorldState _worldState;
    private readonly INodeStorage _nodeStorage;
    private readonly ILogManager _logManager;
    private readonly ILogger _logger;
    private int _diagnosticLogCount = 0; // For limiting diagnostic logs

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
            return 0;
        }

        var address = new Address(account.address);

        // Create account
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

        // Import storage
        int storageSlots = 0;
        Hash256 storageRoot;

        if (account.storage != null && account.storage.Count > 0)
        {
            var accountPath = Keccak.Compute(address.Bytes).ValueHash256;
            var storageTreeStore = new RawScopedTrieStore(_nodeStorage, accountPath.ToCommitment());
            var storageTree = new StorageTree(storageTreeStore, Keccak.EmptyTreeHash, _logManager);

            foreach (var kvp in account.storage)
            {
                try
                {
                    var storagePath = new ValueHash256(kvp.Key);
                    var valueHex = kvp.Value.StartsWith("0x") ? kvp.Value[2..] : kvp.Value;
                    var rlpEncodedValue = Convert.FromHexString(valueHex);

                    storageTree.Set(storagePath, rlpEncodedValue, false);
                    storageSlots++;
                }
                catch (Exception ex)
                {
                    _logger.Error($"Storage import failed for {address}: {ex.Message}");
                }
            }

            storageTree.Commit(writeFlags: WriteFlags.DisableWAL);
            storageTree.UpdateRootHash();
            storageRoot = storageTree.RootHash;
        }
        else
        {
            storageRoot = Keccak.EmptyTreeHash;
        }

        // DIAGNOSTIC: Check account BEFORE and AFTER UpdateStorageRoot (only first 3)
        if (_worldState is WorldState ws)
        {
            if (_diagnosticLogCount < 3)
            {
                var before = ws.GetAccount(address);
                _logger.Info($"[DIAGNOSTIC] {address} BEFORE UpdateStorageRoot:");
                _logger.Info($"  Balance={before.Balance}, Nonce={before.Nonce}, CodeHash={before.CodeHash}");

                ws.UpdateStorageRoot(address, storageRoot);

                var after = ws.GetAccount(address);
                _logger.Info($"[DIAGNOSTIC] {address} AFTER UpdateStorageRoot:");
                _logger.Info($"  Balance={after.Balance}, Nonce={after.Nonce}, CodeHash={after.CodeHash}");

                if (after.Balance != before.Balance || after.Nonce != before.Nonce || after.CodeHash != before.CodeHash)
                {
                    _logger.Error($"[DIAGNOSTIC] UpdateStorageRoot DESTROYED account data!");
                }

                _diagnosticLogCount++;
            }
            else
            {
                ws.UpdateStorageRoot(address, storageRoot);
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
        public string? codeHash { get; set; }
        public Dictionary<string, string>? storage { get; set; }
        public string? storageRoot { get; set; }
        public string? accountRlp { get; set; }
    }
}
