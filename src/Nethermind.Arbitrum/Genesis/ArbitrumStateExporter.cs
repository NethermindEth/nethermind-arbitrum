using System.Text.Json;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Evm.State;
using Nethermind.Trie;

namespace Nethermind.Arbitrum.Genesis;

public class ArbitrumStateExporter
{
    private readonly ILogger _logger;

    public ArbitrumStateExporter(ILogManager logManager)
    {
        _logger = logManager.GetClassLogger();
    }

    public void ExportState(string outputPath, IWorldState worldState)
    {
        _logger.Info("Exporting state by traversing the state trie...");

        var accounts = new List<MinimalAccount>();
        StateTree? actualTree = null;

        if (worldState is WorldState ws)
        {
            var stateProviderField = ws.GetType().GetField("_stateProvider",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var stateProvider = stateProviderField?.GetValue(ws);

            if (stateProvider != null)
            {
                var treeField = stateProvider.GetType().GetField("_tree",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var tree = treeField?.GetValue(stateProvider);

                // If it's a HealingStateTree, unwrap to get the base StateTree
                if (tree?.GetType().Name == "HealingStateTree")
                {
                    _logger.Info("Detected HealingStateTree, unwrapping...");

                    var baseTreeField = tree.GetType().GetField("_baseTree",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (baseTreeField != null)
                    {
                        actualTree = baseTreeField.GetValue(tree) as StateTree;
                        if (actualTree != null)
                        {
                            _logger.Info($"Successfully unwrapped to base StateTree");
                        }
                    }

                    if (actualTree == null)
                    {
                        _logger.Warn("Failed to unwrap HealingStateTree, using as-is (will be slower)");
                        actualTree = tree as StateTree;
                    }
                }
                else
                {
                    actualTree = tree as StateTree;
                }

                if (actualTree != null)
                {
                    _logger.Info($"Traversing StateTree with root: {actualTree.RootHash}");

                    var visitor = new AccountCollectorVisitor(accounts, _logger);

                    try
                    {
                        // Use parallel traversal for speed
                        actualTree.Accept(visitor, actualTree.RootHash, new VisitingOptions
                        {
                            MaxDegreeOfParallelism = Environment.ProcessorCount,
                            FullScanMemoryBudget = 8.GiB()
                        });

                        _logger.Info($"Trie traversal complete: found {accounts.Count} accounts");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Trie traversal failed: {ex.Message}", ex);
                    }
                }
                else
                {
                    _logger.Error("Could not access StateTree");
                }
            }
        }

        _logger.Info($"Export complete: {accounts.Count} accounts");

        // Sort by address
        accounts.Sort((a, b) => string.Compare(a.Address, b.Address, StringComparison.OrdinalIgnoreCase));

        // Write to file
        var json = JsonSerializer.Serialize(accounts, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(outputPath, json);

        _logger.Info($"Saved {accounts.Count} accounts to {outputPath}");
    }

    // Custom context that tracks the path (equivalent to iter.LeafKey() in Go)
    private struct PathContext : INodeContext<PathContext>
    {
        public byte[]? PathNibbles { get; private set; }
        public bool IsStorage { get; private set; }

        public PathContext()
        {
            PathNibbles = Array.Empty<byte>();
            IsStorage = false;
        }

        private PathContext(byte[] pathNibbles, bool isStorage)
        {
            PathNibbles = pathNibbles;
            IsStorage = isStorage;
        }

        public PathContext Add(byte nibble)
        {
            byte[] currentPath = PathNibbles ?? Array.Empty<byte>();
            var newPath = new byte[currentPath.Length + 1];
            Array.Copy(currentPath, newPath, currentPath.Length);
            newPath[currentPath.Length] = nibble;
            return new PathContext(newPath, IsStorage);
        }

        public PathContext Add(ReadOnlySpan<byte> path)
        {
            byte[] currentPath = PathNibbles ?? Array.Empty<byte>();
            var newPath = new byte[currentPath.Length + path.Length];
            Array.Copy(currentPath, newPath, currentPath.Length);
            path.CopyTo(newPath.AsSpan(currentPath.Length));
            return new PathContext(newPath, IsStorage);
        }

        public PathContext AddStorage(in ValueHash256 storage)
        {
            // Mark as storage context so we can skip storage tree traversal
            return new PathContext(Array.Empty<byte>(), true);
        }
    }

    private class AccountCollectorVisitor : ITreeVisitor<PathContext>
    {
        private readonly List<MinimalAccount> _accounts;
        private readonly ILogger _logger;
        private int _count = 0;
        private readonly object _lock = new object();

        public AccountCollectorVisitor(List<MinimalAccount> accounts, ILogger logger)
        {
            _accounts = accounts;
            _logger = logger;
        }

        public bool IsFullDbScan => true;
        public bool ExpectAccounts => true;

        public bool ShouldVisit(in PathContext nodeContext, in ValueHash256 nextNode)
        {
            // Skip storage tree traversal - we already have the StorageRoot hash in the account
            return !nodeContext.IsStorage;
        }

        public void VisitTree(in PathContext nodeContext, in ValueHash256 rootHash) { }
        public void VisitMissingNode(in PathContext nodeContext, in ValueHash256 nodeHash) { }
        public void VisitBranch(in PathContext nodeContext, TrieNode node) { }
        public void VisitExtension(in PathContext nodeContext, TrieNode node) { }
        public void VisitLeaf(in PathContext nodeContext, TrieNode node) { }

        public void VisitAccount(in PathContext nodeContext, TrieNode node, in AccountStruct account)
        {
            try
            {
                // Convert nibbles to bytes (2 nibbles = 1 byte)
                // This is equivalent to Go's iter.LeafKey()
                byte[] pathNibbles = nodeContext.PathNibbles ?? Array.Empty<byte>();
                byte[] pathHash = new byte[32];

                for (int i = 0; i < pathNibbles.Length && i < 64; i++)
                {
                    if (i % 2 == 0)
                    {
                        pathHash[i / 2] = (byte)(pathNibbles[i] << 4);
                    }
                    else
                    {
                        pathHash[i / 2] |= pathNibbles[i];
                    }
                }

                var addressHash = "0x" + BitConverter.ToString(pathHash).Replace("-", "").ToLower();

                var minimalAccount = new MinimalAccount
                {
                    Address = addressHash,
                    Nonce = (ulong)account.Nonce,
                    Balance = account.Balance.ToString(),
                    CodeHash = account.CodeHash.ToString(),
                    StorageRoot = account.StorageRoot.ToString()
                };

                lock (_lock)
                {
                    _accounts.Add(minimalAccount);
                    _count++;

                    if (_count % 10000 == 0)
                    {
                        _logger.Info($"Traversed {_count} accounts...");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to collect account: {ex.Message}");
            }
        }
    }

    public class MinimalAccount
    {
        public string Address { get; set; } = string.Empty;
        public ulong Nonce { get; set; }
        public string Balance { get; set; } = "0";
        public string CodeHash { get; set; } = string.Empty;
        public string StorageRoot { get; set; } = string.Empty;
    }
}
