// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Stylus;

public static class WasmDbExtensions
{
    public static Hash256? GetRebuildingPosition(this IWasmDb wasmDb)
    {
        byte[]? data = wasmDb.Get(WasmStoreSchema.RebuildingPositionKey);
        return data != null && data.Length == 32 ? new Hash256(data) : null;
    }

    public static void SetRebuildingPosition(this IWasmDb wasmDb, Hash256 position)
    {
        wasmDb.Set(WasmStoreSchema.RebuildingPositionKey, position.Bytes.ToArray());
    }

    public static Hash256? GetRebuildingStartBlockHash(this IWasmDb wasmDb)
    {
        byte[]? data = wasmDb.Get(WasmStoreSchema.RebuildingStartBlockHashKey);
        return data != null && data.Length == 32 ? new Hash256(data) : null;
    }

    public static void SetRebuildingStartBlockHash(this IWasmDb wasmDb, Hash256 blockHash)
    {
        wasmDb.Set(WasmStoreSchema.RebuildingStartBlockHashKey, blockHash.Bytes.ToArray());
    }
}
