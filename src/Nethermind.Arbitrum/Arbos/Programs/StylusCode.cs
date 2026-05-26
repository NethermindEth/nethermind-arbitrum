// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Buffers.Binary;
using Nethermind.Arbitrum.Arbos.Compression;
using Nethermind.Core;
using Nethermind.Core.Extensions;

namespace Nethermind.Arbitrum.Arbos.Programs;

public static class StylusCode
{
    // Defines prefix bytes for Stylus WASM program bytecode
    // when deployed on-chain via a user-initiated transaction.
    // These byte prefixes are meant to conflict with the L1 contract EOF
    // validation rules so they can be sufficiently differentiated from EVM bytecode.
    // This allows us to store WASM programs as code in the stateDB side-by-side
    // with EVM contracts, but match against these prefix bytes when loading code
    // to execute the WASMs through Stylus rather than the EVM.
    public const byte StylusEofMagic = 0xEF;
    public const byte StylusEofMagicSuffix = 0xF0;
    public const byte StylusEofVersionClassic = 0x00;
    // ArbOS 60+ adds two more discriminants used by the fragmented-program scheme:
    // 0x01 — a fragment chunk of a larger compressed program (stored in its own contract).
    // 0x02 — a root contract that lists the addresses of all fragments belonging to one program.
    public const byte StylusEofVersionFragment = 0x01;
    public const byte StylusEofVersionRoot = 0x02;
    // 4th byte of a classic or root contract specifies the Stylus dictionary used during compression

    private static readonly byte[] StylusDiscriminant = [StylusEofMagic, StylusEofMagicSuffix, StylusEofVersionClassic];
    private static readonly byte[] StylusFragmentDiscriminant = [StylusEofMagic, StylusEofMagicSuffix, StylusEofVersionFragment];
    private static readonly byte[] StylusRootDiscriminant = [StylusEofMagic, StylusEofMagicSuffix, StylusEofVersionRoot];

    public static bool IsStylusProgramClassic(ReadOnlySpan<byte> code)
    {
        return code.Length > StylusDiscriminant.Length && Bytes.AreEqual(code[..StylusDiscriminant.Length], StylusDiscriminant);
    }

    public static bool IsStylusProgramFragment(ReadOnlySpan<byte> code)
    {
        return code.Length > StylusFragmentDiscriminant.Length && Bytes.AreEqual(code[..StylusFragmentDiscriminant.Length], StylusFragmentDiscriminant);
    }

    // The parser StylusRoot.Parse enforces the full header layout; this predicate only matches the magic bytes.
    public static bool IsStylusProgramRoot(ReadOnlySpan<byte> code)
    {
        return code.Length > StylusRootDiscriminant.Length && Bytes.AreEqual(code[..StylusRootDiscriminant.Length], StylusRootDiscriminant);
    }

    // Mirrors Nitro's state.IsStylusComponentPrefix (go-ethereum/core/state/statedb_arbitrum.go:69):
    // accepts classic at v30+, classic+root at v60+, classic+root+fragment at v60+.
    public static bool IsStylusComponentPrefix(ReadOnlySpan<byte> code, ulong arbosVersion)
    {
        if (arbosVersion < ArbosVersion.StylusContractLimit)
            return IsStylusDeployableProgramPrefix(code, arbosVersion);
        return IsStylusDeployableProgramPrefix(code, arbosVersion) || IsStylusProgramFragment(code);
    }

    // Mirrors Nitro's state.IsStylusDeployableProgramPrefix (statedb_arbitrum.go:78): false pre-Stylus,
    // classic-only pre-v60, classic+root at v60+. Used by the EVM dispatch to route into the Stylus
    // runtime — fragments are NOT deployable (they're referenced by a root contract).
    public static bool IsStylusDeployableProgramPrefix(ReadOnlySpan<byte> code, ulong arbosVersion)
    {
        if (arbosVersion < ArbosVersion.Stylus)
            return false;
        if (arbosVersion < ArbosVersion.StylusContractLimit)
            return IsStylusProgramClassic(code);
        return IsStylusProgramClassic(code) || IsStylusProgramRoot(code);
    }

    public static StylusOperationResult<StylusBytes> StripStylusPrefix(ReadOnlySpan<byte> code)
    {
        return IsStylusProgramClassic(code)
            ? StylusOperationResult<StylusBytes>.Success(new StylusBytes(code[4..], code[3]))
            : StylusOperationResult<StylusBytes>.Failure(new(StylusOperationResultType.InvalidByteCode, "Specified bytecode is not a Stylus program", []));
    }

    public static StylusOperationResult<ReadOnlySpan<byte>> StripStylusFragmentPrefix(ReadOnlySpan<byte> code)
    {
        return !IsStylusProgramFragment(code)
            ? StylusOperationResult<ReadOnlySpan<byte>>.Failure(new(StylusOperationResultType.InvalidByteCode, "Specified bytecode is not a Stylus program fragment", []))
            : StylusOperationResult<ReadOnlySpan<byte>>.Success(code[StylusFragmentDiscriminant.Length..]);
    }

    public static byte[] NewStylusPrefix(byte dictionary)
    {
        byte[] prefix = new byte[StylusDiscriminant.Length + 1];
        Array.Copy(StylusDiscriminant, prefix, StylusDiscriminant.Length);
        prefix[^1] = dictionary;
        return prefix;
    }

    public static byte[] NewStylusFragmentPrefix(ReadOnlySpan<byte> compressedChunk)
    {
        byte[] result = new byte[StylusFragmentDiscriminant.Length + compressedChunk.Length];
        StylusFragmentDiscriminant.CopyTo(result.AsSpan());
        compressedChunk.CopyTo(result.AsSpan(StylusFragmentDiscriminant.Length));
        return result;
    }

    public static byte[] NewStylusRootPrefix(byte dictionary, uint decompressedLength, ReadOnlySpan<Address> fragments)
    {
        const int headerSize = 8; // 3 magic + 1 dict + 4 length
        byte[] result = new byte[headerSize + fragments.Length * Address.Size];

        Span<byte> buffer = result;
        StylusRootDiscriminant.CopyTo(buffer);
        buffer[StylusRootDiscriminant.Length] = dictionary;
        BinaryPrimitives.WriteUInt32BigEndian(buffer[(StylusRootDiscriminant.Length + 1)..], decompressedLength);

        int offset = headerSize;
        foreach (Address fragment in fragments)
        {
            fragment.Bytes.CopyTo(buffer[offset..]);
            offset += Address.Size;
        }

        return result;
    }
}
