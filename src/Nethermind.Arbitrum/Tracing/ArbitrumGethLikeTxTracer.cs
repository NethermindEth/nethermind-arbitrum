using CommunityToolkit.HighPerformance.Helpers;
using Microsoft.AspNetCore.Components.Forms;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Evm.Tracing.GethStyle;
using Nethermind.Evm.Tracing.GethStyle.Custom.Native.Prestate;
using Nethermind.Int256;
using Nethermind.State;
// ReSharper disable UseCollectionExpression

namespace Nethermind.Arbitrum.Tracing;

public class ArbitrumNativePrestateTracer(
    IWorldState worldState,
    GethTraceOptions options,
    Hash256? txHash,
    Address? from,
    Address? to = null,
    Address? beneficiary = null)
    : NativePrestateTracer(worldState, options, txHash, from, to, beneficiary), IArbitrumTxTracer
{
    public void CaptureArbitrumTransferHook(Address from, Address to, UInt256 value, bool before, string reason)
    {
    }

    public void CaptureArbitrumStorageGetHook(UInt256 index, int depth, bool before)
    {
        LookupAccount(ArbosAddresses.ArbosSystemAccount);
        LookupStorage(ArbosAddresses.ArbosSystemAccount, index);
    }

    public void CaptureArbitrumStorageSetHook(UInt256 index, Hash256 value, int depth, bool before)
    {
        LookupAccount(ArbosAddresses.ArbosSystemAccount);
        LookupStorage(ArbosAddresses.ArbosSystemAccount, index);
    }

    public void CaptureStylusHostioHook(string name, ReadOnlySpan<byte> args, ReadOnlySpan<byte> outs, ulong startInk, ulong endInk)
    {
    }
}

public class ArbitrumGethLikeTxTracer(GethTraceOptions options) : GethLikeTxTracer(options), IArbitrumTxTracer
{
    public void CaptureArbitrumTransferHook(Address? from, Address? to, UInt256 value, bool before, string reason)
    {
        var transfer = new ArbitrumTransfer
        {
            Purpose = reason,
            Value = value
        };

        if (from != null) transfer.From = from;
        if (to != null) transfer.To = to;

        if (before)
            Trace.BeforeEvmTransfers.Add(transfer);
        else
            Trace.AfterEvmTransfers.Add(transfer);
    }

    public void CaptureArbitrumStorageGetHook(UInt256 index, int depth, bool before)
    {
    }

    public void CaptureArbitrumStorageSetHook(UInt256 index, Hash256 value, int depth, bool before)
    {
    }

    public void CaptureStylusHostioHook(string name, ReadOnlySpan<byte> args, ReadOnlySpan<byte> outs, ulong startInk, ulong endInk)
    {
    }
}

public static class AribitrumTracingExtension
{
    private static readonly byte[] MockReturnStack = TracingStackFromArgs(stackalloc UInt256[] { UInt256.Zero, UInt256.Zero });
    private static readonly byte[] MockReturnPop = TracingStackFromArgs(stackalloc UInt256[] { UInt256.One });

    public static void RecordStorageGet(this IArbitrumTxTracer tracer, in ExecutionEnvironment env, Hash256 key, TracingScenario scenario)
    {
        if (scenario == TracingScenario.TracingDuringEvm)
        {
            // ReSharper disable once UseCollectionExpression
            var stack = TracingStackFromArgs(stackalloc UInt256[] { new UInt256(key.Bytes) }); 
            TraceInstruction(tracer, env, new TraceMemory(), new TraceStack(stack), Instruction.SLOAD);
        }
        else
        {
            tracer.CaptureArbitrumStorageGetHook(new UInt256(key.Bytes), env.CallDepth,
                scenario == TracingScenario.TracingBeforeEvm);
        }
    }
    
    public static void RecordStorageSet(this IArbitrumTxTracer tracer, in ExecutionEnvironment env, Hash256 key, Hash256 value, TracingScenario scenario)
    {
        if (scenario == TracingScenario.TracingDuringEvm)
        {
            var stack = TracingStackFromArgs(stackalloc UInt256[] { new UInt256(key.Bytes), new UInt256(value.Bytes) }); 
            TraceInstruction(tracer, env, new TraceMemory(), new TraceStack(stack), Instruction.SSTORE);
        }
        else
        {
            tracer.CaptureArbitrumStorageSetHook(new UInt256(key.Bytes), value, env.CallDepth,
                scenario == TracingScenario.TracingBeforeEvm);
        }
    }

    public static void MockCall(this ITxTracer tracer, in ExecutionEnvironment env, Address from, Address to,
        UInt256 amount, long gas, byte[] input)
    {
        var memoryCall = new TraceMemory((ulong)input.Length, input);
        Span<UInt256> callArgs = stackalloc UInt256[7];
        callArgs[0] = (UInt256)gas;
        callArgs[1] = new UInt256(to.Bytes);
        callArgs[2] = amount;
        callArgs[3] = 0; // memory offset
        callArgs[4] = (UInt256)input.Length; // memory length 
        callArgs[5] = 0; // return offset
        callArgs[6] = 0; // return size
        
        var stackCall = new TraceStack(TracingStackFromArgs(callArgs));
        TraceInstruction(tracer, env, memoryCall, stackCall, Instruction.CALL);
        
        tracer.ReportAction(gas, amount, from, to, input, ExecutionType.CALL);

        var stackReturn = new TraceStack(MockReturnStack);
        TraceInstruction(tracer, env, new TraceMemory(), stackReturn, Instruction.RETURN);

        tracer.ReportActionEnd(gas, Array.Empty<byte>());

        var stackPop = new TraceStack(MockReturnPop);
        TraceInstruction(tracer, env, new TraceMemory(), stackPop, Instruction.POP);
    }

    private static void TraceInstruction(ITxTracer tracer, ExecutionEnvironment env, TraceMemory memory, TraceStack stack, Instruction instruction)
    {
        tracer.StartOperation(0, instruction, 0, env, 0,0);
        if (tracer.IsTracingMemory)
        {
            tracer.SetOperationMemory(memory);
            tracer.SetOperationMemorySize(memory.Size);
        }

        if (tracer.IsTracingStack)
        {
            tracer.SetOperationStack(stack); 
        }
    }


    private static byte[] TracingStackFromArgs(ReadOnlySpan<UInt256> args)
    {
        if (args.IsEmpty)
        {
            return [];
        }

        var stackBytes = new byte[args.Length * 32];
        var span = stackBytes.AsSpan();

        for (var i = 0; i < args.Length; i++)
        {
            args[args.Length - 1 - i].ToBigEndian(span.Slice(i * 32, 32));
        }

        return stackBytes;
    }
}

