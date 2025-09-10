// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Buffers.Binary;
using Autofac.Features.AttributeFilters;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Db;

namespace Nethermind.Arbitrum.Stylus;

public class WasmDb([KeyFilter(WasmDb.DbName)] IDb db) : IWasmDb
{
    public const string DbName = "wasm";

    public bool TryGetActivatedAsm(string target, in ValueHash256 moduleHash, out byte[] bytes)
    {
        byte[] key = WasmStoreSchema.GetActivatedKey(target, moduleHash);
        byte[]? asm = db.Get(key.AsSpan());
        bytes = asm ?? [];
        return asm != null;
    }

    public void WriteActivation(in ValueHash256 moduleHash, IReadOnlyDictionary<string, byte[]> asmMap)
    {
        foreach ((string target, byte[] asm) in asmMap)
        {
            byte[] key = WasmStoreSchema.GetActivatedKey(target, in moduleHash);
            db.Set(key, asm);
        }
    }

    public void WriteAllActivations(IReadOnlyDictionary<Hash256AsKey, IReadOnlyDictionary<string, byte[]>> wasms)
    {
        using IWriteBatch batch = db.StartWriteBatch();
        foreach ((Hash256AsKey moduleHash, IReadOnlyDictionary<string, byte[]> asmMap) in wasms)
        {
            foreach ((string target, byte[] asm) in asmMap)
            {
                byte[] key = WasmStoreSchema.GetActivatedKey(target, in moduleHash.Value.ValueHash256);
                batch.Set(key, asm);
            }
        }
    }

    public bool IsEmpty()
    {
        return !db.GetAllValues().Any();
    }

    public uint GetWasmerSerializeVersion()
    {
        byte[]? value = db.Get(WasmStoreSchema.WasmerSerializeVersionKey.Span);
        return value is null ? 0 : BinaryPrimitives.ReadUInt32BigEndian(value);
    }

    public void SetWasmerSerializeVersion(uint version)
    {
        Span<byte> value = stackalloc byte[32];
        BinaryPrimitives.WriteUInt32BigEndian(value, version);
        db.PutSpan(WasmStoreSchema.WasmerSerializeVersionKey.Span, value);
    }

    public byte GetWasmSchemaVersion()
    {
        byte[]? value = db.Get(WasmStoreSchema.WasmSchemaVersionKey.Span);
        return value?[0] ?? 0;
    }

    public void SetWasmSchemaVersion(byte version)
    {
        db.PutSpan(WasmStoreSchema.WasmSchemaVersionKey.Span, [version]);
    }

    public DeleteWasmResult DeleteWasmEntries(IReadOnlyList<ReadOnlyMemory<byte>> prefixes, int? expectedKeyLength = null)
    {
        int deletedCount = 0;
        int keyLengthMismatchCount = 0;

        foreach (byte[] key in db.GetAllKeys())
        {
            foreach (ReadOnlyMemory<byte> prefix in prefixes)
            {
                ReadOnlySpan<byte> keyPrefix = key.AsSpan(0, prefix.Length);
                if (!Bytes.AreEqual(prefix.Span, keyPrefix))
                    continue;

                if (expectedKeyLength.HasValue && key.Length != expectedKeyLength.Value)
                {
                    keyLengthMismatchCount++;
                    continue;
                }

                deletedCount++;
                db.Remove(key);

                break;
            }
        }

        return new DeleteWasmResult(deletedCount, keyLengthMismatchCount);
    }
}
