// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics;
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
    /// <param name="gas">The gas state which is reduced by the operation's cost.</param>
    /// <param name="programCounter">The program counter.</param>
    /// <returns>An EVM exception type if an error occurs.</returns>
    [SkipLocalsInit]
    public static EvmExceptionType InstructionBlkUInt256<TTracingInst>(VirtualMachine<ArbitrumGasPolicy> vm, ref EvmStack stack, ref ArbitrumGasPolicy gas, ref int programCounter)
        where TTracingInst : struct, IFlag
    {
        ArbitrumVirtualMachine arbitrumVirtualMachine = AsArbitrum(vm);

        ArbitrumGasPolicy.Consume(ref gas, OpGasPrice.GasCost);

        ref readonly UInt256 result = ref OpGasPrice.Operation(arbitrumVirtualMachine);

        stack.PushUInt256<TTracingInst>(in result);

        return EvmExceptionType.None;
    }

    /// <summary>
    /// Executes an environment introspection opcode that returns a UInt64 value (NUMBER).
    /// </summary>
    /// <typeparam name="TTracingInst">The tracing flag.</typeparam>
    /// <param name="vm">The virtual machine instance.</param>
    /// <param name="stack">The execution stack.</param>
    /// <param name="gas">The gas state which is reduced by the operation's cost.</param>
    /// <param name="programCounter">The program counter.</param>
    /// <returns>An EVM exception type if an error occurs.</returns>
    [SkipLocalsInit]
    public static EvmExceptionType InstructionBlkUInt64<TTracingInst>(VirtualMachine<ArbitrumGasPolicy> vm, ref EvmStack stack, ref ArbitrumGasPolicy gas, ref int programCounter)
        where TTracingInst : struct, IFlag
    {
        ArbitrumVirtualMachine arbitrumVirtualMachine = AsArbitrum(vm);

        ArbitrumGasPolicy.Consume(ref gas, OpNumber.GasCost);

        ulong result = OpNumber.Operation(arbitrumVirtualMachine);

        stack.PushUInt64<TTracingInst>(result);

        return EvmExceptionType.None;
    }

    [SkipLocalsInit]
    public static EvmExceptionType InstructionBlockHash<TTracingInst>(VirtualMachine<ArbitrumGasPolicy> vm, ref EvmStack stack, ref ArbitrumGasPolicy gas, ref int programCounter)
        where TTracingInst : struct, IFlag
    {
        ArbitrumVirtualMachine arbitrumVirtualMachine = AsArbitrum(vm);

        ArbitrumGasPolicy.Consume(ref gas, GasCostOf.BlockHash);

        if (!stack.PopUInt256(out UInt256 a))
            return EvmExceptionType.StackUnderflow;

        if (a.IsLargerThanULong())
        {
            stack.PushBytes<TTracingInst>(BytesZero32);
            return EvmExceptionType.None;
        }

        ulong l1BlockNumber = a.u0;

        ulong upper = arbitrumVirtualMachine.FreeArbosState.Blockhashes.GetL1BlockNumber();
        ulong lower = upper < 257 ? 0 : upper - 256;

        Hash256? blockHash = null;

        if (l1BlockNumber >= lower && l1BlockNumber < upper)
        {
            if (arbitrumVirtualMachine.L1BlockCache.TryGetL1BlockHash(l1BlockNumber, out Hash256 cachedHash))
                blockHash = cachedHash;
            else
            {
                blockHash = arbitrumVirtualMachine.FreeArbosState.Blockhashes.GetL1BlockHash(l1BlockNumber);

                if (blockHash is not null)
                    arbitrumVirtualMachine.L1BlockCache.SetL1BlockHash(l1BlockNumber, blockHash);
            }
        }

        stack.PushBytes<TTracingInst>(blockHash is not null ? blockHash.Bytes : BytesZero32);

        if (vm.TxTracer.IsTracingBlockHash && blockHash is not null)
            vm.TxTracer.ReportBlockHash(blockHash);

        return EvmExceptionType.None;
    }

    /// <summary>
    /// Safely downcasts <see cref="VirtualMachine{TGasPolicy}"/> to <see cref="ArbitrumVirtualMachine"/>.
    /// In DEBUG builds, validates that the runtime type is correct.
    /// In RELEASE builds, uses zero-overhead <see cref="Unsafe.As{TFrom,TTo}(ref TFrom)"/>.
    /// </summary>
    /// <remarks>
    /// This cast is safe because Arbitrum-specific instructions are only registered in
    /// <see cref="ArbitrumVirtualMachine.GenerateOpCodes{TTracingInst}"/>, which is only called
    /// on <see cref="ArbitrumVirtualMachine"/> instances. The opcode delegate receives <c>this</c>
    /// (the VM instance) as the first parameter, so at runtime it is always an ArbitrumVirtualMachine.
    /// </remarks>
    /// <param name="vm">The virtual machine instance typed as the base class.</param>
    /// <returns>The same instance typed as <see cref="ArbitrumVirtualMachine"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ArbitrumVirtualMachine AsArbitrum(VirtualMachine<ArbitrumGasPolicy> vm)
    {
        Debug.Assert(vm is ArbitrumVirtualMachine,
            $"ArbitrumEvmInstructions called with {vm.GetType().Name}. " +
            "These instructions must only be used with ArbitrumVirtualMachine.");
        return Unsafe.As<VirtualMachine<ArbitrumGasPolicy>, ArbitrumVirtualMachine>(ref vm);
    }

    /// <summary>
    /// Returns the gas price for the transaction.
    /// </summary>
    private struct OpGasPrice
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
    /// Returns the L1 block number of the current L2 block.
    /// </summary>
    private struct OpNumber
    {
        public static long GasCost => GasCostOf.Base;

        public static ulong Operation(ArbitrumVirtualMachine vm)
        {
            ulong? cached = vm.L1BlockCache.GetCachedL1BlockNumber();
            if (cached.HasValue)
                return cached.Value;

            ulong blockNumber = vm.FreeArbosState.Blockhashes.GetL1BlockNumber();
            vm.L1BlockCache.SetCachedL1BlockNumber(blockNumber);

            return blockNumber;
        }
    }
}
