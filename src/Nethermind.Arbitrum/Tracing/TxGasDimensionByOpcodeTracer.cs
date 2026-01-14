// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

// ReSharper disable UnusedAutoPropertyAccessor.Global - Properties are used by JSON serialization

using System.Text.Json;
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
/// Tracer that aggregates gas dimension breakdown by opcode type.
/// </summary>
public sealed class TxGasDimensionByOpcodeTracer : GethLikeNativeTxTracer, IArbitrumTxTracer
{
    public const string TracerName = "txGasDimensionByOpcode";

    private readonly Transaction? _transaction;
    private readonly Block? _block;
    private readonly Dictionary<Instruction, GasDimensionBreakdown> _dimensionsByOpcode = new();

    private ulong _gasUsed;
    private ulong _intrinsicGas;
    private ulong _posterGas;
    private bool _failed;

    public TxGasDimensionByOpcodeTracer(Transaction? transaction, Block? block, GethTraceOptions options)
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

        // Convert dictionary to use opcode names as keys
        Dictionary<string, GasDimensionBreakdown> dimensionsByName = new();
        foreach (KeyValuePair<Instruction, GasDimensionBreakdown> kvp in _dimensionsByOpcode)
        {
            string opcodeName = (kvp.Key.GetName() ?? kvp.Key.ToString()).ToUpperInvariant();
            dimensionsByName[opcodeName] = kvp.Value;
        }

        TxGasDimensionByOpcodeResult dimensionResult = new()
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
            Dimensions = dimensionsByName
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

        // Get or create the breakdown for this opcode
        if (!_dimensionsByOpcode.TryGetValue(opcode, out GasDimensionBreakdown? breakdown))
        {
            breakdown = new GasDimensionBreakdown();
            _dimensionsByOpcode[opcode] = breakdown;
        }

        // Aggregate the gas dimensions
        breakdown.OneDimensionalGasCost += (ulong)gasCost;
        breakdown.Computation += delta.Get(ResourceKind.Computation);
        breakdown.StateAccess += delta.Get(ResourceKind.StorageAccess);
        breakdown.StateGrowth += delta.Get(ResourceKind.StorageGrowth);
        breakdown.HistoryGrowth += delta.Get(ResourceKind.HistoryGrowth);
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
/// JSON converter that preserves dictionary key casing (bypasses CamelCase policy).
/// Required because EthereumJsonSerializer uses DictionaryKeyPolicy = JsonNamingPolicy.CamelCase.
/// </summary>
public sealed class PreserveCaseDictionaryConverter : JsonConverter<Dictionary<string, GasDimensionBreakdown>>
{
    public override Dictionary<string, GasDimensionBreakdown> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Read not needed for this tracer");
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, GasDimensionBreakdown> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (KeyValuePair<string, GasDimensionBreakdown> kvp in value)
        {
            writer.WritePropertyName(kvp.Key); // Use key as-is, no CamelCase conversion
            JsonSerializer.Serialize(writer, kvp.Value, options);
        }
        writer.WriteEndObject();
    }
}

/// <summary>
/// Aggregated gas dimension breakdown for a single opcode type.
/// Matches Nitro's GasesByDimension structure.
/// </summary>
public sealed class GasDimensionBreakdown
{
    [JsonPropertyName("gas1d")]
    [JsonConverter(typeof(ULongRawJsonConverter))]
    public ulong OneDimensionalGasCost { get; set; }

    [JsonPropertyName("cpu")]
    [JsonConverter(typeof(ULongRawJsonConverter))]
    public ulong Computation { get; set; }

    [JsonPropertyName("rw")]
    [JsonConverter(typeof(ULongRawJsonConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ulong StateAccess { get; set; }

    [JsonPropertyName("growth")]
    [JsonConverter(typeof(ULongRawJsonConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ulong StateGrowth { get; set; }

    [JsonPropertyName("hist")]
    [JsonConverter(typeof(ULongRawJsonConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ulong HistoryGrowth { get; set; }
}

public sealed class TxGasDimensionByOpcodeResult : BaseTxGasDimensionResult
{
    [JsonPropertyName("dimensions")]
    [JsonConverter(typeof(PreserveCaseDictionaryConverter))]
    public Dictionary<string, GasDimensionBreakdown> Dimensions { get; init; } = new();
}
