// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

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
public class TxGasDimensionByOpcodeTracerTests
{
    private static GethTraceOptions DefaultOptions => GethTraceOptions.Default with { Tracer = TxGasDimensionByOpcodeTracer.TracerName };

    [Test]
    public void CaptureGasDimension_SameOpcodeTwice_AggregatesGas()
    {
        Transaction tx = Build.A.Transaction.WithHash(TestItem.KeccakA).TestObject;
        Block block = Build.A.Block.WithNumber(100).TestObject;
        TxGasDimensionByOpcodeTracer tracer = new(tx, block, DefaultOptions);

        MultiGas gas1Before = default;
        MultiGas gas1After = default;
        gas1After.Increment(ResourceKind.Computation, 5);

        MultiGas gas2Before = default;
        gas2Before.Increment(ResourceKind.Computation, 5);
        MultiGas gas2After = default;
        gas2After.Increment(ResourceKind.Computation, 12);

        tracer.BeginGasDimensionCapture(pc: 0, Instruction.ADD, depth: 1, gas1Before);
        tracer.EndGasDimensionCapture(gas1After);
        tracer.BeginGasDimensionCapture(pc: 5, Instruction.ADD, depth: 1, gas2Before);
        tracer.EndGasDimensionCapture(gas2After);
        tracer.MarkAsSuccess(Address.Zero, new GasConsumed(21006, 21006), [], []);
        GethLikeTxTrace result = tracer.BuildResult();

        TxGasDimensionByOpcodeResult dimensionResult = (TxGasDimensionByOpcodeResult)result.CustomTracerResult!.Value;
        dimensionResult.Dimensions.Should().HaveCount(1);
        dimensionResult.Dimensions.Should().ContainKey("ADD");

        GasDimensionBreakdown addBreakdown = dimensionResult.Dimensions["ADD"];
        // gasCost = gas1After.Total - gas1Before.Total + gas2After.Total - gas2Before.Total = 5 + 7 = 12
        addBreakdown.OneDimensionalGasCost.Should().Be(12);
        // Computation = gas1After.Computation - gas1Before.Computation + gas2After.Computation - gas2Before.Computation = 5 + 7 = 12
        addBreakdown.Computation.Should().Be(12);
    }

    [Test]
    public void CaptureGasDimension_DifferentOpcodes_CreatesSeparateEntries()
    {
        Transaction tx = Build.A.Transaction.WithHash(TestItem.KeccakA).TestObject;
        Block block = Build.A.Block.WithNumber(100).TestObject;
        TxGasDimensionByOpcodeTracer tracer = new(tx, block, DefaultOptions);

        MultiGas gasEmpty = default;
        MultiGas gasComputation = default;
        gasComputation.Increment(ResourceKind.Computation, 3);
        MultiGas gasStorage = default;
        gasStorage.Increment(ResourceKind.StorageAccess, 100);

        tracer.BeginGasDimensionCapture(pc: 0, Instruction.ADD, depth: 1, gasEmpty);
        tracer.EndGasDimensionCapture(gasComputation);
        tracer.BeginGasDimensionCapture(pc: 1, Instruction.SLOAD, depth: 1, gasComputation);
        tracer.EndGasDimensionCapture(gasStorage);
        tracer.MarkAsSuccess(Address.Zero, new GasConsumed(23103, 23103), [], []);
        GethLikeTxTrace result = tracer.BuildResult();

        TxGasDimensionByOpcodeResult dimensionResult = (TxGasDimensionByOpcodeResult)result.CustomTracerResult!.Value;
        dimensionResult.Dimensions.Should().HaveCount(2);
        dimensionResult.Dimensions.Should().ContainKey("ADD");
        dimensionResult.Dimensions.Should().ContainKey("SLOAD");

        // ADD: 3 (gasComputation.Total - gasEmpty.Total)
        dimensionResult.Dimensions["ADD"].OneDimensionalGasCost.Should().Be(3);
        // SLOAD: 100 - 3 = 97 (gasStorage.Total - gasComputation.Total)
        dimensionResult.Dimensions["SLOAD"].OneDimensionalGasCost.Should().Be(97);
    }

