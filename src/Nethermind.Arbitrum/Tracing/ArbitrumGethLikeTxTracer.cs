using Nethermind.Blockchain.Tracing.GethStyle;
using Nethermind.Blockchain.Tracing.GethStyle.Custom;
using Nethermind.Blockchain.Tracing.GethStyle.Custom.Native;
using Nethermind.Blockchain.Tracing.GethStyle.Custom.Native.Call;
using Nethermind.Core;
using Nethermind.Core.Collections;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using Nethermind.Serialization.Json;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nethermind.Core.Extensions;

namespace Nethermind.Arbitrum.Tracing;

public class ArbitrumTransfer(string purpose, Address? from, Address? to, UInt256 amount)
{
    public string Purpose { get; } = purpose;
    public Address? From { get; } = from;
    public Address? To { get; } = to;
    public UInt256 Value { get; } = amount;
}

public sealed class ArbitrumGethLikeTxTracer : GethLikeTxMemoryTracer, IArbitrumTxTracer
{
    public ArbitrumGethLikeTxTracer(GethTraceOptions options) : base(null, options)
    {
        IsTracingStorage = true;
    }

    public ArbitrumGethLikeTxTracer(Transaction? tx, GethTraceOptions options) : base(tx, options)
    {
        IsTracingStorage = true;
    }
    public List<ArbitrumTransfer> BeforeEvmTransfers { get; } = new();

    public List<ArbitrumTransfer> AfterEvmTransfers { get; } = new();

    public void CaptureArbitrumTransfer(Address? from, Address? to, UInt256 value, bool before,
        BalanceChangeReason reason)
    {
        ArbitrumTransfer transfer = new ArbitrumTransfer(reason.ToString(), from, to, value);

        if (before)
            BeforeEvmTransfers.Add(transfer);
        else
            AfterEvmTransfers.Add(transfer);
    }

    public void CaptureArbitrumStorageGet(UInt256 index, int depth, bool before)
    {
    }

    public void CaptureArbitrumStorageSet(UInt256 index, ValueHash256 value, int depth, bool before)
    {
    }

    public void CaptureStylusHostio(string name, ReadOnlySpan<byte> args, ReadOnlySpan<byte> outs, ulong startInk,
        ulong endInk)
    {
    }
}

[JsonConverter(typeof(ArbitrumNativeCallTracerCallFrameConverter))]
public class ArbitrumNativeCallFrame : NativeCallTracerCallFrame
{
    public List<ArbitrumTransfer> BeforeEvmTransfers { get; } = [];
    public List<ArbitrumTransfer> AfterEvmTransfers { get; } = [];
}

public class ArbitrumNativeCallTracerCallFrameConverter : JsonConverter<ArbitrumNativeCallFrame>
{
    public override void Write(Utf8JsonWriter writer, ArbitrumNativeCallFrame value, JsonSerializerOptions options)
    {
        NumberConversion? previousValue = ForcedNumberConversion.ForcedConversion.Value;
        try
        {
            writer.WriteStartObject();

            ForcedNumberConversion.ForcedConversion.Value = NumberConversion.Hex;
            writer.WritePropertyName("type"u8);
            JsonSerializer.Serialize(writer, value.Type.GetName(), options);

            writer.WritePropertyName("from"u8);
            JsonSerializer.Serialize(writer, value.From, options);

            if (value.To is not null)
            {
                writer.WritePropertyName("to"u8);
                JsonSerializer.Serialize(writer, value.To, options);
            }

            if (value.Value is not null)
            {
                writer.WritePropertyName("value"u8);
                JsonSerializer.Serialize(writer, value.Value, options);
            }

            writer.WritePropertyName("gas"u8);
            JsonSerializer.Serialize(writer, value.Gas, options);

            writer.WritePropertyName("gasUsed"u8);
            JsonSerializer.Serialize(writer, value.GasUsed, options);

            writer.WritePropertyName("input"u8);
            if (value.Input is null || value.Input.Count == 0)
            {
                writer.WriteStringValue("0x"u8);
            }
            else
            {
                JsonSerializer.Serialize(writer, value.Input.AsReadOnlyMemory(), options);
            }

            if (value.Output?.Count > 0)
            {
                writer.WritePropertyName("output"u8);
                JsonSerializer.Serialize(writer, value.Output.AsReadOnlyMemory(), options);
            }

            if (value.Error is not null)
            {
                writer.WritePropertyName("error"u8);
                JsonSerializer.Serialize(writer, value.Error, options);
            }

            if (value.RevertReason is not null)
            {
                writer.WritePropertyName("revertReason"u8);
                JsonSerializer.Serialize(writer, value.RevertReason, options);
            }

            if (value.Logs?.Count > 0)
            {
                writer.WritePropertyName("logs"u8);
                JsonSerializer.Serialize(writer, value.Logs.AsMemory(), options);
            }

            if (value.Calls?.Count > 0)
            {
                writer.WritePropertyName("calls"u8);
                JsonSerializer.Serialize(writer, value.Calls.AsMemory(), options);
            }

            if (value.BeforeEvmTransfers?.Count > 0)
            {
                writer.WritePropertyName("beforeEvmTransfers"u8);
                JsonSerializer.Serialize(writer, value.BeforeEvmTransfers, options);
            }

            if (value.AfterEvmTransfers?.Count > 0)
            {
                writer.WritePropertyName("afterEvmTransfers"u8);
                JsonSerializer.Serialize(writer, value.AfterEvmTransfers, options);
            }

            writer.WriteEndObject();
        }
        finally
        {
            ForcedNumberConversion.ForcedConversion.Value = previousValue;
        }
    }

