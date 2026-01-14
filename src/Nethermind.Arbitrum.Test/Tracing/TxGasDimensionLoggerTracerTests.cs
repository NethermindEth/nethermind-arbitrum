// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Blockchain.Tracing.GethStyle;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.TransactionProcessing;

namespace Nethermind.Arbitrum.Test.Tracing;

[TestFixture]
public class TxGasDimensionLoggerTracerTests
{
    private static GethTraceOptions DefaultOptions => GethTraceOptions.Default with { Tracer = TxGasDimensionLoggerTracer.TracerName };

    [Test]
    public void CaptureGasDimension_SingleOpcode_RecordsCorrectDelta()
    {
        Transaction tx = Build.A.Transaction.WithHash(TestItem.KeccakA).TestObject;
        Block block = Build.A.Block.WithNumber(100).TestObject;
        TxGasDimensionLoggerTracer tracer = new(tx, block, DefaultOptions);

        MultiGas gasBefore = default;
        gasBefore.Increment(ResourceKind.Computation, 10);

        MultiGas gasAfter = default;
        gasAfter.Increment(ResourceKind.Computation, 15);
        gasAfter.Increment(ResourceKind.StorageAccess, 100);

        tracer.CaptureGasDimension(pc: 5, Instruction.ADD, depth: 1, in gasBefore, in gasAfter, gasCost: 3);
        tracer.MarkAsSuccess(Address.Zero, new GasConsumed(21000, 21000), [], []);
        GethLikeTxTrace result = tracer.BuildResult();

        TxGasDimensionResult dimensionResult = (TxGasDimensionResult)result.CustomTracerResult!.Value;
        dimensionResult.DimensionLogs.Should().HaveCount(1);

        DimensionLog log = dimensionResult.DimensionLogs[0];
        log.Pc.Should().Be(5);
        log.Op.Should().Be("ADD");
        log.Depth.Should().Be(1);
        log.OneDimensionalGasCost.Should().Be(3);
        log.Computation.Should().Be(5);
        log.StateAccess.Should().Be(100);
        log.StateGrowth.Should().Be(0);
        log.HistoryGrowth.Should().Be(0);
    }

    [Test]
    public void CaptureGasDimension_MultipleOpcodes_AccumulatesLogs()
    {
        Transaction tx = Build.A.Transaction.WithHash(TestItem.KeccakA).TestObject;
        Block block = Build.A.Block.WithNumber(100).TestObject;
        TxGasDimensionLoggerTracer tracer = new(tx, block, DefaultOptions);

        MultiGas gas1Before = default;
        gas1Before.Increment(ResourceKind.Computation, 0);
        MultiGas gas1After = default;
        gas1After.Increment(ResourceKind.Computation, 3);

        MultiGas gas2Before = default;
        gas2Before.Increment(ResourceKind.Computation, 3);
        MultiGas gas2After = default;
        gas2After.Increment(ResourceKind.Computation, 6);
        gas2After.Increment(ResourceKind.StorageAccess, 100);

        tracer.CaptureGasDimension(pc: 0, Instruction.PUSH1, depth: 1, in gas1Before, in gas1After, gasCost: 3);
        tracer.CaptureGasDimension(pc: 2, Instruction.SLOAD, depth: 1, in gas2Before, in gas2After, gasCost: 2100);
        tracer.MarkAsSuccess(Address.Zero, new GasConsumed(23103, 23103), [], []);
        GethLikeTxTrace result = tracer.BuildResult();

        TxGasDimensionResult dimensionResult = (TxGasDimensionResult)result.CustomTracerResult!.Value;
        dimensionResult.DimensionLogs.Should().HaveCount(2);

        dimensionResult.DimensionLogs[0].Op.Should().Be("PUSH1");
        dimensionResult.DimensionLogs[0].OneDimensionalGasCost.Should().Be(3);

        dimensionResult.DimensionLogs[1].Op.Should().Be("SLOAD");
        dimensionResult.DimensionLogs[1].OneDimensionalGasCost.Should().Be(2100);
        dimensionResult.DimensionLogs[1].StateAccess.Should().Be(100);
    }

