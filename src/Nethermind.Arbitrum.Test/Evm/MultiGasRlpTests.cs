// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Evm;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Test.Evm;

[TestFixture]
public class MultiGasRlpTests
{
    [Test]
    public void Decode_AllDimensions_PreservesAllFields()
    {
        MultiGas original = default;
        original.Increment(ResourceKind.Computation, 10);
        original.Increment(ResourceKind.HistoryGrowth, 11);
        original.Increment(ResourceKind.StorageAccess, 12);
        original.Increment(ResourceKind.StorageGrowth, 13);
        original.Increment(ResourceKind.L1Calldata, 14);
        original.Increment(ResourceKind.L2Calldata, 15);
        original.Increment(ResourceKind.WasmComputation, 16);

        MultiGas decoded = RlpRoundTrip(original);

        AssertMultiGasEqual(original, decoded);
    }

    [Test]
    public void Decode_AllDimensionsWithRefund_PreservesAllFields()
    {
        MultiGas original = CreateMultiGasWithRefund(
            computation: 10,
            historyGrowth: 11,
            storageAccess: 12,
            storageGrowth: 13,
            l1Calldata: 14,
            l2Calldata: 15,
            wasmComputation: 16,
            refund: 7);

        MultiGas decoded = RlpRoundTrip(original);

        AssertMultiGasEqual(original, decoded);
    }
    [Test]
    public void Decode_DefaultGas_PreservesAllFields()
    {
        MultiGas original = default;

        MultiGas decoded = RlpRoundTrip(original);

        AssertMultiGasEqual(original, decoded);
    }

    [Test]
    public void Decode_PartialDimensions_PreservesAllFields()
    {
        MultiGas original = default;
        original.Increment(ResourceKind.Unknown, 1);
        original.Increment(ResourceKind.Computation, 10);
        original.Increment(ResourceKind.HistoryGrowth, 11);
        original.Increment(ResourceKind.StorageGrowth, 13);

        MultiGas decoded = RlpRoundTrip(original);

        AssertMultiGasEqual(original, decoded);
    }

    [Test]
    public void Decode_SingleDimension_PreservesAllFields()
    {
        MultiGas original = default;
        original.Increment(ResourceKind.Computation, 100);

        MultiGas decoded = RlpRoundTrip(original);

        AssertMultiGasEqual(original, decoded);
    }

    [Test]
    public void Decode_ValueDecoderContext_PreservesAllFields()
    {
        MultiGas original = CreateMultiGasWithRefund(
            computation: 100,
            storageAccess: 200,
            refund: 50);

        RlpStream stream = new(original.GetRlpLength());
        original.Encode(stream);
        byte[] encoded = stream.Data.ToArray()!;

        Rlp.ValueDecoderContext context = new(encoded);
        MultiGas decoded = MultiGas.Decode(ref context);

        AssertMultiGasEqual(original, decoded);
    }

    [Test]
    public void Decode_WithRefund_PreservesAllFields()
    {
        MultiGas original = CreateMultiGasWithRefund(
            computation: 100,
            l1Calldata: 50,
            refund: 20);

        MultiGas decoded = RlpRoundTrip(original);

        AssertMultiGasEqual(original, decoded);
    }

    [Test]
    public void GetRlpLength_DefaultGas_ReturnsCorrectLength()
    {
        MultiGas gas = default;

        int length = gas.GetRlpLength();

        RlpStream stream = new(length);
        gas.Encode(stream);
        stream.Data.ToArray()!.Length.Should().Be(length);
    }

    [Test]
    public void GetRlpLength_WithValues_ReturnsCorrectLength()
    {
        MultiGas gas = default;
        gas.Increment(ResourceKind.Computation, 21000);
        gas.Increment(ResourceKind.StorageAccess, 5000);

        int length = gas.GetRlpLength();

        RlpStream stream = new(length);
        gas.Encode(stream);
        stream.Data.ToArray()!.Length.Should().Be(length);
    }

    [Test]
    public void ToJson_AllDimensionsWithRefund_SerializesAllFields()
    {
        MultiGas gas = CreateMultiGasWithRefund(
            computation: 10,
            historyGrowth: 11,
            storageAccess: 12,
            storageGrowth: 13,
            l1Calldata: 14,
            l2Calldata: 15,
            wasmComputation: 16,
            refund: 7);

        MultiGasForJson json = gas.ToJson();

        json.Computation.Should().Be(10UL);
        json.HistoryGrowth.Should().Be(11UL);
        json.StorageAccess.Should().Be(12UL);
        json.StorageGrowth.Should().Be(13UL);
        json.L1Calldata.Should().Be(14UL);
        json.L2Calldata.Should().Be(15UL);
        json.WasmComputation.Should().Be(16UL);
        json.Total.Should().Be(91UL);
        json.Refund.Should().Be(7UL);
    }

