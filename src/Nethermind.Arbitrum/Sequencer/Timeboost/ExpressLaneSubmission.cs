// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Buffers.Binary;
using System.Text;
using Nethermind.Arbitrum.Data;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Crypto;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Sequencer.Timeboost;

public sealed class ExpressLaneSubmission
{
    // domainValue = Keccak256("TIMEBOOST_BID") — constant sentinel prefix for signing.
    private static readonly byte[] DomainValue = Keccak.Compute("TIMEBOOST_BID").BytesToArray();

    public required Transaction Transaction { get; init; }
    public required ulong Round { get; init; }
    public required ulong SequenceNumber { get; init; }
    public required byte[] Signature { get; init; }
    public required ulong ChainId { get; init; }
    public required Address AuctionContractAddress { get; init; }
    public ConditionalOptions? Options { get; init; }

    // Format: domainValue (32) | chainId (32 BE) | auctionContract (20) | round (8 BE) | seqNum (8 BE) | rlpTx
    public byte[] ToMessageBytes()
    {
        byte[] rlpTx = Rlp.Encode(Transaction).Bytes;

        // 32 + 32 + 20 + 8 + 8 + rlpTx.Length
        byte[] buf = new byte[100 + rlpTx.Length];
        int pos = 0;

        DomainValue.CopyTo(buf, pos);
        pos += 32;

        // chainId padded to 32 bytes big-endian
        BinaryPrimitives.WriteUInt64BigEndian(buf.AsSpan(pos + 24, 8), ChainId);
        pos += 32;

        AuctionContractAddress.Bytes.CopyTo(buf, pos);
        pos += 20;

        BinaryPrimitives.WriteUInt64BigEndian(buf.AsSpan(pos, 8), Round);
        pos += 8;

        BinaryPrimitives.WriteUInt64BigEndian(buf.AsSpan(pos, 8), SequenceNumber);
        pos += 8;

        rlpTx.CopyTo(buf, pos);
        return buf;
    }

    public ValueHash256 ComputeSigningHash()
    {
        byte[] msgBytes = ToMessageBytes();
        string prefixStr = $"\x19Ethereum Signed Message:\n{msgBytes.Length}";
        byte[] prefixBytes = Encoding.UTF8.GetBytes(prefixStr);
        byte[] prefixed = new byte[prefixBytes.Length + msgBytes.Length];
        prefixBytes.CopyTo(prefixed, 0);
        msgBytes.CopyTo(prefixed, prefixBytes.Length);
        return ValueKeccak.Compute(prefixed);
    }

    // Recovers the signer address from the Ethereum personal-sign signature; caches the result.
    public Address RecoverSender(IEthereumEcdsa ecdsa)
    {
        if (Signature.Length != 65)
            throw new InvalidOperationException("Express lane submission signature must be 65 bytes");

        ValueHash256 msgHash = ComputeSigningHash();

        // Normalize V: if >= 27, subtract 27 to get the raw recovery ID
        int recoveryId = Signature[64] >= 27 ? Signature[64] - 27 : Signature[64];
        Signature sig = new(Signature.AsSpan(0, 64), recoveryId);

        return ecdsa.RecoverAddress(sig, msgHash)
            ?? throw new InvalidOperationException("Could not recover signer from express lane submission signature");
    }
}
