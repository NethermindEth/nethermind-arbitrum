// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics.CodeAnalysis;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Stylus;

public interface IWasmStore
{
    public void ActivateWasm(in ValueHash256 moduleHash, IReadOnlyDictionary<string, byte[]> asmMap);
    public (ushort openNow, ushort openEver) AddStylusPages(ushort newPages);
    public CloseOpenedPages AddStylusPagesWithClosing(ushort newPages);
    public void Commit();
    public RecentWasms GetRecentWasms();

    public (ushort openNow, ushort openEver) GetStylusPages();
    public ushort GetStylusPagesOpen();
    public uint GetWasmCacheTag();
    public IReadOnlyCollection<string> GetWasmTargets();
    public void ResetPages();
    public void SetStylusPagesOpen(ushort openNow);
    public bool TryGetActivatedAsm(string target, in ValueHash256 moduleHash, [NotNullWhen(true)] out byte[]? bytes);
    public void WriteActivationToDb(in ValueHash256 moduleHash, IReadOnlyDictionary<string, byte[]> asmMap);
}

public readonly record struct DeleteWasmResult(int DeletedCount, int KeyLengthMismatchCount);
