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
using Nethermind.Serialization.Json;

namespace Nethermind.Arbitrum.Test.Tracing;

[TestFixture]
public class GasDimensionSerializationTests
{
    private readonly EthereumJsonSerializer _serializer = new();

    [Test]
    public void DimensionLog_Serializes_WithCorrectPropertyNames()
    {
        DimensionLog log = new()
        {
            Pc = 123,
            Op = "ADD",
            Depth = 1,
            OneDimensionalGasCost = 3,
            Computation = 5,
            StateAccess = 100,
            StateGrowth = 20000,
            HistoryGrowth = 50
        };

        string json = _serializer.Serialize(log);

        json.Should().Contain("\"pc\":");
        json.Should().Contain("\"op\":\"ADD\"");
        json.Should().Contain("\"depth\":");
        json.Should().Contain("\"cost\":");
        json.Should().Contain("\"cpu\":");
        json.Should().Contain("\"rw\":");
        json.Should().Contain("\"growth\":");
        json.Should().Contain("\"history\":");
    }

    [Test]
    public void DimensionLog_Serializes_WithRawNumbersNotHex()
    {
        DimensionLog log = new()
        {
            Pc = 123,
            Op = "ADD",
            Depth = 1,
            OneDimensionalGasCost = 456,
            Computation = 789
        };

        string json = _serializer.Serialize(log);

        json.Should().Contain("\"pc\":123");
        json.Should().Contain("\"cost\":456");
        json.Should().Contain("\"cpu\":789");
        json.Should().NotContain("0x"); // No hex values
    }

    [Test]
    public void DimensionLog_Serializes_OmitsZeroValues()
    {
        DimensionLog log = new()
        {
            Pc = 0,
            Op = "ADD",
            Depth = 1,
            OneDimensionalGasCost = 3,
            Computation = 5,
            StateAccess = 0,
            StateGrowth = 0,
            HistoryGrowth = 0
        };

        string json = _serializer.Serialize(log);

        json.Should().Contain("\"cpu\":");
        json.Should().NotContain("\"rw\":");
        json.Should().NotContain("\"growth\":");
        json.Should().NotContain("\"history\":");
    }

    [Test]
    public void TxGasDimensionResult_Serializes_WithCorrectStructure()
    {
        Hash256 txHash = TestItem.KeccakA;
        Transaction tx = Build.A.Transaction.WithHash(txHash).TestObject;
        Block block = Build.A.Block.WithNumber(100).WithTimestamp(1704067200).TestObject;
        TxGasDimensionLoggerTracer tracer = new(tx, block, GethTraceOptions.Default with { Tracer = TxGasDimensionLoggerTracer.TracerName });

        MultiGas gasBefore = default;
        MultiGas gasAfter = default;
        gasAfter.Increment(ResourceKind.Computation, 10);

        tracer.CaptureGasDimension(pc: 0, Instruction.ADD, depth: 1, in gasBefore, in gasAfter, gasCost: 3);
        tracer.SetIntrinsicGas(21000);
        tracer.SetPosterGas(1000);
        tracer.MarkAsSuccess(Address.Zero, new GasConsumed(22003, 22003), [], []);

        GethLikeTxTrace result = tracer.BuildResult();
        TxGasDimensionResult dimensionResult = (TxGasDimensionResult)result.CustomTracerResult!.Value;
        string json = _serializer.Serialize(dimensionResult);

        json.Should().Contain("\"gasUsed\":");
        json.Should().Contain("\"gasUsedForL1\":");
        json.Should().Contain("\"gasUsedForL2\":");
        json.Should().Contain("\"intrinsicGas\":");
        json.Should().Contain("\"failed\":");
        json.Should().Contain("\"status\":");
        json.Should().Contain("\"blockNumber\":");
        json.Should().Contain("\"blockTimestamp\":");
        json.Should().Contain("\"dim\":");
    }

    [Test]
    public void TxGasDimensionByOpcodeResult_Serializes_WithCorrectStructure()
    {
        Hash256 txHash = TestItem.KeccakA;
        Transaction tx = Build.A.Transaction.WithHash(txHash).TestObject;
        Block block = Build.A.Block.WithNumber(100).WithTimestamp(1704067200).TestObject;
        TxGasDimensionByOpcodeTracer tracer = new(tx, block, GethTraceOptions.Default with { Tracer = TxGasDimensionByOpcodeTracer.TracerName });

        MultiGas gasBefore = default;
        MultiGas gasAfter = default;
        gasAfter.Increment(ResourceKind.Computation, 10);

        tracer.CaptureGasDimension(pc: 0, Instruction.ADD, depth: 1, in gasBefore, in gasAfter, gasCost: 3);
        tracer.MarkAsSuccess(Address.Zero, new GasConsumed(21003, 21003), [], []);

        GethLikeTxTrace result = tracer.BuildResult();
        TxGasDimensionByOpcodeResult dimensionResult = (TxGasDimensionByOpcodeResult)result.CustomTracerResult!.Value;
        string json = _serializer.Serialize(dimensionResult);

        json.Should().Contain("\"gasUsed\":");
        json.Should().Contain("\"dimensions\":");
    }

    [Test]
    public void PreserveCaseDictionaryConverter_PreservesUppercaseKeys()
    {
        Hash256 txHash = TestItem.KeccakA;
        Transaction tx = Build.A.Transaction.WithHash(txHash).TestObject;
        Block block = Build.A.Block.WithNumber(100).TestObject;
        TxGasDimensionByOpcodeTracer tracer = new(tx, block, GethTraceOptions.Default with { Tracer = TxGasDimensionByOpcodeTracer.TracerName });

        MultiGas gasBefore = default;
        MultiGas gasAfter = default;
        gasAfter.Increment(ResourceKind.Computation, 10);

        tracer.CaptureGasDimension(pc: 0, Instruction.ADD, depth: 1, in gasBefore, in gasAfter, gasCost: 3);
        tracer.CaptureGasDimension(pc: 1, Instruction.SSTORE, depth: 1, in gasBefore, in gasAfter, gasCost: 5000);
        tracer.MarkAsSuccess(Address.Zero, new GasConsumed(26003, 26003), [], []);

        GethLikeTxTrace result = tracer.BuildResult();
        TxGasDimensionByOpcodeResult dimensionResult = (TxGasDimensionByOpcodeResult)result.CustomTracerResult!.Value;
        string json = _serializer.Serialize(dimensionResult);

        json.Should().Contain("\"ADD\":");
        json.Should().Contain("\"SSTORE\":");
        json.Should().NotContain("\"add\":");
        json.Should().NotContain("\"sstore\":");
    }

    [Test]
    public void GasDimensionBreakdown_Serializes_WithCorrectPropertyNames()
    {
        GasDimensionBreakdown breakdown = new()
        {
            OneDimensionalGasCost = 100,
            Computation = 50,
            StateAccess = 30,
            StateGrowth = 15,
            HistoryGrowth = 5
        };

        string json = _serializer.Serialize(breakdown);

        json.Should().Contain("\"gas1d\":100");
        json.Should().Contain("\"cpu\":50");
        json.Should().Contain("\"rw\":30");
        json.Should().Contain("\"growth\":15");
        json.Should().Contain("\"hist\":5");
    }
}