    [Test]
    public void SetIntrinsicGas_Called_StoresValue()
    {
        Transaction tx = Build.A.Transaction.WithHash(TestItem.KeccakA).TestObject;
        Block block = Build.A.Block.WithNumber(100).TestObject;
        TxGasDimensionLoggerTracer tracer = new(tx, block, DefaultOptions);

        tracer.SetIntrinsicGas(21000);
        tracer.MarkAsSuccess(Address.Zero, new GasConsumed(21000, 21000), [], []);
        GethLikeTxTrace result = tracer.BuildResult();

        TxGasDimensionResult dimensionResult = (TxGasDimensionResult)result.CustomTracerResult!.Value;
        dimensionResult.IntrinsicGas.Should().Be(21000);
    }

    [Test]
    public void SetPosterGas_Called_StoresValueAndAffectsL1L2Split()
    {
        Transaction tx = Build.A.Transaction.WithHash(TestItem.KeccakA).TestObject;
        Block block = Build.A.Block.WithNumber(100).TestObject;
        TxGasDimensionLoggerTracer tracer = new(tx, block, DefaultOptions);

        tracer.SetPosterGas(5000);
        tracer.MarkAsSuccess(Address.Zero, new GasConsumed(25000, 25000), [], []);
        GethLikeTxTrace result = tracer.BuildResult();

        TxGasDimensionResult dimensionResult = (TxGasDimensionResult)result.CustomTracerResult!.Value;
        dimensionResult.GasUsed.Should().Be(25000);
        dimensionResult.GasUsedForL1.Should().Be(5000);
        dimensionResult.GasUsedForL2.Should().Be(20000);
    }

    [Test]
    public void BuildResult_AfterCaptures_ReturnsCorrectStructure()
    {
        Hash256 txHash = TestItem.KeccakA;
        Transaction tx = Build.A.Transaction.WithHash(txHash).TestObject;
        Block block = Build.A.Block.WithNumber(123).WithTimestamp(1704067200).TestObject;
        TxGasDimensionLoggerTracer tracer = new(tx, block, DefaultOptions);

        MultiGas gasBefore = default;
        MultiGas gasAfter = default;
        gasAfter.Increment(ResourceKind.Computation, 10);

        tracer.CaptureGasDimension(pc: 0, Instruction.STOP, depth: 1, in gasBefore, in gasAfter, gasCost: 0);
        tracer.SetIntrinsicGas(21000);
        tracer.SetPosterGas(1000);
        tracer.MarkAsSuccess(Address.Zero, new GasConsumed(22000, 22000), [], []);
        GethLikeTxTrace result = tracer.BuildResult();

        result.TxHash.Should().Be(txHash);
        result.CustomTracerResult.Should().NotBeNull();

        TxGasDimensionResult dimensionResult = (TxGasDimensionResult)result.CustomTracerResult!.Value;
        dimensionResult.TxHash.Should().Be(txHash.ToString());
        dimensionResult.GasUsed.Should().Be(22000);
        dimensionResult.GasUsedForL1.Should().Be(1000);
        dimensionResult.GasUsedForL2.Should().Be(21000);
        dimensionResult.IntrinsicGas.Should().Be(21000);
        dimensionResult.BlockNumber.Should().Be(123);
        dimensionResult.BlockTimestamp.Should().Be(1704067200);
        dimensionResult.Status.Should().Be(1);
        dimensionResult.Failed.Should().BeFalse();
        dimensionResult.DimensionLogs.Should().HaveCount(1);
    }

    [Test]
    public void BuildResult_WithFailure_SetsFailedTrue()
    {
        Transaction tx = Build.A.Transaction.WithHash(TestItem.KeccakA).TestObject;
        Block block = Build.A.Block.WithNumber(100).TestObject;
        TxGasDimensionLoggerTracer tracer = new(tx, block, DefaultOptions);

        tracer.MarkAsFailed(Address.Zero, new GasConsumed(21000, 21000), [], "execution reverted");
        GethLikeTxTrace result = tracer.BuildResult();

        TxGasDimensionResult dimensionResult = (TxGasDimensionResult)result.CustomTracerResult!.Value;
        dimensionResult.Failed.Should().BeTrue();
        dimensionResult.Status.Should().Be(0);
    }

