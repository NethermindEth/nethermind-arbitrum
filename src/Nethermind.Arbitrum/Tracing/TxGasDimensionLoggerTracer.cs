// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

// ReSharper disable UnusedAutoPropertyAccessor.Global - Properties are used by JSON serialization

using System.Text.Json.Serialization;
using Nethermind.Arbitrum.Evm;
using Nethermind.Blockchain.Tracing.GethStyle;
using Nethermind.Blockchain.Tracing.GethStyle.Custom;
using Nethermind.Blockchain.Tracing.GethStyle.Custom.Native;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using Nethermind.Serialization.Json;

namespace Nethermind.Arbitrum.Tracing;

/// <summary>
/// Tracer that captures per-opcode gas dimension breakdown.
/// </summary>
public sealed class TxGasDimensionLoggerTracer : GethLikeNativeTxTracer, IArbitrumTxTracer
{
    public const string TracerName = "txGasDimensionLogger";

    private readonly Transaction? _transaction;
    private readonly Block? _block;
    private readonly List<DimensionLog> _logs = new(256);

    private ulong _gasUsed;
    private ulong _intrinsicGas;
    private ulong _posterGas;
    private bool _failed;

    public TxGasDimensionLoggerTracer(Transaction? transaction, Block? block, GethTraceOptions options)
        : base(options)
    {
        _transaction = transaction;
        _block = block;
        IsTracingActions = true;
    }

    public bool IsTracingGasDimension => true;

    public override GethLikeTxTrace BuildResult()
    {
        GethLikeTxTrace result = base.BuildResult();

        // L1 gas is the poster gas (L1 data posting cost)
        // L2 gas is everything else (computation, storage, etc.)
        ulong gasUsedForL1 = _posterGas;
        ulong gasUsedForL2 = _gasUsed > gasUsedForL1 ? _gasUsed - gasUsedForL1 : 0;

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

    public override void MarkAsSuccess(Address recipient, GasConsumed gasSpent, byte[] output, LogEntry[] logs, Hash256? stateRoot = null)
    {
        base.MarkAsSuccess(recipient, gasSpent, output, logs, stateRoot);
        _gasUsed = (ulong)gasSpent.SpentGas;
        _failed = false;
    }

    public override void MarkAsFailed(Address recipient, GasConsumed gasSpent, byte[] output, string? error, Hash256? stateRoot = null)
    {
        base.MarkAsFailed(recipient, gasSpent, output, error, stateRoot);
        _gasUsed = (ulong)gasSpent.SpentGas;
        _failed = true;
    }

    public void CaptureGasDimension(
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

    public void SetIntrinsicGas(long intrinsicGas)
    {
        _intrinsicGas = (ulong)intrinsicGas;
    }

    public void SetPosterGas(ulong posterGas)
    {
        _posterGas = posterGas;
    }

    public void CaptureArbitrumTransfer(Address? from, Address? to, UInt256 value, bool before, BalanceChangeReason reason)
    {
    }

    public void CaptureArbitrumStorageGet(UInt256 index, int depth, bool before)
    {
    }

    public void CaptureArbitrumStorageSet(UInt256 index, ValueHash256 value, int depth, bool before)
    {
    }

    public void CaptureStylusHostio(string name, ReadOnlySpan<byte> args, ReadOnlySpan<byte> outs, ulong startInk, ulong endInk)
    {
    }
}

/// <summary>
/// Per-opcode gas dimension breakdown matching Nitro's DimensionLogRes.
/// </summary>
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

/// <summary>
/// Base class for gas dimension tracer results matching Nitro's BaseExecutionResult.
/// </summary>
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

/// <summary>
/// Result structure for txGasDimensionLogger matching Nitro's ExecutionResult.
/// </summary>
public sealed class TxGasDimensionResult : BaseTxGasDimensionResult
{
    [JsonPropertyName("dim")]
    public List<DimensionLog> DimensionLogs { get; init; } = [];
}
