// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Buffers.Binary;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Arbos.Programs;

// Nitro parity (go-ethereum/core/state/statedb_arbitrum.go:127-153, NewStylusRoot): Parse stores the raw
// dictionary byte without validation. Dictionary validity is checked later, at the post-fragment-read
// site in StylusPrograms.GetWasmFromRootStylus — moving the check there preserves Nitro's gas-burn
// pattern, where a bad-dict failure occurs only after N×fragmentReadGasCost has been charged.
public readonly record struct StylusRoot(byte DictionaryType, uint DecompressedLength, IReadOnlyList<Address> Fragments)
{
    private const int HeaderSize = 8; // 3 magic + 1 dict + 4 length

    public static StylusOperationResult<StylusRoot> Parse(ReadOnlySpan<byte> rootCode)
    {
        if (!StylusCode.IsStylusProgramRoot(rootCode))
            return Failure("Specified bytecode is not a Stylus program root");

        if (rootCode.Length < HeaderSize)
            return Failure($"Stylus program root too short: need at least {HeaderSize} bytes, got {rootCode.Length}");

        ReadOnlySpan<byte> addressData = rootCode[HeaderSize..];
        if (addressData.Length % Address.Size != 0)
            return Failure($"Stylus program root address data has invalid length: {addressData.Length} (must be multiple of {Address.Size})");

        int count = addressData.Length / Address.Size;
        Address[] fragments = new Address[count];
        for (int i = 0; i < count; i++)
            fragments[i] = new Address(addressData.Slice(i * Address.Size, Address.Size));

        byte dictionaryType = rootCode[3];
        uint decompressedLength = BinaryPrimitives.ReadUInt32BigEndian(rootCode[4..HeaderSize]);
        return StylusOperationResult<StylusRoot>.Success(new StylusRoot(dictionaryType, decompressedLength, fragments));

        static StylusOperationResult<StylusRoot> Failure(string message)
            => StylusOperationResult<StylusRoot>.Failure(new(StylusOperationResultType.InvalidByteCode, message, []));
    }
}
