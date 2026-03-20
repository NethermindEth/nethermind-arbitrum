// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core.Caching;
using Nethermind.Init;
using Nethermind.Logging;
using Nethermind.Trie.Pruning;

namespace Nethermind.Arbitrum.Modules;

/// <summary>
/// Wraps the TrieStore as an IClearableCache so that debug_reinitialize
/// can automatically clear its internal state between comparison tests.
/// Without this, dirty nodes, commit sets, and block number tracking
/// leak between test runs on the same worker process.
/// </summary>
public sealed class TrieStoreClearableCache(
    MainPruningTrieStoreFactory trieStoreFactory,
    ILogManager logManager) : IClearableCache
{
    private readonly ILogger _logger = logManager.GetClassLogger<TrieStoreClearableCache>();

    public void ClearCache()
    {
        if (trieStoreFactory.PruningTrieStore is TrieStore concreteTrieStore)
        {
            concreteTrieStore.ClearForTesting();
            if (_logger.IsDebug)
                _logger.Debug("TrieStore internal state cleared for test isolation");
        }
        else
        {
            if (_logger.IsWarn)
                _logger.Warn($"Cannot clear TrieStore: unexpected type {trieStoreFactory.PruningTrieStore.GetType().Name}");
        }
    }
}
