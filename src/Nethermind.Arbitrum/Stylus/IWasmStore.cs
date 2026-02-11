// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Diagnostics.CodeAnalysis;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Stylus;

public interface IWasmStore
{
    IReadOnlyCollection<string> GetWasmTargets();
    uint GetWasmCacheTag();

    (ushort openNow, ushort openEver) GetStylusPages();
    ushort GetStylusPagesOpen();
    void SetStylusPagesOpen(ushort openNow);
    CloseOpenedPages AddStylusPagesWithClosing(ushort newPages);
    (ushort openNow, ushort openEver) AddStylusPages(ushort newPages);

    void ActivateWasm(in ValueHash256 moduleHash, IReadOnlyDictionary<string, byte[]> asmMap);
    void WriteActivationToDb(in ValueHash256 moduleHash, IReadOnlyDictionary<string, byte[]> asmMap);
    bool TryGetActivatedAsm(string target, in ValueHash256 moduleHash, [NotNullWhen(true)] out byte[]? bytes);
    RecentWasms GetRecentWasms();
    void ResetPages();
    void Commit();
}

public readonly record struct DeleteWasmResult(int DeletedCount, int KeyLengthMismatchCount);
