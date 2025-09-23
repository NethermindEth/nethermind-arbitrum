// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics.CodeAnalysis;
using Nethermind.Core;
using Nethermind.Core.Caching;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Stylus;

/// <summary>
/// Stores compiled Stylus ASM binaries keyed by module hash.
///
/// Unlike Nitro's implementation, this store does not use journaling for ASM entries.
/// This is safe because:
///
/// 1. VISIBILITY CONTROL: Program activation status (Program.Version) is stored in ArbOS
///    state storage, which participates in WorldState snapshots and reverts automatically.
///    A program cannot be executed unless Program.Version > 0, regardless of whether
///    its ASM exists in this store.
///
/// 2. GAS CALCULATION: Gas costs are determined by Program.cached flag in ArbOS state
///    and the block-local RecentWasms cache - both deterministic across all nodes.
///    The actual presence of ASM in this store or native cache does not affect gas.
///
/// 3. EXECUTION CORRECTNESS: ASM is always provided to the WASM runtime during calls.
///    Cache hits improve performance but produce identical execution results as cache misses.
///
/// Consequence of no journaling: If a transaction activates a program and then reverts
/// (including nested call reverts), the ASM bytes may remain in this store as "garbage" -
/// orphaned entries with no valid Program.Version pointing to them. This is acceptable
/// for now because these entries are invisible to execution and do not affect consensus.
/// </summary>
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

        if (Out.IsTargetBlock)
            Out.Log("stylus wasm store reset pages");
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

        if (Out.IsTargetBlock)
            Out.Log($"stylus wasm store set openNow={_openNowWasmPages}");
    }

    public CloseOpenedPages AddStylusPagesWithClosing(ushort newPages)
    {
        (ushort openNow, ushort openEver) = GetStylusPages();
        _openNowWasmPages = Math.Utils.SaturateAdd(openNow, newPages);
        _openEverWasmPages = System.Math.Max(openEver, _openNowWasmPages);

        if (Out.IsTargetBlock)
            Out.Log($"stylus wasm store add pages openNow={_openNowWasmPages} openEver={_openEverWasmPages}");

        return new(openNow, this);
    }

    public (ushort openNow, ushort openEver) AddStylusPages(ushort newPages)
    {
        (ushort openNow, ushort openEver) = GetStylusPages();
        _openNowWasmPages = Math.Utils.SaturateAdd(openNow, newPages);
        _openEverWasmPages = System.Math.Max(openEver, _openNowWasmPages);

        if (Out.IsTargetBlock)
            Out.Log($"stylus wasm store add pages openNow={_openNowWasmPages} openEver={_openEverWasmPages}");

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

        if (Out.IsTargetBlock)
            Out.Log("stylus wasm store committed");
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
