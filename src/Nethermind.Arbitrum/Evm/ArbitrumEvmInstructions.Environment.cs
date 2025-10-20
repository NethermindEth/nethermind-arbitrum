// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Runtime.CompilerServices;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Int256;
using static Nethermind.Arbitrum.Evm.ArbitrumVirtualMachine;

namespace Nethermind.Arbitrum.Evm;

internal static class ArbitrumEvmInstructions
{
    /// <summary>
    /// Executes an environment introspection opcode that returns a UInt256 value.
    /// </summary>
    /// <param name="vm">The virtual machine instance.</param>
    /// <param name="stack">The execution stack.</param>
    /// <param name="gasAvailable">The available gas which is reduced by the operation's cost.</param>
    /// <param name="programCounter">The program counter.</param>
    /// <returns>An EVM exception type if an error occurs.</returns>
    [SkipLocalsInit]
    public static EvmExceptionType InstructionBlkUInt256<TTracingInst>(VirtualMachine vm, ref EvmStack stack, ref long gasAvailable, ref int programCounter)
        where TTracingInst : struct, IFlag
    {
        gasAvailable -= OpGasPrice.GasCost;

        ref readonly UInt256 result = ref OpGasPrice.Operation((ArbitrumVirtualMachine)vm);

        stack.PushUInt256<TTracingInst>(in result);

        return EvmExceptionType.None;
    }

    /// <summary>
    /// Returns the gas price for the transaction.
    /// </summary>
    public struct OpGasPrice
    {
        public static long GasCost => GasCostOf.Base;

        public static ref readonly UInt256 Operation(ArbitrumVirtualMachine vm)
        {
            return ref vm.FreeArbosState.CurrentArbosVersion is < ArbosVersion.Three or
                ArbosVersion.Nine
                ? ref vm.TxExecutionContext.GasPrice
                : ref vm.BlockExecutionContext.Header.BaseFeePerGas;
        }
    }

    /// <summary>
    /// Executes an environment introspection opcode that returns a UInt64 value.
    /// </summary>
    /// <typeparam name="TOpEnv">The specific operation implementation.</typeparam>
    /// <param name="vm">The virtual machine instance.</param>
    /// <param name="stack">The execution stack.</param>
    /// <param name="gasAvailable">The available gas which is reduced by the operation's cost.</param>
    /// <param name="programCounter">The program counter.</param>
    /// <returns>An EVM exception type if an error occurs.</returns>
    [SkipLocalsInit]
    public static EvmExceptionType InstructionBlkUInt64<TTracingInst>(VirtualMachine vm, ref EvmStack stack, ref long gasAvailable, ref int programCounter)
        where TTracingInst : struct, IFlag
    {
        gasAvailable -= OpNumber.GasCost;

        ulong result = OpNumber.Operation((ArbitrumVirtualMachine)vm);

        stack.PushUInt64<TTracingInst>(result);

        return EvmExceptionType.None;
    }

    /// <summary>
    /// Returns the L1 block number of the current L2 block.
    /// Implements per-transaction caching.
    /// </summary>
    public struct OpNumber
    {
        public static long GasCost => GasCostOf.Base;

        public static ulong Operation(ArbitrumVirtualMachine vm)
        {
            if (vm.ArbitrumTxExecutionContext.CachedL1BlockNumber.HasValue)
                return vm.ArbitrumTxExecutionContext.CachedL1BlockNumber.Value;

            ulong blockNumber = vm.FreeArbosState.Blockhashes.GetL1BlockNumber();
            vm.ArbitrumTxExecutionContext.CachedL1BlockNumber = blockNumber;

            return blockNumber;
        }
    }

    [SkipLocalsInit]
    public static EvmExceptionType InstructionBlockHash<TTracingInst>(VirtualMachine vm, ref EvmStack stack, ref long gasAvailable, ref int programCounter)
        where TTracingInst : struct, IFlag
    {
        gasAvailable -= GasCostOf.BlockHash;

        if (!stack.PopUInt256(out UInt256 a))
            return EvmExceptionType.StackUnderflow;

        if (a.IsLargerThanULong())
        {
            stack.PushBytes<TTracingInst>(BytesZero32);
            return EvmExceptionType.None;
        }

        ulong l1BlockNumber = a.u0;
        ArbitrumVirtualMachine arbitrumVirtualMachine = (ArbitrumVirtualMachine)vm;

        ulong upper = arbitrumVirtualMachine.FreeArbosState.Blockhashes.GetL1BlockNumber();
        ulong lower = upper < 257 ? 0 : upper - 256;

        Hash256? blockHash = null;

        if (l1BlockNumber >= lower && l1BlockNumber < upper)
        {
            if (arbitrumVirtualMachine.ArbitrumTxExecutionContext.CachedL1BlockHashes.TryGetValue(l1BlockNumber, out Hash256? cachedHash))
                blockHash = cachedHash;
            else
            {
                blockHash = arbitrumVirtualMachine.FreeArbosState.Blockhashes.GetL1BlockHash(l1BlockNumber);

                if (blockHash is not null)
                    arbitrumVirtualMachine.ArbitrumTxExecutionContext.CachedL1BlockHashes[l1BlockNumber] = blockHash;
            }
        }

        stack.PushBytes<TTracingInst>(blockHash is not null ? blockHash.Bytes : BytesZero32);

        if (vm.TxTracer.IsTracingBlockHash && blockHash is not null)
            vm.TxTracer.ReportBlockHash(blockHash);

        return EvmExceptionType.None;
    }
}