    [Test]
    public void ToJson_DefaultGas_SerializesAllFields()
    {
        MultiGas gas = default;

        MultiGasForJson json = gas.ToJson();

        json.Unknown.Should().Be(0UL);
        json.Computation.Should().Be(0UL);
        json.HistoryGrowth.Should().Be(0UL);
        json.StorageAccess.Should().Be(0UL);
        json.StorageGrowth.Should().Be(0UL);
        json.L1Calldata.Should().Be(0UL);
        json.L2Calldata.Should().Be(0UL);
        json.WasmComputation.Should().Be(0UL);
        json.Total.Should().Be(0UL);
        json.Refund.Should().Be(0UL);
    }

    [Test]
    public void ToJson_PartialDimensions_SerializesCorrectly()
    {
        MultiGas gas = default;
        gas.Increment(ResourceKind.Unknown, 1);
        gas.Increment(ResourceKind.Computation, 10);
        gas.Increment(ResourceKind.HistoryGrowth, 11);
        gas.Increment(ResourceKind.StorageGrowth, 13);

        MultiGasForJson json = gas.ToJson();

        json.Unknown.Should().Be(1UL);
        json.Computation.Should().Be(10UL);
        json.HistoryGrowth.Should().Be(11UL);
        json.StorageGrowth.Should().Be(13UL);
        json.StorageAccess.Should().Be(0UL);
        json.Total.Should().Be(35UL);
    }

    [Test]
    public void ToJson_SingleDimension_SerializesCorrectly()
    {
        MultiGas gas = default;
        gas.Increment(ResourceKind.Computation, 100);

        MultiGasForJson json = gas.ToJson();

        json.Computation.Should().Be(100UL);
        json.Total.Should().Be(100UL);
        json.HistoryGrowth.Should().Be(0UL);
    }

    [Test]
    public void ToJson_WithRefund_IncludesRefundField()
    {
        MultiGas gas = CreateMultiGasWithRefund(l1Calldata: 50, refund: 20);

        MultiGasForJson json = gas.ToJson();

        json.L1Calldata.Should().Be(50UL);
        json.Total.Should().Be(50UL);
        json.Refund.Should().Be(20UL);
    }

    private static void AssertMultiGasEqual(MultiGas expected, MultiGas actual)
    {
        for (int i = 0; i < MultiGas.NumResourceKinds; i++)
        {
            ResourceKind kind = (ResourceKind)i;
            actual.Get(kind).Should().Be(expected.Get(kind), $"dimension {kind}");
        }
        actual.Refund.Should().Be(expected.Refund, "refund");
        actual.Total.Should().Be(expected.Total, "total");
    }

    /// <summary>
    /// Creates a MultiGas with specified values including refund.
    /// This uses RLP encode/decode to set the refund since Refund has a private setter.
    /// </summary>
    private static MultiGas CreateMultiGasWithRefund(
        ulong unknown = 0,
        ulong computation = 0,
        ulong historyGrowth = 0,
        ulong storageAccess = 0,
        ulong storageGrowth = 0,
        ulong l1Calldata = 0,
        ulong l2Calldata = 0,
        ulong wasmComputation = 0,
        ulong refund = 0)
    {
        ulong total = unknown + computation + historyGrowth + storageAccess +
                      storageGrowth + l1Calldata + l2Calldata + wasmComputation;

        int contentLength = Rlp.LengthOf(total) + Rlp.LengthOf(refund);
        ulong[] gas = [unknown, computation, historyGrowth, storageAccess,
                       storageGrowth, l1Calldata, l2Calldata, wasmComputation];
        foreach (ulong g in gas)
            contentLength += Rlp.LengthOf(g);

        RlpStream stream = new(Rlp.LengthOfSequence(contentLength));
        stream.StartSequence(contentLength);
        stream.Encode(total);
        stream.Encode(refund);
        foreach (ulong g in gas)
            stream.Encode(g);

        byte[] encoded = stream.Data.ToArray()!;
        return MultiGas.Decode(new RlpStream(encoded));
    }

    private static MultiGas RlpRoundTrip(MultiGas original)
    {
        RlpStream stream = new(original.GetRlpLength());
        original.Encode(stream);
        byte[] encoded = stream.Data.ToArray()!;

        RlpStream decodeStream = new(encoded);
        return MultiGas.Decode(decodeStream);
    }
}
