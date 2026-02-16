// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

// ReSharper disable UnusedAutoPropertyAccessor.Global - Properties are used by JSON serialization

using System.Text.Json.Serialization;
using Nethermind.Arbitrum.Evm;
using Nethermind.Blockchain.Tracing.GethStyle;
using Nethermind.Blockchain.Tracing.GethStyle.Custom;
using Nethermind.Core;
using Nethermind.Evm;
using Nethermind.Serialization.Json;

namespace Nethermind.Arbitrum.Tracing;

/// <summary>
/// Tracer that captures per-opcode gas dimension breakdown.
/// </summary>
public sealed class TxGasDimensionLoggerTracer : GasDimensionTracerBase
{
    public const string TracerName = "txGasDimensionLogger";

    private readonly List<DimensionLog> _logs = new(256);

    public TxGasDimensionLoggerTracer(Transaction? transaction, Block? block, GethTraceOptions options)
        : base(transaction, block, options)
    {
    }

    public override GethLikeTxTrace BuildResult()
    {
        GethLikeTxTrace result = base.BuildResult();

        (ulong gasUsedForL1, ulong gasUsedForL2) = ComputeL1L2Gas();

        TxGasDimensionResult dimensionResult = new()
        {
            TxHash = _transaction?.Hash?.ToString(),
            GasUsed = _gasUsed,
            GasUsedForL1 = gasUsedForL1,
            GasUsedForL2 = gasUsedForL2,
            IntrinsicGas = _intrinsicGas,
            AdjustedRefund = 0, // TODO: Track refunds
            RootIsPrecompile = false,
            RootIsPrecompileAdjustment = 0,
            RootIsStylus = false,
            RootIsStylusAdjustment = 0,
            Failed = _failed,
            BlockTimestamp = _block?.Timestamp ?? 0,
            BlockNumber = (ulong)(_block?.Number ?? 0),
            Status = _failed ? 0UL : 1UL,
            DimensionLogs = _logs
        };

        result.TxHash = _transaction?.Hash;
        result.CustomTracerResult = new GethLikeCustomTrace { Value = dimensionResult };

        return result;
    }

    protected override void OnGasDimensionCaptured(
        int pc,
        Instruction opcode,
        int depth,
        in MultiGas gasBefore,
        in MultiGas gasAfter,
        long gasCost)
    {
        MultiGas delta = gasAfter.SaturatingSub(in gasBefore);

        DimensionLog log = new()
        {
            Pc = (ulong)pc,
            Op = (opcode.GetName() ?? opcode.ToString()).ToUpperInvariant(),
            Depth = depth,
            OneDimensionalGasCost = (ulong)gasCost,
            Computation = delta.Get(ResourceKind.Computation),
            StateAccess = delta.Get(ResourceKind.StorageAccess),
            StateGrowth = delta.Get(ResourceKind.StorageGrowth),
            HistoryGrowth = delta.Get(ResourceKind.HistoryGrowth)
        };

        _logs.Add(log);
    }
}

public sealed class DimensionLog
{
    [JsonPropertyName("pc")]
    [JsonConverter(typeof(ULongRawJsonConverter))]
    public ulong Pc { get; init; }

    [JsonPropertyName("op")]
    public string Op { get; init; } = string.Empty;

    [JsonPropertyName("depth")]
    public int Depth { get; init; }

    [JsonPropertyName("cost")]
    [JsonConverter(typeof(ULongRawJsonConverter))]
    public ulong OneDimensionalGasCost { get; init; }

    [JsonPropertyName("cpu")]
    [JsonConverter(typeof(ULongRawJsonConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ulong Computation { get; init; }

    [JsonPropertyName("rw")]
    [JsonConverter(typeof(ULongRawJsonConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ulong StateAccess { get; init; }

    [JsonPropertyName("growth")]
    [JsonConverter(typeof(ULongRawJsonConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ulong StateGrowth { get; init; }

    [JsonPropertyName("history")]
    [JsonConverter(typeof(ULongRawJsonConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ulong HistoryGrowth { get; init; }
}

public abstract class BaseTxGasDimensionResult
{
    [JsonPropertyName("gasUsed")]
    [JsonConverter(typeof(ULongRawJsonConverter))]
    public ulong GasUsed { get; init; }

    [JsonPropertyName("gasUsedForL1")]
    [JsonConverter(typeof(ULongRawJsonConverter))]
    public ulong GasUsedForL1 { get; init; }

    [JsonPropertyName("gasUsedForL2")]
    [JsonConverter(typeof(ULongRawJsonConverter))]
    public ulong GasUsedForL2 { get; init; }

    [JsonPropertyName("intrinsicGas")]
    [JsonConverter(typeof(ULongRawJsonConverter))]
    public ulong IntrinsicGas { get; init; }

    [JsonPropertyName("adjustedRefund")]
    [JsonConverter(typeof(ULongRawJsonConverter))]
    public ulong AdjustedRefund { get; init; }

    [JsonPropertyName("rootIsPrecompile")]
    public bool RootIsPrecompile { get; init; }

    [JsonPropertyName("rootIsPrecompileAdjustment")]
    [JsonConverter(typeof(ULongRawJsonConverter))]
    public ulong RootIsPrecompileAdjustment { get; init; }

    [JsonPropertyName("rootIsStylus")]
    public bool RootIsStylus { get; init; }

    [JsonPropertyName("rootIsStylusAdjustment")]
    [JsonConverter(typeof(ULongRawJsonConverter))]
    public ulong RootIsStylusAdjustment { get; init; }

    [JsonPropertyName("failed")]
    public bool Failed { get; init; }

    [JsonPropertyName("txHash")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TxHash { get; init; }

    [JsonPropertyName("blockTimestamp")]
    [JsonConverter(typeof(ULongRawJsonConverter))]
    public ulong BlockTimestamp { get; init; }

    [JsonPropertyName("blockNumber")]
    [JsonConverter(typeof(ULongRawJsonConverter))]
    public ulong BlockNumber { get; init; }

    [JsonPropertyName("status")]
    [JsonConverter(typeof(ULongRawJsonConverter))]
    public ulong Status { get; init; }
}

public sealed class TxGasDimensionResult : BaseTxGasDimensionResult
{
    [JsonPropertyName("dim")]
    public List<DimensionLog> DimensionLogs { get; init; } = [];
}