    public override ArbitrumNativeCallFrame Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException();
    }
}

public sealed class ArbitrumNativeCallTracer : GethLikeNativeTxTracer, IArbitrumTxTracer
{
    public const string CallTracer = "callTracer";

    private readonly long _gasLimit;
    private readonly Hash256? _txHash;
    private readonly NativeCallTracerConfig _config;
    private readonly ArrayPoolList<ArbitrumNativeCallFrame> _callStack = new(1024);
    private readonly CompositeDisposable _disposables = new();

    private EvmExceptionType? _error;
    private long _remainingGas;
    private bool _resultBuilt = false;

    public ArbitrumNativeCallTracer(
        Transaction? tx,
        GethTraceOptions options) : base(options)
    {
        IsTracingActions = true;
        _gasLimit = tx!.GasLimit;
        _txHash = tx.Hash;

        _config = options.TracerConfig?.Deserialize<NativeCallTracerConfig>(EthereumJsonSerializer.JsonOptions) ?? new NativeCallTracerConfig();

        if (_config.WithLog)
        {
            IsTracingLogs = true;
        }
    }

    protected override GethLikeTxTrace CreateTrace() => new(_disposables);

    public override GethLikeTxTrace BuildResult()
    {
        GethLikeTxTrace result = base.BuildResult();
        ArbitrumNativeCallFrame firstCallFrame = _callStack[0];

        Debug.Assert(_callStack.Count == 1, $"Unexpected frames on call stack, expected only master frame, found {_callStack.Count} frames.");

        _callStack.RemoveAt(0);
        _disposables.Add(firstCallFrame);

        result.TxHash = _txHash;
        result.CustomTracerResult = new GethLikeCustomTrace { Value = firstCallFrame };

        _resultBuilt = true;

        return result;
    }

    public override void Dispose()
    {
        base.Dispose();
        for (int i = _resultBuilt ? 1 : 0; i < _callStack.Count; i++)
        {
            _callStack[i].Dispose();
        }

        _callStack.Dispose();
    }

    public override void ReportAction(long gas, UInt256 value, Address from, Address to, ReadOnlyMemory<byte> input, ExecutionType callType, bool isPrecompileCall = false)
    {
        base.ReportAction(gas, value, from, to, input, callType, isPrecompileCall);

        if (_config.OnlyTopCall && Depth > 0)
            return;

        Instruction callOpcode = callType.ToInstruction();
        ArbitrumNativeCallFrame callFrame = new()
        {
            Type = callOpcode,
            From = from,
            To = to,
            Gas = Depth == 0 ? _gasLimit : gas,
            Value = callOpcode == Instruction.STATICCALL ? null : value,
            Input = input.Span.ToPooledList()
        };
        _callStack.Add(callFrame);
    }

    public override void ReportLog(LogEntry log)
    {
        base.ReportLog(log);

        if (_config.OnlyTopCall && Depth > 0)
            return;

        NativeCallTracerCallFrame callFrame = _callStack[^1];

        NativeCallTracerLogEntry callLog = new(
            log.Address,
            log.Data,
            log.Topics,
            (ulong)callFrame.Calls.Count);

        callFrame.Logs ??= new ArrayPoolList<NativeCallTracerLogEntry>(8);
        callFrame.Logs.Add(callLog);
    }

    public override void ReportOperationRemainingGas(long gas)
    {
        base.ReportOperationRemainingGas(gas);
        _remainingGas = gas > 0 ? gas : 0;
    }

    public override void ReportActionEnd(long gas, Address deploymentAddress, ReadOnlyMemory<byte> deployedCode)
    {
        OnExit(gas, deployedCode);
        base.ReportActionEnd(gas, deploymentAddress, deployedCode);
    }

    public override void ReportActionEnd(long gas, ReadOnlyMemory<byte> output)
    {
        OnExit(gas, output);
        base.ReportActionEnd(gas, output);
    }

    public override void ReportActionError(EvmExceptionType evmExceptionType)
    {
        _error = evmExceptionType;
        OnExit(_remainingGas, null, _error);
        base.ReportActionError(evmExceptionType);
    }

