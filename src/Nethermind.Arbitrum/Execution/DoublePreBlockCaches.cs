// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core;
using Nethermind.Core.Collections;
using Nethermind.State;

namespace Nethermind.Arbitrum.Execution;

public class DoublePreBlockCaches : IPreBlockCachesInner
{
    private PreBlockCaches _front;
    private PreBlockCaches _back;
    private readonly object _lock = new();

    public DoublePreBlockCaches()
    {
        _front = new PreBlockCaches();
        _back = new PreBlockCaches();
    }

    public PreBlockCaches Front => _front;
    public PreBlockCaches Back => _back;

    public void Swap()
    {
        lock (_lock)
        {
            (_front, _back) = (_back, _front);
            _back.ClearCaches();
        }
    }

    public SeqlockCache<StorageCell, byte[]> GetStorageCache(bool forWriting = true) => forWriting ? _back.StorageCache : _front.StorageCache;

    public SeqlockCache<AddressAsKey, Account> GetStateCache(bool forWriting = true) => forWriting ? _back.StateCache : _front.StateCache;

    public CacheType ClearCaches()
    {
        lock (_lock)
        {
            return _back.ClearCaches();
        }
    }
}
