// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Text.Json;
using FluentAssertions;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution.Receipts;
using Nethermind.Arbitrum.Rpc;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Int256;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Test.Rpc;

[TestFixture]
public class ArbitrumReceiptForRpcTests
{
    [Test]
    public void Constructor_ArbitrumTxReceipt_SetsGasUsedForL1()
    {
        ArbitrumTxReceipt receipt = CreateBasicReceipt();
        receipt.GasUsedForL1 = 12345;

        ArbitrumReceiptForRpc receiptForRpc = CreateReceiptForRpc(receipt);

        receiptForRpc.GasUsedForL1.Should().Be(12345);
    }

    [Test]
    public void Constructor_WithArbitrumReceiptNonZeroMultiGas_ExposesMultiGasUsed()
    {
        ArbitrumTxReceipt receipt = CreateBasicReceipt();
        receipt.GasUsedForL1 = 100;
        MultiGas multiGas = default;
        multiGas.Increment(ResourceKind.Computation, 21000);
        receipt.MultiGasUsed = multiGas;

        ArbitrumReceiptForRpc receiptForRpc = CreateReceiptForRpc(receipt);

        receiptForRpc.MultiGasUsed.Should().NotBeNull();
        receiptForRpc.MultiGasUsed!.Value.Computation.Should().Be(21000);
    }

    [Test]
    public void Constructor_WithArbitrumReceiptZeroMultiGas_HidesMultiGasUsed()
    {
        ArbitrumTxReceipt receipt = CreateBasicReceipt();
        receipt.GasUsedForL1 = 100;
        MultiGas multiGas = default; // Zero multigas
        receipt.MultiGasUsed = multiGas;

        ArbitrumReceiptForRpc receiptForRpc = CreateReceiptForRpc(receipt);

        receiptForRpc.MultiGasUsed.Should().BeNull();
    }

    [Test]
    public void Constructor_WithArbitrumReceiptNullMultiGas_HidesMultiGasUsed()
    {
        ArbitrumTxReceipt receipt = CreateBasicReceipt();
        receipt.GasUsedForL1 = 100;
        receipt.MultiGasUsed = null;

        ArbitrumReceiptForRpc receiptForRpc = CreateReceiptForRpc(receipt);

        receiptForRpc.MultiGasUsed.Should().BeNull();
    }

    [Test]
    public void Constructor_WithBaseReceiptNonZeroMultiGas_ExposesMultiGas()
    {
        ArbitrumTxReceipt receipt = CreateBasicReceipt();
        receipt.GasUsedForL1 = 500;
        MultiGas multiGas = default;
        multiGas.Increment(ResourceKind.StorageAccess, 1000);
        receipt.MultiGasUsed = multiGas;
        TxReceipt baseReceipt = receipt;

        ArbitrumReceiptForRpc receiptForRpc = new(
            receipt.TxHash!,
            baseReceipt,
            1234567890,
            new TxGasInfo(new UInt256(1000)));

        receiptForRpc.GasUsedForL1.Should().Be(500);
        receiptForRpc.MultiGasUsed.Should().NotBeNull();
        receiptForRpc.MultiGasUsed!.Value.StorageAccess.Should().Be(1000);
    }

    [Test]
    public void Constructor_WithBaseReceiptZeroMultiGas_HidesMultiGas()
    {
        ArbitrumTxReceipt receipt = CreateBasicReceipt();
        receipt.GasUsedForL1 = 500;
        MultiGas multiGas = default; // Zero multigas
        receipt.MultiGasUsed = multiGas;
        TxReceipt baseReceipt = receipt;

        ArbitrumReceiptForRpc receiptForRpc = new(
            receipt.TxHash!,
            baseReceipt,
            1234567890,
            new TxGasInfo(new UInt256(1000)));

        receiptForRpc.GasUsedForL1.Should().Be(500);
        receiptForRpc.MultiGasUsed.Should().BeNull();
    }

    [Test]
    public void IsZero_DefaultMultiGas_ReturnsTrue()
    {
        MultiGas zero = default;

        zero.IsZero().Should().BeTrue();
    }

    [Test]
    public void IsZero_NonZeroComputation_ReturnsFalse()
    {
        MultiGas gas = default;
        gas.Increment(ResourceKind.Computation, 1);

        gas.IsZero().Should().BeFalse();
    }

    [Test]
    public void IsZero_NonZeroRefund_ReturnsFalse()
    {
        // Create multigas with non-zero refund using RLP
        MultiGas gas = CreateMultiGasWithRefund(refund: 100);

        gas.IsZero().Should().BeFalse();
    }

