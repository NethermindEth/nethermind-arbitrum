// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Stylus;

public interface IWasmDb
{
    public DeleteWasmResult DeleteWasmEntries(IReadOnlyList<ReadOnlyMemory<byte>> prefixes, int? expectedKeyLength = null);
    public byte[]? Get(ReadOnlySpan<byte> key);
    public Hash256? GetRebuildingPosition();
    public Hash256? GetRebuildingStartBlockHash();
    public uint GetWasmerSerializeVersion();
    public byte GetWasmSchemaVersion();
    public bool IsEmpty();
    public void Set(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value);
    public void SetRebuildingPosition(Hash256 position);
    public void SetRebuildingStartBlockHash(Hash256 blockHash);
    public void SetWasmerSerializeVersion(uint version);
    public void SetWasmSchemaVersion(byte version);
    public bool TryGetActivatedAsm(string target, in ValueHash256 moduleHash, out byte[] bytes);
    public void WriteActivation(in ValueHash256 moduleHash, IReadOnlyDictionary<string, byte[]> asmMap);
    public void WriteAllActivations(IReadOnlyDictionary<Hash256AsKey, IReadOnlyDictionary<string, byte[]>> wasmMap);
}
