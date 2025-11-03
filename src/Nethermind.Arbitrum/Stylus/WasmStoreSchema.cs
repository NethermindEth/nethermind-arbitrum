// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos.Stylus;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Stylus;

public static class WasmStoreSchema
{
    public const byte WasmSchemaVersion = 1;

    // Based on https://github.com/wasmerio/wasmer/blob/6de934035a4b34c2878552320f058862faea4651/lib/types/src/serialize.rs#L16
    public const byte WasmerSerializeVersion = 8;

    public static readonly ReadOnlyMemory<byte> WasmSchemaVersionKey = "WasmSchemaVersion"u8.ToArray();
    public static readonly ReadOnlyMemory<byte> WasmerSerializeVersionKey = "WasmerSerializeVersion"u8.ToArray();

    public static readonly byte[] RebuildingPositionKey = "rebuild_position"u8.ToArray();
    public static readonly byte[] RebuildingStartBlockHashKey = "rebuild_start_block"u8.ToArray();

    public static readonly Hash256 RebuildingDone = new(Enumerable.Repeat((byte)0xff, 32).ToArray());

    public const byte ArbosVersionStylus = 20;

    // deprecated prefixes, used in version 0x00, purged in version 0x01
    public static readonly ReadOnlyMemory<byte> ActivatedAsmPrefix = "\0wa"u8.ToArray();
    public static readonly ReadOnlyMemory<byte> ActivatedModulePrefix = "\0wm"u8.ToArray();

    // 0x00 prefix to avoid conflicts when wasmdb is not separate database
    public static readonly ReadOnlyMemory<byte> ActivatedAsmWavmPrefix = "\0ww"u8.ToArray(); // (prefix, moduleHash) -> stylus module (wavm)
    public static readonly ReadOnlyMemory<byte> ActivatedAsmArmPrefix = "\0wr"u8.ToArray(); // (prefix, moduleHash) -> stylus asm for ARM system
    public static readonly ReadOnlyMemory<byte> ActivatedAsmX86Prefix = "\0wx"u8.ToArray(); // (prefix, moduleHash) -> stylus asm for x86 system
    public static readonly ReadOnlyMemory<byte> ActivatedAsmHostPrefix = "\0wh"u8.ToArray(); // (prefix, moduleHash) -> stylus asm for system other than ARM and x86

    public const byte WasmPrefixLength = 3;
    public const byte WasmKeyLength = WasmPrefixLength + ValueHash256.MemorySize;

    public static (IReadOnlyList<ReadOnlyMemory<byte>> prefixes, int keyLength) DeprecatedPrefixesV0()
    {
        return ([ActivatedAsmPrefix, ActivatedModulePrefix], WasmKeyLength);
    }

    public static IReadOnlyList<ReadOnlyMemory<byte>> WasmPrefixesExceptWavm()
    {
        (IReadOnlyList<ReadOnlyMemory<byte>> deprecatedPrefixes, _) = DeprecatedPrefixesV0();
        return [.. deprecatedPrefixes, ActivatedAsmArmPrefix, ActivatedAsmX86Prefix, ActivatedAsmHostPrefix];
    }

    public static bool IsSupportedWasmTarget(string target)
    {
        return TryGetActivatedAsmKeyPrefix(target, out _);
    }

    public static bool TryGetActivatedAsmKeyPrefix(string target, out ReadOnlyMemory<byte> prefix)
    {
        prefix = target switch
        {
            StylusTargets.WavmTargetName => ActivatedAsmWavmPrefix,
            StylusTargets.Arm64TargetName => ActivatedAsmArmPrefix,
            StylusTargets.Amd64TargetName => ActivatedAsmX86Prefix,
            StylusTargets.HostTargetName => ActivatedAsmHostPrefix,
            _ => ReadOnlyMemory<byte>.Empty
        };

        return prefix.Length > 0;
    }

    public static byte[] GetActivatedKey(string target, in ValueHash256 moduleHash)
    {
        if (!TryGetActivatedAsmKeyPrefix(target, out ReadOnlyMemory<byte> prefix))
            throw new InvalidOperationException($"Unknown target encountered: {target}");

        byte[] key = new byte[WasmKeyLength];
        prefix.Span.CopyTo(key);
        moduleHash.Bytes.CopyTo(key.AsSpan()[WasmPrefixLength..]);
        return key;
    }

    /// <summary>
    /// Checks if a position hash indicates that rebuilding is complete.
    /// </summary>
    public static bool IsRebuildingDone(Hash256 position)
    {
        return position == RebuildingDone;
    }
}
