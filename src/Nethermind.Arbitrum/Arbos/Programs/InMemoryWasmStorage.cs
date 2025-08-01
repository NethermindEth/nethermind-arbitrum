// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Concurrent;
using System.Collections.Frozen;
using Nethermind.Arbitrum.Arbos.Stylus;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Arbos.Programs;

public class InMemoryWasmStorage
{
    private static readonly ConcurrentDictionary<ValueHash256, FrozenDictionary<string, byte[]>> ActivatedWasms = new();
    private readonly StylusTargetConfig _config;

    public static InMemoryWasmStorage Instance { get; } = new(new StylusTargetConfig());

    private InMemoryWasmStorage(StylusTargetConfig config)
    {
        _config = config;
    }

    public ushort GetStylusPagesOpen()
    {
        return 0;
    }

    public IReadOnlyCollection<string> GetWasmTargets()
    {
        return _config.GetWasmTargets();
    }

    public void ActivateWasm(ValueHash256 moduleHash, IReadOnlyDictionary<string, byte[]> asmMap)
    {
        ActivatedWasms[moduleHash] = asmMap.ToFrozenDictionary();
    }

    public uint GetWasmCacheTag()
    {
        return 0;
    }

    public (ushort openNow, ushort openEver) GetStylusPages()
    {
        return (0, 0); // OpenEver is max pages opened ever, OpenNow is the current open pages
    }

    public RecentWasms GetRecentWasms()
    {
        return new RecentWasms();
    }

    public CloseOpenedPages AddStylusPages(ushort newPages)
    {
        return new(0, this);
    }

    public void SetStylusPagesOpen(ushort openNow)
    {
    }

    public bool TryGetActivatedAsm(string localTarget, ValueHash256 moduleHash, out byte[] bytes)
    {
        bytes = [];
        return false;
    }

    public void WriteActivation(ValueHash256 moduleHash, IReadOnlyDictionary<string, byte[]> asmMap)
    {
    }
}

public readonly ref struct CloseOpenedPages(ushort openNow, InMemoryWasmStorage storage)
{
    public void Dispose()
    {
        storage.SetStylusPagesOpen(openNow);
    }
}

// Type for managing recent program access.
// The cache contained is discarded at the end of each block.
public class RecentWasms
{
    public bool Insert(ValueHash256 codeHash, ushort retain)
    {
        return false;
    }
}
