// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Stylus;

public interface IWasmDb
{
    bool TryGetActivatedAsm(string target, in ValueHash256 moduleHash, out byte[] bytes);
    void WriteActivation(in ValueHash256 moduleHash, IReadOnlyDictionary<string, byte[]> asmMap);
    void WriteAllActivations(IReadOnlyDictionary<Hash256AsKey, IReadOnlyDictionary<string, byte[]>> wasmMap);

    bool IsEmpty();
    uint GetWasmerSerializeVersion();
    void SetWasmerSerializeVersion(uint version);
    byte GetWasmSchemaVersion();
    void SetWasmSchemaVersion(byte version);
    DeleteWasmResult DeleteWasmEntries(IReadOnlyList<ReadOnlyMemory<byte>> prefixes, int? expectedKeyLength = null);
}
