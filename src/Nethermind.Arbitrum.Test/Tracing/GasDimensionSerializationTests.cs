// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Text.Json;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Blockchain.Tracing.GethStyle;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Serialization.Json;
using NUnit.Framework;

namespace Nethermind.Arbitrum.Test.Tracing;

[TestFixture]
public class GasDimensionSerializationTests
{
    private readonly EthereumJsonSerializer _serializer = new();

    private static void AssertJsonEquals(string actual, string expected)
    {
        Assert.That(
            JsonElement.DeepEquals(
                JsonDocument.Parse(actual).RootElement,
                JsonDocument.Parse(expected).RootElement),
            $"JSON mismatch.\nActual: {actual}\nExpected: {expected}");
    }

    [Test]
    public void Serialize_DimensionLogWithAllFields_IncludesAllProperties()
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

        const string expected = """
            {
                "pc": 123,
                "op": "ADD",
                "depth": 1,
                "cost": 3,
                "cpu": 5,
                "rw": 100,
                "growth": 20000,
                "history": 50
            }
            """;

        AssertJsonEquals(json, expected);
    }

    [Test]
    public void Serialize_DimensionLogWithZeroOptional_OmitsZeroFields()
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

        const string expected = """
            {
                "pc": 0,
                "op": "ADD",
                "depth": 1,
                "cost": 3,
                "cpu": 5
            }
            """;

        AssertJsonEquals(json, expected);
    }

    [Test]
    public void Serialize_TxGasDimensionResult_ProducesExpectedJson()
    {
        Hash256 txHash = TestItem.KeccakA;
        Transaction tx = Build.A.Transaction.WithHash(txHash).TestObject;
        Block block = Build.A.Block.WithNumber(100).WithTimestamp(1704067200).TestObject;
        TxGasDimensionLoggerTracer tracer = new(tx, block, GethTraceOptions.Default with { Tracer = TxGasDimensionLoggerTracer.TracerName });

        MultiGas gasBefore = default;
        MultiGas gasAfter = default;
        gasAfter.Increment(ResourceKind.Computation, 10);

        tracer.BeginGasDimensionCapture(pc: 0, Instruction.ADD, depth: 1, gasBefore);
        tracer.EndGasDimensionCapture(gasAfter);
        tracer.SetIntrinsicGas(21000);
        tracer.SetPosterGas(1000);
        tracer.MarkAsSuccess(Address.Zero, new GasConsumed(22003, 22003), [], []);

        GethLikeTxTrace result = tracer.BuildResult();
        TxGasDimensionResult dimensionResult = (TxGasDimensionResult)result.CustomTracerResult!.Value;
        string json = _serializer.Serialize(dimensionResult);

        string expected = $$"""
            {
                "gasUsed": 22003,
                "gasUsedForL1": 1000,
                "gasUsedForL2": 21003,
                "intrinsicGas": 21000,
                "adjustedRefund": 0,
                "rootIsPrecompile": false,
                "rootIsPrecompileAdjustment": 0,
                "rootIsStylus": false,
                "rootIsStylusAdjustment": 0,
                "failed": false,
                "txHash": "{{txHash}}",
                "blockTimestamp": 1704067200,
                "blockNumber": 100,
                "status": 1,
                "dim": [
                    {
                        "pc": 0,
                        "op": "ADD",
                        "depth": 1,
                        "cost": 10,
                        "cpu": 10
                    }
                ]
            }
            """;

        AssertJsonEquals(json, expected);
    }

    [Test]
    public void Serialize_TxGasDimensionByOpcodeResult_ProducesExpectedJson()
    {
        Hash256 txHash = TestItem.KeccakA;
        Transaction tx = Build.A.Transaction.WithHash(txHash).TestObject;
        Block block = Build.A.Block.WithNumber(100).WithTimestamp(1704067200).TestObject;
        TxGasDimensionByOpcodeTracer tracer = new(tx, block, GethTraceOptions.Default with { Tracer = TxGasDimensionByOpcodeTracer.TracerName });

        MultiGas gasBefore = default;
        MultiGas gasAfter = default;
        gasAfter.Increment(ResourceKind.Computation, 10);

        tracer.BeginGasDimensionCapture(pc: 0, Instruction.ADD, depth: 1, gasBefore);
        tracer.EndGasDimensionCapture(gasAfter);
        tracer.MarkAsSuccess(Address.Zero, new GasConsumed(21003, 21003), [], []);

        GethLikeTxTrace result = tracer.BuildResult();
        TxGasDimensionByOpcodeResult dimensionResult = (TxGasDimensionByOpcodeResult)result.CustomTracerResult!.Value;
        string json = _serializer.Serialize(dimensionResult);

        string expected = $$"""
            {
                "gasUsed": 21003,
                "gasUsedForL1": 0,
                "gasUsedForL2": 21003,
                "intrinsicGas": 0,
                "adjustedRefund": 0,
                "rootIsPrecompile": false,
                "rootIsPrecompileAdjustment": 0,
                "rootIsStylus": false,
                "rootIsStylusAdjustment": 0,
                "failed": false,
                "txHash": "{{txHash}}",
                "blockTimestamp": 1704067200,
                "blockNumber": 100,
                "status": 1,
                "dimensions": {
                    "ADD": {
                        "gas1d": 10,
                        "cpu": 10
                    }
                }
            }
            """;

        AssertJsonEquals(json, expected);
    }

    [Test]
    public void Serialize_TxGasDimensionByOpcodeResultWithMultipleOpcodes_PreservesUppercaseKeys()
    {
        Hash256 txHash = TestItem.KeccakA;
        Transaction tx = Build.A.Transaction.WithHash(txHash).TestObject;
        Block block = Build.A.Block.WithNumber(100).WithTimestamp(1704067200).TestObject;
        TxGasDimensionByOpcodeTracer tracer = new(tx, block, GethTraceOptions.Default with { Tracer = TxGasDimensionByOpcodeTracer.TracerName });

        MultiGas gasBefore = default;
        MultiGas gasAfter = default;
        gasAfter.Increment(ResourceKind.Computation, 10);

        tracer.BeginGasDimensionCapture(pc: 0, Instruction.ADD, depth: 1, gasBefore);
        tracer.EndGasDimensionCapture(gasAfter);
        tracer.BeginGasDimensionCapture(pc: 1, Instruction.SSTORE, depth: 1, gasBefore);
        tracer.EndGasDimensionCapture(gasAfter);
        tracer.MarkAsSuccess(Address.Zero, new GasConsumed(26003, 26003), [], []);

        GethLikeTxTrace result = tracer.BuildResult();
        TxGasDimensionByOpcodeResult dimensionResult = (TxGasDimensionByOpcodeResult)result.CustomTracerResult!.Value;
        string json = _serializer.Serialize(dimensionResult);

        string expected = $$"""
            {
                "gasUsed": 26003,
                "gasUsedForL1": 0,
                "gasUsedForL2": 26003,
                "intrinsicGas": 0,
                "adjustedRefund": 0,
                "rootIsPrecompile": false,
                "rootIsPrecompileAdjustment": 0,
                "rootIsStylus": false,
                "rootIsStylusAdjustment": 0,
                "failed": false,
                "txHash": "{{txHash}}",
                "blockTimestamp": 1704067200,
                "blockNumber": 100,
                "status": 1,
                "dimensions": {
                    "ADD": {
                        "gas1d": 10,
                        "cpu": 10
                    },
                    "SSTORE": {
                        "gas1d": 10,
                        "cpu": 10
                    }
                }
            }
            """;

        AssertJsonEquals(json, expected);
    }

    [Test]
    public void Serialize_GasDimensionBreakdownWithAllFields_IncludesAllProperties()
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

        const string expected = """
            {
                "gas1d": 100,
                "cpu": 50,
                "rw": 30,
                "growth": 15,
                "hist": 5
            }
            """;

        AssertJsonEquals(json, expected);
    }

    [Test]
    public void Serialize_GasDimensionBreakdownWithZeroOptional_OmitsZeroFields()
    {
        GasDimensionBreakdown breakdown = new()
        {
            OneDimensionalGasCost = 100,
            Computation = 50,
            StateAccess = 0,
            StateGrowth = 0,
            HistoryGrowth = 0
        };

        string json = _serializer.Serialize(breakdown);

        const string expected = """
            {
                "gas1d": 100,
                "cpu": 50
            }
            """;

        AssertJsonEquals(json, expected);
    }
}