    [Test]
    public void ToJson_ZeroMultiGas_SetsAllFieldsToZero()
    {
        MultiGas zero = default;

        MultiGasForJson json = zero.ToJson();

        json.Total.Should().Be(0);
        json.Refund.Should().Be(0);
        json.Unknown.Should().Be(0);
        json.Computation.Should().Be(0);
        json.HistoryGrowth.Should().Be(0);
        json.StorageAccess.Should().Be(0);
        json.StorageGrowth.Should().Be(0);
        json.L1Calldata.Should().Be(0);
        json.L2Calldata.Should().Be(0);
        json.WasmComputation.Should().Be(0);
    }

    [Test]
    public void ToJson_ComputationGas_SetsComputationField()
    {
        MultiGas gas = default;
        gas.Increment(ResourceKind.Computation, 21000);

        MultiGasForJson json = gas.ToJson();

        json.Computation.Should().Be(21000);
        json.Total.Should().Be(21000);
    }

    [Test]
    public void ToJson_AllDimensions_SetsAllFieldsCorrectly()
    {
        MultiGas gas = CreateMultiGasWithRefund(
            unknown: 1,
            computation: 10,
            historyGrowth: 11,
            storageAccess: 12,
            storageGrowth: 13,
            l1Calldata: 14,
            l2Calldata: 15,
            wasmComputation: 16,
            refund: 7);

        MultiGasForJson json = gas.ToJson();

        json.Unknown.Should().Be(1);
        json.Computation.Should().Be(10);
        json.HistoryGrowth.Should().Be(11);
        json.StorageAccess.Should().Be(12);
        json.StorageGrowth.Should().Be(13);
        json.L1Calldata.Should().Be(14);
        json.L2Calldata.Should().Be(15);
        json.WasmComputation.Should().Be(16);
        json.Refund.Should().Be(7);
        json.Total.Should().Be(gas.Total);
    }

    [Test]
    public void MultiGasForJson_JsonSerialization_ProducesCorrectPropertyNames()
    {
        MultiGas gas = CreateMultiGasWithRefund(
            computation: 100,
            storageAccess: 200,
            refund: 50);

        MultiGasForJson json = gas.ToJson();
        string serialized = JsonSerializer.Serialize(json);

        serialized.Should().Contain("\"computation\":100");
        serialized.Should().Contain("\"storageAccess\":200");
        serialized.Should().Contain("\"refund\":50");
        // Total is sum of dimensions (100 + 200 = 300), refund is separate
        serialized.Should().Contain("\"total\":300");
    }

    [Test]
    public void MultiGasForJson_AllDimensionsSerialization_ProducesCorrectJson()
    {
        MultiGas gas = CreateMultiGasWithRefund(
            unknown: 1,
            computation: 10,
            historyGrowth: 11,
            storageAccess: 12,
            storageGrowth: 13,
            l1Calldata: 14,
            l2Calldata: 15,
            wasmComputation: 16,
            refund: 7);

        MultiGasForJson json = gas.ToJson();
        string serialized = JsonSerializer.Serialize(json);

        serialized.Should().Contain("\"unknown\":1");
        serialized.Should().Contain("\"computation\":10");
        serialized.Should().Contain("\"historyGrowth\":11");
        serialized.Should().Contain("\"storageAccess\":12");
        serialized.Should().Contain("\"storageGrowth\":13");
        serialized.Should().Contain("\"l1Calldata\":14");
        serialized.Should().Contain("\"l2Calldata\":15");
        serialized.Should().Contain("\"wasmComputation\":16");
        serialized.Should().Contain("\"refund\":7");
    }

    private static ArbitrumTxReceipt CreateBasicReceipt()
    {
        return new ArbitrumTxReceipt
        {
            TxHash = TestItem.KeccakA,
            Bloom = new Bloom(),
            Index = 0,
            Recipient = TestItem.AddressA,
            Sender = TestItem.AddressB,
            BlockHash = TestItem.KeccakB,
            BlockNumber = 1,
            GasUsed = 21000,
            GasUsedTotal = 21000,
            StatusCode = 1,
            Logs = []
        };
    }

    private static ArbitrumReceiptForRpc CreateReceiptForRpc(ArbitrumTxReceipt receipt)
    {
        return new ArbitrumReceiptForRpc(
            receipt.TxHash!,
            receipt,
            1234567890,
            new TxGasInfo(new UInt256(1000)));
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
