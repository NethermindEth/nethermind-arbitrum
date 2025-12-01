// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Evm.Serialization;

/// <summary>
/// RLP encoder/decoder for MultiGas structure.
/// Format: [total, refund, gas[0], gas[1], ..., gas[7]]
/// </summary>
public sealed class MultiGasRlpDecoder : RlpValueDecoder<MultiGas>
{
    public static readonly MultiGasRlpDecoder Instance = new();

    protected override MultiGas DecodeInternal(RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        int sequenceLength = rlpStream.ReadSequenceLength();
        int position = rlpStream.Position;
        int endPosition = position + sequenceLength;

        ulong total = rlpStream.DecodeULong();
        ulong refund = rlpStream.DecodeULong();

        // Read gas dimensions in order: Unknown, Computation, HistoryGrowth, StorageAccess,
        // StorageGrowth, L1Calldata, L2Calldata, WasmComputation
        ulong unknown = 0;
        ulong computation = 0;
        ulong historyGrowth = 0;
        ulong storageAccess = 0;
        ulong storageGrowth = 0;
        ulong l1Calldata = 0;
        ulong l2Calldata = 0;
        ulong wasmComputation = 0;

        // Read up to 8 dimensions, handling backward compatibility (missing fields = 0)
        // and forward compatibility (extra fields skipped)
        int index = 0;
        while (rlpStream.Position < endPosition)
        {
            ulong value = rlpStream.DecodeULong();

            switch (index)
            {
                case 0: unknown = value; break;
                case 1: computation = value; break;
                case 2: historyGrowth = value; break;
                case 3: storageAccess = value; break;
                case 4: storageGrowth = value; break;
                case 5: l1Calldata = value; break;
                case 6: l2Calldata = value; break;
                case 7: wasmComputation = value; break;
                // Skip extra values for forward compatibility (index >= 8)
            }

            index++;
        }

        return new MultiGas(
            unknown,
            computation,
            historyGrowth,
            storageAccess,
            storageGrowth,
            l1Calldata,
            l2Calldata,
            wasmComputation,
            total,
            refund
        );
    }

    protected override MultiGas DecodeInternal(ref Rlp.ValueDecoderContext decoderContext, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        int sequenceLength = decoderContext.ReadSequenceLength();
        int endPosition = decoderContext.Position + sequenceLength;

        ulong total = decoderContext.DecodeULong();
        ulong refund = decoderContext.DecodeULong();

        // Read gas dimensions
        ulong unknown = 0;
        ulong computation = 0;
        ulong historyGrowth = 0;
        ulong storageAccess = 0;
        ulong storageGrowth = 0;
        ulong l1Calldata = 0;
        ulong l2Calldata = 0;
        ulong wasmComputation = 0;

        // Read available dimensions (backward/forward compatible)
        int index = 0;
        while (decoderContext.Position < endPosition)
        {
            ulong value = decoderContext.DecodeULong();

            switch (index)
            {
                case 0: unknown = value; break;
                case 1: computation = value; break;
                case 2: historyGrowth = value; break;
                case 3: storageAccess = value; break;
                case 4: storageGrowth = value; break;
                case 5: l1Calldata = value; break;
                case 6: l2Calldata = value; break;
                case 7: wasmComputation = value; break;
                // Skip extra values for forward compatibility
            }

            index++;
        }

        return new MultiGas(
            unknown,
            computation,
            historyGrowth,
            storageAccess,
            storageGrowth,
            l1Calldata,
            l2Calldata,
            wasmComputation,
            total,
            refund
        );
    }

    public override void Encode(RlpStream stream, MultiGas item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        int contentLength = GetContentLength(item);

        stream.StartSequence(contentLength);

        // Write total and refund first
        stream.Encode(item.Total);
        stream.Encode(item.Refund);

        // Write all 8 gas dimensions in order
        stream.Encode(item.Get(ResourceKind.Unknown));
        stream.Encode(item.Get(ResourceKind.Computation));
        stream.Encode(item.Get(ResourceKind.HistoryGrowth));
        stream.Encode(item.Get(ResourceKind.StorageAccess));
        stream.Encode(item.Get(ResourceKind.StorageGrowth));
        stream.Encode(item.Get(ResourceKind.L1Calldata));
        stream.Encode(item.Get(ResourceKind.L2Calldata));
        stream.Encode(item.Get(ResourceKind.WasmComputation));
    }

    public Rlp Encode(MultiGas item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        RlpStream stream = new(GetLength(item, rlpBehaviors));
        Encode(stream, item, rlpBehaviors);
        return new Rlp(stream.Data.ToArray()!);
    }

    private static int GetContentLength(MultiGas item)
    {
        // Total (ulong) + Refund (ulong) + 8 gas dimensions (each ulong)
        // Each ulong is encoded with Rlp.LengthOf
        return Rlp.LengthOf(item.Total) +
               Rlp.LengthOf(item.Refund) +
               Rlp.LengthOf(item.Get(ResourceKind.Unknown)) +
               Rlp.LengthOf(item.Get(ResourceKind.Computation)) +
               Rlp.LengthOf(item.Get(ResourceKind.HistoryGrowth)) +
               Rlp.LengthOf(item.Get(ResourceKind.StorageAccess)) +
               Rlp.LengthOf(item.Get(ResourceKind.StorageGrowth)) +
               Rlp.LengthOf(item.Get(ResourceKind.L1Calldata)) +
               Rlp.LengthOf(item.Get(ResourceKind.L2Calldata)) +
               Rlp.LengthOf(item.Get(ResourceKind.WasmComputation));
    }

    public override int GetLength(MultiGas item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        return Rlp.LengthOfSequence(GetContentLength(item));
    }
}