    [Test]
    public void BuildResult_OpcodeNames_AreUppercase()
    {
        Transaction tx = Build.A.Transaction.WithHash(TestItem.KeccakA).TestObject;
        Block block = Build.A.Block.WithNumber(100).TestObject;
        TxGasDimensionLoggerTracer tracer = new(tx, block, DefaultOptions);

        MultiGas gasBefore = default;
        MultiGas gasAfter = default;
        gasAfter.Increment(ResourceKind.Computation, 1);

        tracer.CaptureGasDimension(pc: 0, Instruction.ADD, depth: 1, in gasBefore, in gasAfter, gasCost: 3);
        tracer.CaptureGasDimension(pc: 1, Instruction.SSTORE, depth: 1, in gasBefore, in gasAfter, gasCost: 5000);
        tracer.CaptureGasDimension(pc: 2, Instruction.CALL, depth: 1, in gasBefore, in gasAfter, gasCost: 700);
        tracer.CaptureGasDimension(pc: 3, Instruction.PUSH1, depth: 1, in gasBefore, in gasAfter, gasCost: 3);
        tracer.MarkAsSuccess(Address.Zero, new GasConsumed(21000, 21000), [], []);
        GethLikeTxTrace result = tracer.BuildResult();

        TxGasDimensionResult dimensionResult = (TxGasDimensionResult)result.CustomTracerResult!.Value;
        foreach (DimensionLog log in dimensionResult.DimensionLogs)
            log.Op.Should().Be(log.Op.ToUpperInvariant(), $"Opcode {log.Op} should be uppercase");

        dimensionResult.DimensionLogs[0].Op.Should().Be("ADD");
        dimensionResult.DimensionLogs[1].Op.Should().Be("SSTORE");
        dimensionResult.DimensionLogs[2].Op.Should().Be("CALL");
        dimensionResult.DimensionLogs[3].Op.Should().Be("PUSH1");
    }

    [Test]
    public void IsTracingGasDimension_ReturnsTrue()
    {
        Transaction tx = Build.A.Transaction.TestObject;
        Block block = Build.A.Block.TestObject;
        TxGasDimensionLoggerTracer tracer = new(tx, block, DefaultOptions);

        tracer.IsTracingGasDimension.Should().BeTrue();
    }

    [Test]
    public void CaptureGasDimension_AllResourceKinds_RecordsCorrectly()
    {
        Transaction tx = Build.A.Transaction.WithHash(TestItem.KeccakA).TestObject;
        Block block = Build.A.Block.WithNumber(100).TestObject;
        TxGasDimensionLoggerTracer tracer = new(tx, block, DefaultOptions);

        MultiGas gasBefore = default;
        MultiGas gasAfter = default;
        gasAfter.Increment(ResourceKind.Computation, 10);
        gasAfter.Increment(ResourceKind.StorageAccess, 20);
        gasAfter.Increment(ResourceKind.StorageGrowth, 30);
        gasAfter.Increment(ResourceKind.HistoryGrowth, 40);

        tracer.CaptureGasDimension(pc: 0, Instruction.SSTORE, depth: 1, in gasBefore, in gasAfter, gasCost: 5000);
        tracer.MarkAsSuccess(Address.Zero, new GasConsumed(26000, 26000), [], []);
        GethLikeTxTrace result = tracer.BuildResult();

        TxGasDimensionResult dimensionResult = (TxGasDimensionResult)result.CustomTracerResult!.Value;
        DimensionLog log = dimensionResult.DimensionLogs[0];
        log.Computation.Should().Be(10);
        log.StateAccess.Should().Be(20);
        log.StateGrowth.Should().Be(30);
        log.HistoryGrowth.Should().Be(40);
    }
}