    [Test]
    public void BuildResult_AfterCaptures_DimensionKeysAreUppercase()
    {
        Transaction tx = Build.A.Transaction.WithHash(TestItem.KeccakA).TestObject;
        Block block = Build.A.Block.WithNumber(100).TestObject;
        TxGasDimensionByOpcodeTracer tracer = new(tx, block, DefaultOptions);

        MultiGas gasBefore = default;
        MultiGas gasAfter = default;
        gasAfter.Increment(ResourceKind.Computation, 1);

        tracer.BeginGasDimensionCapture(pc: 0, Instruction.ADD, depth: 1, gasBefore);
        tracer.EndGasDimensionCapture(gasAfter);
        tracer.BeginGasDimensionCapture(pc: 1, Instruction.SSTORE, depth: 1, gasBefore);
        tracer.EndGasDimensionCapture(gasAfter);
        tracer.BeginGasDimensionCapture(pc: 2, Instruction.PUSH1, depth: 1, gasBefore);
        tracer.EndGasDimensionCapture(gasAfter);
        tracer.MarkAsSuccess(Address.Zero, new GasConsumed(26006, 26006), [], []);
        GethLikeTxTrace result = tracer.BuildResult();

        TxGasDimensionByOpcodeResult dimensionResult = (TxGasDimensionByOpcodeResult)result.CustomTracerResult!.Value;
        foreach (string key in dimensionResult.Dimensions.Keys)
            key.Should().Be(key.ToUpperInvariant(), $"Key {key} should be uppercase");

        dimensionResult.Dimensions.Should().ContainKey("ADD");
        dimensionResult.Dimensions.Should().ContainKey("SSTORE");
        dimensionResult.Dimensions.Should().ContainKey("PUSH1");
    }

    [Test]
    public void IsTracingGasDimension_Always_ReturnsTrue()
    {
        Transaction tx = Build.A.Transaction.TestObject;
        Block block = Build.A.Block.TestObject;
        TxGasDimensionByOpcodeTracer tracer = new(tx, block, DefaultOptions);

        tracer.IsTracingGasDimension.Should().BeTrue();
    }

    [Test]
    public void SetIntrinsicAndPosterGas_WithValidValues_UpdatesL1L2Gas()
    {
        Transaction tx = Build.A.Transaction.WithHash(TestItem.KeccakA).TestObject;
        Block block = Build.A.Block.WithNumber(100).TestObject;
        TxGasDimensionByOpcodeTracer tracer = new(tx, block, DefaultOptions);

        tracer.SetIntrinsicGas(21000);
        tracer.SetPosterGas(3000);
        tracer.MarkAsSuccess(Address.Zero, new GasConsumed(28000, 28000), [], []);
        GethLikeTxTrace result = tracer.BuildResult();

        TxGasDimensionByOpcodeResult dimensionResult = (TxGasDimensionByOpcodeResult)result.CustomTracerResult!.Value;
        dimensionResult.IntrinsicGas.Should().Be(21000);
        dimensionResult.GasUsed.Should().Be(28000);
        dimensionResult.GasUsedForL1.Should().Be(3000);
        dimensionResult.GasUsedForL2.Should().Be(25000);
    }

    [Test]
    public void BuildResult_WithFailure_SetsFailedTrue()
    {
        Transaction tx = Build.A.Transaction.WithHash(TestItem.KeccakA).TestObject;
        Block block = Build.A.Block.WithNumber(100).TestObject;
        TxGasDimensionByOpcodeTracer tracer = new(tx, block, DefaultOptions);

        tracer.MarkAsFailed(Address.Zero, new GasConsumed(21000, 21000), [], "execution reverted");
        GethLikeTxTrace result = tracer.BuildResult();

        TxGasDimensionByOpcodeResult dimensionResult = (TxGasDimensionByOpcodeResult)result.CustomTracerResult!.Value;
        dimensionResult.Failed.Should().BeTrue();
        dimensionResult.Status.Should().Be(0);
    }
}
