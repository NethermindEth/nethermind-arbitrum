// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution.Receipts;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Test.Execution.Receipts;

[TestFixture]
public class ArbitrumReceiptStorageDecoderTests
{
    [Test]
    public void RlpRoundTrip_BasicReceipt_PreservesAllFields()
    {
        ArbitrumTxReceipt receipt = CreateBasicReceipt();

        ArbitrumTxReceipt decoded = RlpRoundTrip(receipt);

        AssertReceiptFieldsEqual(receipt, decoded);
        decoded.GasUsedForL1.Should().Be(receipt.GasUsedForL1);
        decoded.MultiGasUsed.Should().BeNull();
    }

    [Test]
    public void RlpRoundTrip_ReceiptWithGasUsedForL1_PreservesAllFields()
    {
        ArbitrumTxReceipt receipt = CreateBasicReceipt();
        receipt.GasUsedForL1 = 12345;

        ArbitrumTxReceipt decoded = RlpRoundTrip(receipt);

        AssertReceiptFieldsEqual(receipt, decoded);
        decoded.GasUsedForL1.Should().Be(12345);
        decoded.MultiGasUsed.Should().BeNull();
    }

    [Test]
    public void RlpRoundTrip_ReceiptWithZeroMultiGas_PreservesAllFields()
    {
        ArbitrumTxReceipt receipt = CreateBasicReceipt();
        receipt.GasUsedForL1 = 100;
        receipt.MultiGasUsed = default(MultiGas);

        ArbitrumTxReceipt decoded = RlpRoundTrip(receipt);

        AssertReceiptFieldsEqual(receipt, decoded);
        decoded.GasUsedForL1.Should().Be(100);
        decoded.MultiGasUsed.Should().NotBeNull();
        AssertMultiGasEqual(receipt.MultiGasUsed.Value, decoded.MultiGasUsed!.Value);
    }

    [Test]
    public void RlpRoundTrip_ReceiptWithComputationMultiGas_PreservesAllFields()
    {
        ArbitrumTxReceipt receipt = CreateBasicReceipt();
        receipt.GasUsedForL1 = 500;
        MultiGas multiGas = default;
        multiGas.Increment(ResourceKind.Computation, 21000);
        receipt.MultiGasUsed = multiGas;

        ArbitrumTxReceipt decoded = RlpRoundTrip(receipt);

        AssertReceiptFieldsEqual(receipt, decoded);
        decoded.GasUsedForL1.Should().Be(500);
        decoded.MultiGasUsed.Should().NotBeNull();
        AssertMultiGasEqual(receipt.MultiGasUsed.Value, decoded.MultiGasUsed!.Value);
    }

    [Test]
    public void RlpRoundTrip_ReceiptWithFullMultiGas_PreservesAllFields()
    {
        ArbitrumTxReceipt receipt = CreateBasicReceipt();
        receipt.GasUsedForL1 = 1000;
        MultiGas multiGas = CreateMultiGasWithRefund(
            computation: 10,
            historyGrowth: 11,
            storageAccess: 12,
            storageGrowth: 13,
            l1Calldata: 14,
            l2Calldata: 15,
            wasmComputation: 16,
            refund: 7);
        receipt.MultiGasUsed = multiGas;

        ArbitrumTxReceipt decoded = RlpRoundTrip(receipt);

        AssertReceiptFieldsEqual(receipt, decoded);
        decoded.GasUsedForL1.Should().Be(1000);
        decoded.MultiGasUsed.Should().NotBeNull();
        AssertMultiGasEqual(receipt.MultiGasUsed.Value, decoded.MultiGasUsed!.Value);
    }

    [Test]
    public void RlpRoundTrip_ReceiptWithLogs_PreservesAllFields()
    {
        ArbitrumTxReceipt receipt = CreateBasicReceipt();
        receipt.Logs =
        [
            new LogEntry(TestItem.AddressA, [1, 2, 3], [TestItem.KeccakA, TestItem.KeccakB]),
            new LogEntry(TestItem.AddressB, [4, 5, 6], [TestItem.KeccakC])
        ];
        receipt.GasUsedForL1 = 200;
        MultiGas multiGas = default;
        multiGas.Increment(ResourceKind.Computation, 5000);
        receipt.MultiGasUsed = multiGas;

        ArbitrumTxReceipt decoded = RlpRoundTrip(receipt);

        AssertReceiptFieldsEqual(receipt, decoded);
        decoded.Logs.Should().HaveCount(2);
        decoded.GasUsedForL1.Should().Be(200);
        decoded.MultiGasUsed.Should().NotBeNull();
        AssertMultiGasEqual(receipt.MultiGasUsed.Value, decoded.MultiGasUsed!.Value);
    }

    [Test]
    public void RlpRoundTrip_ValueDecoderContext_PreservesAllFields()
    {
        ArbitrumTxReceipt receipt = CreateBasicReceipt();
        receipt.GasUsedForL1 = 300;
        MultiGas multiGas = CreateMultiGasWithRefund(
            computation: 100,
            storageAccess: 200,
            refund: 50);
        receipt.MultiGasUsed = multiGas;

        ArbitrumReceiptStorageDecoder decoder = new();
        Rlp rlp = decoder.Encode(receipt, RlpBehaviors.Eip658Receipts);

        Rlp.ValueDecoderContext context = new(rlp.Bytes);
        ArbitrumTxReceipt decoded = decoder.Decode(ref context, RlpBehaviors.None);

        AssertReceiptFieldsEqual(receipt, decoded);
        decoded.GasUsedForL1.Should().Be(300);
        decoded.MultiGasUsed.Should().NotBeNull();
        AssertMultiGasEqual(receipt.MultiGasUsed.Value, decoded.MultiGasUsed!.Value);
    }

