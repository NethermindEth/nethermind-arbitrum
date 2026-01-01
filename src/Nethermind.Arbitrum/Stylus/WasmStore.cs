// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics.CodeAnalysis;
using Nethermind.Core.Caching;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Stylus;

public class WasmStore : IWasmStore
{
    private static IWasmStore _store = null!;

    private readonly IWasmDb _db;
    private readonly IStylusTargetConfig _config;
    private readonly uint _cacheTag;

    private readonly Dictionary<Hash256AsKey, IReadOnlyDictionary<string, byte[]>> _wasmChangesOrigin;
    private readonly Dictionary<Hash256AsKey, IReadOnlyDictionary<string, byte[]>>.AlternateLookup<ValueHash256> _wasmChanges;
    private readonly RecentWasms _recentWasms = new();

    private ushort _openNowWasmPages;
    private ushort _openEverWasmPages;

    public WasmStore(IWasmDb db, IStylusTargetConfig config, uint cacheTag)
    {
        _db = db;
        _config = config;
        _cacheTag = cacheTag;

        _wasmChangesOrigin = new(Hash256AsKeyComparer.Instance);
        _wasmChanges = _wasmChangesOrigin.GetAlternateLookup<ValueHash256>();
    }

    public void ResetPages()
    {
        _openEverWasmPages = 0;
        _openNowWasmPages = 0;
    }

    public static IWasmStore Instance
    {
        get => _store ?? throw new InvalidOperationException("WasmStorage is not initialized. Call Initialize first.");
    }

    public IReadOnlyCollection<string> GetWasmTargets()
    {
        return _config.GetWasmTargets();
    }

    public uint GetWasmCacheTag()
    {
        return _cacheTag;
    }

    public (ushort openNow, ushort openEver) GetStylusPages()
    {
        return (_openNowWasmPages, _openEverWasmPages);
    }

    public ushort GetStylusPagesOpen()
    {
        return _openNowWasmPages;
    }

    public void SetStylusPagesOpen(ushort openNow)
    {
        _openNowWasmPages = openNow;
    }

    public CloseOpenedPages AddStylusPagesWithClosing(ushort newPages)
    {
        (ushort openNow, ushort openEver) = GetStylusPages();
        _openNowWasmPages = Math.Utils.SaturateAdd(openNow, newPages);
        _openEverWasmPages = System.Math.Max(openEver, _openNowWasmPages);
        return new(openNow, this);
    }

    public (ushort openNow, ushort openEver) AddStylusPages(ushort newPages)
    {
        (ushort openNow, ushort openEver) = GetStylusPages();
        _openNowWasmPages = Math.Utils.SaturateAdd(openNow, newPages);
        _openEverWasmPages = System.Math.Max(openEver, _openNowWasmPages);
        return new(openNow, openEver);
    }

    public void ActivateWasm(in ValueHash256 moduleHash, IReadOnlyDictionary<string, byte[]> asmMap)
    {
        _wasmChanges.TryAdd(moduleHash, asmMap);
    }

    public void WriteActivationToDb(in ValueHash256 moduleHash, IReadOnlyDictionary<string, byte[]> asmMap)
    {
        _db.WriteActivation(in moduleHash, asmMap);
    }

    public void Commit()
    {
        _db.WriteAllActivations(_wasmChangesOrigin);
        _wasmChangesOrigin.Clear();
        _openNowWasmPages = 0;
        _openEverWasmPages = 0;
    }

    public bool TryGetActivatedAsm(string target, in ValueHash256 moduleHash, [NotNullWhen(true)] out byte[]? bytes)
    {
        if (_wasmChanges.TryGetValue(moduleHash, out IReadOnlyDictionary<string, byte[]>? asmMap))
            if (asmMap.TryGetValue(target, out bytes))
                return true;

        return _db.TryGetActivatedAsm(target, in moduleHash, out bytes);
    }

    public RecentWasms GetRecentWasms()
    {
        return _recentWasms;
    }
}

public readonly ref struct CloseOpenedPages(ushort openNow, IWasmStore store)
{
    public void Dispose()
    {
        store.SetStylusPagesOpen(openNow);
    }
}

// Type for managing recent program access.
// The cache contained is discarded at the end of each block.
// Fixed as per https://github.com/NethermindEth/nethermind-arbitrum/issues/414
// TODO They can't fix it to work properly without introducing a new ArbOS version, so it should stay as is for now
// Offchain Labs related issue https://github.com/OffchainLabs/nitro/pull/4035
public class RecentWasms
{
    private ClockCache<Hash256AsKey, byte>? _cache = null!;

    public bool Insert(in ValueHash256 codeHash, ushort retain)
    {
        // Wild fix for bug in Nitro's RecentWasms
        return false;
    }

    public void Clear()
    {
        _cache?.Clear();
    }
}
