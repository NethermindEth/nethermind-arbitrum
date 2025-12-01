// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Runtime.CompilerServices;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.Gas;
using Nethermind.Evm.State;
using Nethermind.Int256;
using static Nethermind.Arbitrum.Evm.ArbitrumVirtualMachine;

namespace Nethermind.Arbitrum.Evm;

/// <summary>
/// Custom environment instruction handlers for Arbitrum with multi-dimensional gas tracking.
/// </summary>
internal static partial class ArbitrumEvmInstructions
{

    /// <summary>
    /// Executes an environment introspection opcode that returns a UInt256 value.
    /// </summary>
    /// <param name="vm">The virtual machine instance.</param>
    /// <param name="stack">The execution stack.</param>
    /// <param name="gasState">The gas state which tracks remaining gas and policy data.</param>
    /// <param name="programCounter">The program counter.</param>
    /// <returns>An EVM exception type if an error occurs.</returns>
    [SkipLocalsInit]
    public static EvmExceptionType InstructionBlkUInt256<TTracingInst>(VirtualMachine<MultiGasPolicy> vm, ref EvmStack stack, ref GasState gasState, ref int programCounter)
        where TTracingInst : struct, IFlag
    {
        MultiGasPolicy.ConsumeGas(ref gasState, OpGasPrice.GasCost, Instruction.GASPRICE);
        if (MultiGasPolicy.GetRemainingGas(in gasState) < 0)
            return EvmExceptionType.OutOfGas;

        ref readonly UInt256 result = ref OpGasPrice.Operation((ArbitrumVirtualMachine)vm);

        stack.PushUInt256<TTracingInst>(in result);

        return EvmExceptionType.None;
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
    /// Executes an environment introspection opcode that returns a UInt64 value.
    /// </summary>
    /// <typeparam name="TTracingInst">The tracing instruction flag.</typeparam>
    /// <param name="vm">The virtual machine instance.</param>
    /// <param name="stack">The execution stack.</param>
    /// <param name="gasState">The gas state which tracks remaining gas and policy data.</param>
    /// <param name="programCounter">The program counter.</param>
    /// <returns>An EVM exception type if an error occurs.</returns>
    [SkipLocalsInit]
    public static EvmExceptionType InstructionBlkUInt64<TTracingInst>(VirtualMachine<MultiGasPolicy> vm, ref EvmStack stack, ref GasState gasState, ref int programCounter)
        where TTracingInst : struct, IFlag
    {
        MultiGasPolicy.ConsumeGas(ref gasState, OpNumber.GasCost, Instruction.NUMBER);

        ulong result = OpNumber.Operation((ArbitrumVirtualMachine)vm);

        stack.PushUInt64<TTracingInst>(result);

        return EvmExceptionType.None;
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

    [SkipLocalsInit]
    public static EvmExceptionType InstructionBlockHash<TTracingInst>(VirtualMachine<MultiGasPolicy> vm, ref EvmStack stack, ref GasState gasState, ref int programCounter)
        where TTracingInst : struct, IFlag
    {
        MultiGasPolicy.ConsumeGas(ref gasState, GasCostOf.BlockHash, Instruction.BLOCKHASH);

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
    /// BALANCE instruction with multi-dimensional gas tracking.
    /// Cold account access → StorageAccess.
    /// </summary>
    [SkipLocalsInit]
    internal static EvmExceptionType InstructionBalance<TTracingInst>(
        VirtualMachine<MultiGasPolicy> vm, ref EvmStack stack, ref GasState gasState, ref int programCounter)
        where TTracingInst : struct, IFlag
    {
        IReleaseSpec spec = vm.Spec;
        EvmState vmState = vm.EvmState;

        // Deduct base gas cost (WarmStateRead)
        MultiGasPolicy.ConsumeGas(ref gasState, spec.GetBalanceCost(), Instruction.BALANCE);
        if (MultiGasPolicy.GetRemainingGas(in gasState) < 0)
            return EvmExceptionType.OutOfGas;

        Address? address = stack.PopAddress();
        if (address is null)
            return EvmExceptionType.StackUnderflow;

        // Check cold/warm BEFORE warming up
        bool isCold = spec.UseHotAndColdStorage &&
                      !spec.IsPrecompile(address) &&
                      vmState.AccessTracker.IsCold(address);

        // Calculate gas cost for account access
        long gasCost = 0;
        if (spec.UseHotAndColdStorage && !spec.IsPrecompile(address))
        {
            if (vmState.AccessTracker.WarmUp(address))
                gasCost = GasCostOf.ColdAccountAccess;
            else
                gasCost = GasCostOf.WarmStateRead;
        }

        // Compute MultiGas for cold access (delta from warm)
        MultiGas multiGas = ArbitrumGas.GasAccountAccess(isCold);

        // Deduct gas and add multigas
        MultiGasPolicy.ConsumeGasWithMultiGas(ref gasState, gasCost, in multiGas);
        if (MultiGasPolicy.GetRemainingGas(in gasState) < 0)
            return EvmExceptionType.OutOfGas;

        ref readonly UInt256 result = ref vm.WorldState.GetBalance(address);
        stack.PushUInt256<TTracingInst>(in result);

        return EvmExceptionType.None;
    }

    /// <summary>
    /// EXTCODESIZE instruction with multi-dimensional gas tracking.
    /// Cold account access → StorageAccess.
    /// </summary>
    [SkipLocalsInit]
    internal static EvmExceptionType InstructionExtCodeSize<TTracingInst>(
        VirtualMachine<MultiGasPolicy> vm, ref EvmStack stack, ref GasState gasState, ref int programCounter)
        where TTracingInst : struct, IFlag
    {
        IReleaseSpec spec = vm.Spec;
        EvmState vmState = vm.EvmState;

        // Deduct base gas cost
        MultiGasPolicy.ConsumeGas(ref gasState, spec.GetExtCodeCost(), Instruction.EXTCODESIZE);
        if (MultiGasPolicy.GetRemainingGas(in gasState) < 0)
            return EvmExceptionType.OutOfGas;

        Address? address = stack.PopAddress();
        if (address is null)
            return EvmExceptionType.StackUnderflow;

        // Check cold/warm BEFORE warming up
        bool isCold = spec.UseHotAndColdStorage &&
                      !spec.IsPrecompile(address) &&
                      vmState.AccessTracker.IsCold(address);

        // Calculate gas cost for account access
        long gasCost = 0;
        if (spec.UseHotAndColdStorage && !spec.IsPrecompile(address))
        {
            if (vmState.AccessTracker.WarmUp(address))
                gasCost = GasCostOf.ColdAccountAccess;
            else
                gasCost = GasCostOf.WarmStateRead;
        }

        // Compute MultiGas for cold access
        MultiGas multiGas = ArbitrumGas.GasAccountAccess(isCold);

        // Deduct gas and add multigas
        MultiGasPolicy.ConsumeGasWithMultiGas(ref gasState, gasCost, in multiGas);
        if (MultiGasPolicy.GetRemainingGas(in gasState) < 0)
            return EvmExceptionType.OutOfGas;

        // Load the account's code
        ReadOnlySpan<byte> accountCode = vm.CodeInfoRepository
            .GetCachedCodeInfo(address, followDelegation: false, spec, out _)
            .CodeSpan;

        stack.PushUInt32<TTracingInst>((uint)accountCode.Length);

        return EvmExceptionType.None;
    }

    /// <summary>
    /// EXTCODEHASH instruction with multi-dimensional gas tracking.
    /// Cold account access → StorageAccess.
    /// </summary>
    [SkipLocalsInit]
    internal static EvmExceptionType InstructionExtCodeHash<TTracingInst>(
        VirtualMachine<MultiGasPolicy> vm, ref EvmStack stack, ref GasState gasState, ref int programCounter)
        where TTracingInst : struct, IFlag
    {
        IReleaseSpec spec = vm.Spec;
        EvmState vmState = vm.EvmState;

        // Deduct base gas cost
        MultiGasPolicy.ConsumeGas(ref gasState, spec.GetExtCodeHashCost(), Instruction.EXTCODEHASH);
        if (MultiGasPolicy.GetRemainingGas(in gasState) < 0)
            return EvmExceptionType.OutOfGas;

        Address? address = stack.PopAddress();
        if (address is null)
            return EvmExceptionType.StackUnderflow;

        // Check cold/warm BEFORE warming up
        bool isCold = spec.UseHotAndColdStorage &&
                      !spec.IsPrecompile(address) &&
                      vmState.AccessTracker.IsCold(address);

        // Calculate gas cost for account access
        long gasCost = 0;
        if (spec.UseHotAndColdStorage && !spec.IsPrecompile(address))
        {
            if (vmState.AccessTracker.WarmUp(address))
                gasCost = GasCostOf.ColdAccountAccess;
            else
                gasCost = GasCostOf.WarmStateRead;
        }

        // Compute MultiGas for cold access
        MultiGas multiGas = ArbitrumGas.GasAccountAccess(isCold);

        // Deduct gas and add multigas
        MultiGasPolicy.ConsumeGasWithMultiGas(ref gasState, gasCost, in multiGas);
        if (MultiGasPolicy.GetRemainingGas(in gasState) < 0)
            return EvmExceptionType.OutOfGas;

        IWorldState state = vm.WorldState;
        if (state.IsDeadAccount(address))
        {
            stack.PushZero<TTracingInst>();
        }
        else
        {
            ref readonly ValueHash256 hash = ref state.GetCodeHash(address);
            stack.Push32Bytes<TTracingInst>(in hash);
        }

        return EvmExceptionType.None;
    }

    /// <summary>
    /// EXTCODECOPY instruction with multi-dimensional gas tracking.
    /// Cold account access → StorageAccess, memory expansion → Computation.
    /// </summary>
    [SkipLocalsInit]
    internal static EvmExceptionType InstructionExtCodeCopy<TTracingInst>(
        VirtualMachine<MultiGasPolicy> vm, ref EvmStack stack, ref GasState gasState, ref int programCounter)
        where TTracingInst : struct, IFlag
    {
        IReleaseSpec spec = vm.Spec;
        EvmState vmState = vm.EvmState;

        // Pop address, destination offset, source offset, and length
        Address? address = stack.PopAddress();
        if (address is null ||
            !stack.PopUInt256(out UInt256 destOffset) ||
            !stack.PopUInt256(out UInt256 srcOffset) ||
            !stack.PopUInt256(out UInt256 length))
            return EvmExceptionType.StackUnderflow;

        // Calculate base gas cost + memory cost
        long gasCost = spec.GetExtCodeCost() +
                       GasCostOf.Memory * EvmCalculations.Div32Ceiling(in length, out bool outOfGas);
        if (outOfGas)
            return EvmExceptionType.OutOfGas;

        // Deduct base gas cost
        MultiGasPolicy.ConsumeGas(ref gasState, gasCost, Instruction.EXTCODECOPY);
        if (MultiGasPolicy.GetRemainingGas(in gasState) < 0)
            return EvmExceptionType.OutOfGas;

        // Check cold/warm BEFORE warming up
        bool isCold = spec.UseHotAndColdStorage &&
                      !spec.IsPrecompile(address) &&
                      vmState.AccessTracker.IsCold(address);

        // Calculate gas cost for account access
        long accountAccessGas = 0;
        if (spec.UseHotAndColdStorage && !spec.IsPrecompile(address))
        {
            if (vmState.AccessTracker.WarmUp(address))
                accountAccessGas = GasCostOf.ColdAccountAccess;
            else
                accountAccessGas = GasCostOf.WarmStateRead;
        }

        // Compute MultiGas for cold access
        MultiGas multiGas = ArbitrumGas.GasAccountAccess(isCold);

        // Deduct account access gas and add multigas
        MultiGasPolicy.ConsumeGasWithMultiGas(ref gasState, accountAccessGas, in multiGas);
        if (MultiGasPolicy.GetRemainingGas(in gasState) < 0)
            return EvmExceptionType.OutOfGas;

        if (!length.IsZero)
        {
            // Update memory cost
            if (!EvmCalculations.UpdateMemoryCost<MultiGasPolicy>(vmState, ref gasState, in destOffset, length, Instruction.EXTCODECOPY))
                return EvmExceptionType.OutOfGas;

            // Get the external code
            ICodeInfo codeInfo = vm.CodeInfoRepository
                .GetCachedCodeInfo(address, followDelegation: false, spec, out _);
            ReadOnlySpan<byte> externalCode = codeInfo.CodeSpan;

            // Slice and copy to memory
            ZeroPaddedSpan slice = externalCode.SliceWithZeroPadding(in srcOffset, (int)length);
            vmState.Memory.Save(in destOffset, in slice);

            // Report memory changes if tracing
            if (TTracingInst.IsActive)
                vm.TxTracer.ReportMemoryChange(destOffset, in slice);
        }

        return EvmExceptionType.None;
    }
}