    public override void ReportActionRevert(long gas, ReadOnlyMemory<byte> output)
    {
        _error = EvmExceptionType.Revert;
        OnExit(gas, output, _error);
        base.ReportActionRevert(gas, output);
    }

    public override void ReportSelfDestruct(Address address, UInt256 balance, Address refundAddress)
    {
        base.ReportSelfDestruct(address, balance, refundAddress);
        if (!_config.OnlyTopCall && _callStack.Count > 0)
        {
            NativeCallTracerCallFrame callFrame = new NativeCallTracerCallFrame
            {
                Type = Instruction.SELFDESTRUCT,
                From = address,
                To = refundAddress,
                Value = balance
            };
            _callStack[^1].Calls.Add(callFrame);
        }
    }

    public override void MarkAsSuccess(Address recipient, GasConsumed gasSpent, byte[] output, LogEntry[] logs, Hash256? stateRoot = null)
    {
        base.MarkAsSuccess(recipient, gasSpent, output, logs, stateRoot);
        NativeCallTracerCallFrame firstCallFrame = _callStack[0];
        firstCallFrame.GasUsed = gasSpent.SpentGas;
        firstCallFrame.Output = new ArrayPoolList<byte>(output);
    }

    public override void MarkAsFailed(Address recipient, GasConsumed gasSpent, byte[] output, string? error, Hash256? stateRoot = null)
    {
        base.MarkAsFailed(recipient, gasSpent, output, error, stateRoot);
        NativeCallTracerCallFrame firstCallFrame = _callStack[0];
        firstCallFrame.GasUsed = gasSpent.SpentGas;
        if (output is not null)
            firstCallFrame.Output = new ArrayPoolList<byte>(output);

        EvmExceptionType errorType = _error!.Value;
        firstCallFrame.Error = errorType.GetEvmExceptionDescription();
        if (errorType == EvmExceptionType.Revert && error is not TransactionSubstate.Revert)
        {
            firstCallFrame.RevertReason = ValidateRevertReason(error);
        }

        if (_config.WithLog)
        {
            ClearFailedLogs(firstCallFrame, false);
        }
    }

    private void OnExit(long gas, ReadOnlyMemory<byte>? output, EvmExceptionType? error = null)
    {
        if (!_config.OnlyTopCall && Depth > 0)
        {
            NativeCallTracerCallFrame callFrame = _callStack[^1];

            int size = _callStack.Count;
            if (size > 1)
            {
                _callStack.RemoveAt(size - 1);
                callFrame.GasUsed = callFrame.Gas - gas;

                ProcessOutput(callFrame, output, error);

                _callStack[^1].Calls.Add(callFrame);
            }
        }
    }

    private static void ProcessOutput(NativeCallTracerCallFrame callFrame, ReadOnlyMemory<byte>? output, EvmExceptionType? error)
    {
        if (error is not null)
        {
            callFrame.Error = error.Value.GetEvmExceptionDescription();
            if (callFrame.Type is Instruction.CREATE or Instruction.CREATE2)
            {
                callFrame.To = null;
            }

            if (error == EvmExceptionType.Revert && output?.Length != 0)
            {
                ArrayPoolList<byte>? outputList = output?.Span.ToPooledList();
                callFrame.Output = outputList;

                if (outputList?.Count >= 4)
                {
                    ProcessRevertReason(callFrame, output!.Value);
                }
            }
        }
        else
        {
            callFrame.Output = output?.Span.ToPooledList();
        }
    }

    private static void ProcessRevertReason(NativeCallTracerCallFrame callFrame, ReadOnlyMemory<byte> output)
    {
        ReadOnlySpan<byte> span = output.Span;
        string errorMessage;
        try
        {
            errorMessage = TransactionSubstate.GetErrorMessage(span)!;
        }
        catch
        {
            errorMessage = TransactionSubstate.EncodeErrorMessage(span);
        }
        callFrame.RevertReason = ValidateRevertReason(errorMessage);
    }

    private static void ClearFailedLogs(NativeCallTracerCallFrame callFrame, bool parentFailed)
    {
        bool failed = callFrame.Error is not null || parentFailed;
        if (failed)
        {
            callFrame.Logs = null;
        }

        foreach (NativeCallTracerCallFrame childCallFrame in callFrame.Calls.AsSpan())
        {
            ClearFailedLogs(childCallFrame, failed);
        }
    }

    private static string? ValidateRevertReason(string? errorMessage) =>
        errorMessage?.StartsWith("0x") == false ? errorMessage : null;

    public void CaptureArbitrumTransfer(Address? from, Address? to, UInt256 value, bool before, BalanceChangeReason reason)
    {
        if (_callStack.Count == 0)
            return;

        ArbitrumTransfer transfer = new(reason.ToString(), from, to, value);

        ArbitrumNativeCallFrame callFrame = _callStack[^1];

        if (before)
            callFrame.BeforeEvmTransfers.Add(transfer);
        else
            callFrame.AfterEvmTransfers.Add(transfer);
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
