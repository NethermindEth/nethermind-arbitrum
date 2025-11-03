// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Stylus;

public static class WasmDbExtensions
{
    public static void SetRebuildingPosition(this IWasmDb db, Hash256 position)
    {
        db.Set(WasmStoreSchema.RebuildingPositionKey, position.Bytes.ToArray());
    }

    public static Hash256? GetRebuildingPosition(this IWasmDb db)
    {
        byte[]? value = db.Get(WasmStoreSchema.RebuildingPositionKey);
        return value != null && value.Length == 32 ? new Hash256(value) : null;
    }

    public static void SetRebuildingStartBlockHash(this IWasmDb db, Hash256 hash)
    {
        db.Set(WasmStoreSchema.RebuildingStartBlockHashKey, hash.Bytes.ToArray());
    }

    public static Hash256? GetRebuildingStartBlockHash(this IWasmDb db)
    {
        byte[]? value = db.Get(WasmStoreSchema.RebuildingStartBlockHashKey);
        return value != null && value.Length == 32 ? new Hash256(value) : null;
    }
}
