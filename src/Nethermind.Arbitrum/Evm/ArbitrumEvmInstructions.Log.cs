// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Runtime.CompilerServices;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.Gas;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Evm;

/// <summary>
/// Interface for specifying topic count in LOG operations.
/// </summary>
internal interface IArbitrumOpCount
{
    static abstract int Count { get; }
}

internal struct ArbitrumOp0 : IArbitrumOpCount { public static int Count => 0; }
internal struct ArbitrumOp1 : IArbitrumOpCount { public static int Count => 1; }
internal struct ArbitrumOp2 : IArbitrumOpCount { public static int Count => 2; }
internal struct ArbitrumOp3 : IArbitrumOpCount { public static int Count => 3; }
internal struct ArbitrumOp4 : IArbitrumOpCount { public static int Count => 4; }

/// <summary>
/// Custom LOG instruction handlers for Arbitrum with multi-dimensional gas tracking.
/// </summary>
internal static partial class ArbitrumEvmInstructions
{
    /// <summary>
    /// LOG instruction with multi-dimensional gas tracking.
    /// Base cost → Computation, topics → split between HistoryGrowth and Computation, data → HistoryGrowth.
    /// </summary>
    [SkipLocalsInit]
    internal static EvmExceptionType InstructionLog<TOpCount, TTracingInst>(
        VirtualMachine<MultiGasPolicy> vm,
        ref EvmStack stack,
        ref GasState gasState,
        ref int programCounter)
        where TOpCount : struct, IArbitrumOpCount
        where TTracingInst : struct, IFlag
    {
        EvmState vmState = vm.EvmState;

        // Logging is not permitted in static call contexts
        if (vmState.IsStatic)
            return EvmExceptionType.StaticCallViolation;

        // Pop memory offset and length for the log data
        if (!stack.PopUInt256(out UInt256 position) || !stack.PopUInt256(out UInt256 length))
            return EvmExceptionType.StackUnderflow;

        int topicsCount = TOpCount.Count;
        Instruction instruction = Instruction.LOG0 + (byte)topicsCount;

        // Update memory cost → Computation (handled by ConsumeGas in UpdateMemoryCost)
        if (!EvmCalculations.UpdateMemoryCost<MultiGasPolicy>(vmState, ref gasState, in position, length, instruction))
            return EvmExceptionType.OutOfGas;

        // Calculate total scalar gas cost
        long gasCost = GasCostOf.Log + topicsCount * GasCostOf.LogTopic + (long)length * GasCostOf.LogData;

        // Compute MultiGas with proper categorization
        MultiGas multiGas = ArbitrumGas.GasLog(topicsCount, (int)length);

        // Deduct gas with proper MultiGas tracking
        MultiGasPolicy.ConsumeGasWithMultiGas(ref gasState, gasCost, in multiGas);
        if (MultiGasPolicy.GetRemainingGas(in gasState) < 0)
            return EvmExceptionType.OutOfGas;

        // Load the log data from memory
        ReadOnlyMemory<byte> data = vmState.Memory.Load(in position, length);

        // Prepare the topics array
        Hash256[] topics = new Hash256[topicsCount];
        for (int i = 0; i < topics.Length; i++)
        {
            topics[i] = new Hash256(stack.PopWord256());
        }

        // Create a new log entry
        LogEntry logEntry = new(
            vmState.Env.ExecutingAccount,
            data.ToArray(),
            topics);
        vmState.AccessTracker.Logs.Add(logEntry);

        // Report the log if tracing is enabled
        if (vm.TxTracer.IsTracingLogs)
            vm.TxTracer.ReportLog(logEntry);

        return EvmExceptionType.None;
    }
}
