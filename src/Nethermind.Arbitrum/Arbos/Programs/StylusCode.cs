// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos.Compression;
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
    public const byte StylusEofVersion = 0x00;
    // 4th byte specifies the Stylus dictionary used during compression

    private static readonly byte[] StylusDiscriminant = [StylusEofMagic, StylusEofMagicSuffix, StylusEofVersion];

    public static bool IsStylusProgram(ReadOnlySpan<byte> code)
    {
        return code.Length >= StylusDiscriminant.Length + 1 && Bytes.AreEqual(code[..3], StylusDiscriminant);
    }

    public static StylusOperationResult<StylusBytes> StripStylusPrefix(ReadOnlySpan<byte> code)
    {
        if (!IsStylusProgram(code))
            return StylusOperationResult<StylusBytes>.Failure(new(StylusOperationResultType.InvalidByteCode, "Specified bytecode is not a Stylus program", []));

        BrotliCompression.Dictionary dictionary = (BrotliCompression.Dictionary)code[3];
        return !Enum.IsDefined(dictionary)
            ? StylusOperationResult<StylusBytes>.Failure(new(StylusOperationResultType.UnsupportedCompressionDict, $"Unsupported Stylus dictionary {dictionary}", []))
            : StylusOperationResult<StylusBytes>.Success(new StylusBytes(code[4..], dictionary));
    }

    public static byte[] NewStylusPrefix(byte dictionary)
    {
        byte[] prefix = new byte[StylusDiscriminant.Length + 1];
        Array.Copy(StylusDiscriminant, prefix, StylusDiscriminant.Length);
        prefix[^1] = dictionary;
        return prefix;
    }
}