    [Test]
    public void RlpRoundTrip_Eip658Receipts_UsesStatusCode()
    {
        ArbitrumTxReceipt receipt = CreateBasicReceipt();
        receipt.StatusCode = 1;
        receipt.PostTransactionState = null;
        receipt.GasUsedForL1 = 100;
        MultiGas multiGas = default;
        multiGas.Increment(ResourceKind.Computation, 1000);
        receipt.MultiGasUsed = multiGas;

        ArbitrumReceiptStorageDecoder decoder = new();
        Rlp rlp = decoder.Encode(receipt, RlpBehaviors.Eip658Receipts);
        ArbitrumTxReceipt decoded = decoder.Decode(new RlpStream(rlp.Bytes), RlpBehaviors.None);

        decoded.StatusCode.Should().Be(1);
        decoded.PostTransactionState.Should().BeNull();
        decoded.GasUsedForL1.Should().Be(100);
        AssertMultiGasEqual(receipt.MultiGasUsed.Value, decoded.MultiGasUsed!.Value);
    }

    [Test]
    public void RlpRoundTrip_PreEip658Receipts_UsesPostTransactionState()
    {
        ArbitrumTxReceipt receipt = CreateBasicReceipt();
        receipt.StatusCode = 0;
        receipt.PostTransactionState = TestItem.KeccakH;
        receipt.GasUsedForL1 = 100;
        MultiGas multiGas = default;
        multiGas.Increment(ResourceKind.StorageAccess, 500);
        receipt.MultiGasUsed = multiGas;

        ArbitrumReceiptStorageDecoder decoder = new();
        Rlp rlp = decoder.Encode(receipt, RlpBehaviors.None);
        ArbitrumTxReceipt decoded = decoder.Decode(new RlpStream(rlp.Bytes), RlpBehaviors.None);

        decoded.PostTransactionState.Should().Be(TestItem.KeccakH);
        decoded.GasUsedForL1.Should().Be(100);
        AssertMultiGasEqual(receipt.MultiGasUsed.Value, decoded.MultiGasUsed!.Value);
    }

    [Test]
    public void GetLength_ReceiptWithMultiGas_ReturnsCorrectLength()
    {
        ArbitrumTxReceipt receipt = CreateBasicReceipt();
        receipt.GasUsedForL1 = 100;
        MultiGas multiGas = default;
        multiGas.Increment(ResourceKind.Computation, 1000);
        receipt.MultiGasUsed = multiGas;

        ArbitrumReceiptStorageDecoder decoder = new();
        int length = decoder.GetLength(receipt, RlpBehaviors.Eip658Receipts);

        Rlp rlp = decoder.Encode(receipt, RlpBehaviors.Eip658Receipts);
        length.Should().Be(rlp.Bytes.Length);
    }

    [Test]
    public void GetLength_ReceiptWithoutMultiGas_ReturnsCorrectLength()
    {
        ArbitrumTxReceipt receipt = CreateBasicReceipt();
        receipt.GasUsedForL1 = 100;
        receipt.MultiGasUsed = null;

        ArbitrumReceiptStorageDecoder decoder = new();
        int length = decoder.GetLength(receipt, RlpBehaviors.Eip658Receipts);

        Rlp rlp = decoder.Encode(receipt, RlpBehaviors.Eip658Receipts);
        length.Should().Be(rlp.Bytes.Length);
    }

    [Test]
    public void TxReceiptInterface_RlpRoundTrip_WorksCorrectly()
    {
        ArbitrumTxReceipt receipt = CreateBasicReceipt();
        receipt.GasUsedForL1 = 100;
        MultiGas multiGas = default;
        multiGas.Increment(ResourceKind.Computation, 1000);
        receipt.MultiGasUsed = multiGas;

        ArbitrumReceiptStorageDecoder decoder = new();
        IRlpStreamDecoder<TxReceipt> txReceiptDecoder = decoder;

        Rlp rlp = decoder.Encode(receipt, RlpBehaviors.Eip658Receipts);
        TxReceipt decoded = txReceiptDecoder.Decode(new RlpStream(rlp.Bytes), RlpBehaviors.None);

        decoded.Should().BeOfType<ArbitrumTxReceipt>();
        ArbitrumTxReceipt arbitrumDecoded = (ArbitrumTxReceipt)decoded;
        arbitrumDecoded.GasUsedForL1.Should().Be(100);
        arbitrumDecoded.MultiGasUsed.Should().NotBeNull();
    }

    private static ArbitrumTxReceipt CreateBasicReceipt()
    {
        return new ArbitrumTxReceipt
        {
            Sender = TestItem.AddressA,
            GasUsedTotal = 21000,
            StatusCode = 1,
            Logs = []
        };
    }

    private static ArbitrumTxReceipt RlpRoundTrip(ArbitrumTxReceipt receipt)
    {
        ArbitrumReceiptStorageDecoder decoder = new();
        Rlp rlp = decoder.Encode(receipt, RlpBehaviors.Eip658Receipts);
        return decoder.Decode(new RlpStream(rlp.Bytes), RlpBehaviors.None);
    }

    private static void AssertReceiptFieldsEqual(ArbitrumTxReceipt expected, ArbitrumTxReceipt actual)
    {
        actual.Sender.Should().Be(expected.Sender!, "sender");
        actual.GasUsedTotal.Should().Be(expected.GasUsedTotal, "gas used total");
        actual.StatusCode.Should().Be(expected.StatusCode, "status code");
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
}
