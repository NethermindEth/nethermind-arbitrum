// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Runtime.CompilerServices;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.Gas;
using Nethermind.Int256;
using static Nethermind.Arbitrum.Evm.ArbitrumVirtualMachine;

namespace Nethermind.Arbitrum.Evm;

/// <summary>
/// Custom storage instruction handlers for Arbitrum with multi-dimensional gas tracking.
/// These handlers compute MultiGas with full state access, matching Nitro's dynamicGas pattern.
/// </summary>
internal static partial class ArbitrumEvmInstructions
{
    /// <summary>
    /// SLOAD instruction with multi-dimensional gas tracking.
    /// Matches base implementation but computes MultiGas using ArbitrumGas.GasSLoadEIP2929.
    /// </summary>
    [SkipLocalsInit]
    internal static EvmExceptionType InstructionSLoad<TTracingInst>(
        VirtualMachine<MultiGasPolicy> vm, ref EvmStack stack, ref GasState gasState, ref int programCounter)
        where TTracingInst : struct, IFlag
    {
        IReleaseSpec spec = vm.Spec;
        EvmState vmState = vm.EvmState;

        // Increment the SLOAD opcode metric
        Metrics.IncrementSLoadOpcode();

        // Pop the key from the stack
        if (!stack.PopUInt256(out UInt256 result))
            return EvmExceptionType.StackUnderflow;

        // Construct the storage cell for the executing account
        Address executingAccount = vmState.Env.ExecutingAccount;
        StorageCell storageCell = new(executingAccount, in result);

        // Check cold/warm BEFORE warming up
        bool isCold = spec.UseHotAndColdStorage && vmState.AccessTracker.IsCold(in storageCell);

        // Calculate gas cost (same logic as base)
        long gasCost = spec.GetSLoadCost();
        if (isCold)
        {
            gasCost += GasCostOf.ColdSLoad;
            vmState.AccessTracker.WarmUp(in storageCell);
        }

        // Compute MultiGas with proper categorization
        MultiGas multiGas = ArbitrumGas.GasSLoadEIP2929(isCold);

        // Deduct gas and add multigas
        MultiGasPolicy.ConsumeGasWithMultiGas(ref gasState, gasCost, in multiGas);
        if (MultiGasPolicy.GetRemainingGas(in gasState) < 0)
            return EvmExceptionType.OutOfGas;

        // Retrieve the persistent storage value and push it onto the stack
        ReadOnlySpan<byte> value = vm.WorldState.Get(in storageCell);
        stack.PushBytes<TTracingInst>(value);

        // Log the storage load operation if tracing is enabled
        if (vm.TxTracer.IsTracingStorage)
            vm.TxTracer.LoadOperationStorage(executingAccount, result, value);

        return EvmExceptionType.None;
    }

    /// <summary>
    /// SSTORE instruction with multi-dimensional gas tracking (net metered).
    /// Matches base implementation but computes MultiGas using ArbitrumGas.GasSStoreEIP2929.
    /// </summary>
    [SkipLocalsInit]
    internal static EvmExceptionType InstructionSStoreMetered<TTracingInst, TUseNetGasStipendFix>(
        VirtualMachine<MultiGasPolicy> vm, ref EvmStack stack, ref GasState gasState, ref int programCounter)
        where TTracingInst : struct, IFlag
        where TUseNetGasStipendFix : struct, IFlag
    {
        // Increment the SSTORE opcode metric
        Metrics.IncrementSStoreOpcode();

        EvmState vmState = vm.EvmState;
        IReleaseSpec spec = vm.Spec;

        // Disallow storage modifications in static calls
        if (vmState.IsStatic)
            return EvmExceptionType.StaticCallViolation;

        // In net metering with stipend fix, ensure extra gas pressure is reported
        if (TUseNetGasStipendFix.IsActive)
        {
            if (vm.TxTracer.IsTracingRefunds)
                vm.TxTracer.ReportExtraGasPressure(GasCostOf.CallStipend - spec.GetNetMeteredSStoreCost() + 1);
            long availableGas = MultiGasPolicy.GetRemainingGas(in gasState);
            if (availableGas <= GasCostOf.CallStipend)
                return EvmExceptionType.OutOfGas;
        }

        // Pop the key and value from stack
        if (!stack.PopUInt256(out UInt256 key))
            return EvmExceptionType.StackUnderflow;
        ReadOnlySpan<byte> newValue = stack.PopWord256();

        // Determine if the new value is zero and normalize
        bool newIsZero = newValue.IsZero();
        newValue = !newIsZero ? newValue.WithoutLeadingZeros() : BytesZero;

        // Construct the storage cell
        StorageCell storageCell = new(vmState.Env.ExecutingAccount, in key);

        // Check cold/warm BEFORE warming up
        bool isCold = spec.UseHotAndColdStorage && vmState.AccessTracker.IsCold(in storageCell);
        if (isCold)
            vmState.AccessTracker.WarmUp(in storageCell);

        // Get current and original values (full state access!)
        ReadOnlySpan<byte> currentValue = vm.WorldState.Get(in storageCell);
        Span<byte> originalValue = vm.WorldState.GetOriginal(in storageCell);

        bool currentIsZero = currentValue.IsZero();
        bool originalIsZero = originalValue.IsZero();
        bool newSameAsCurrent = (newIsZero && currentIsZero) || Bytes.AreEqual(currentValue, newValue);

        // Calculate gas cost (same logic as base InstructionSStoreMetered)
        long gasCost = 0;

        // Cold access cost
        if (isCold)
            gasCost += GasCostOf.ColdSLoad;

        // Retrieve the refund value
        long sClearRefunds = RefundOf.SClear(spec.IsEip3529Enabled);

        if (newSameAsCurrent)
        {
            // Noop case
            gasCost += spec.GetNetMeteredSStoreCost();
        }
        else
        {
            bool currentSameAsOriginal = Bytes.AreEqual(originalValue, currentValue);

            if (currentSameAsOriginal)
            {
                if (currentIsZero)
                {
                    // Create slot (0 → nonzero)
                    gasCost += GasCostOf.SSet;
                }
                else
                {
                    // Write existing slot
                    gasCost += spec.GetSStoreResetCost();

                    if (newIsZero)
                    {
                        vmState.Refund += sClearRefunds;
                        if (vm.TxTracer.IsTracingRefunds)
                            vm.TxTracer.ReportRefund(sClearRefunds);
                    }
                }
            }
            else
            {
                // Dirty update
                gasCost += spec.GetNetMeteredSStoreCost();

                if (!originalIsZero)
                {
                    if (currentIsZero)
                    {
                        vmState.Refund -= sClearRefunds;
                        if (vm.TxTracer.IsTracingRefunds)
                            vm.TxTracer.ReportRefund(-sClearRefunds);
                    }

                    if (newIsZero)
                    {
                        vmState.Refund += sClearRefunds;
                        if (vm.TxTracer.IsTracingRefunds)
                            vm.TxTracer.ReportRefund(sClearRefunds);
                    }
                }

                // Reversal refund
                bool newSameAsOriginal = Bytes.AreEqual(originalValue, newValue);
                if (newSameAsOriginal)
                {
                    long refundFromReversal = originalIsZero
                        ? spec.GetSetReversalRefund()
                        : spec.GetClearReversalRefund();

                    vmState.Refund += refundFromReversal;
                    if (vm.TxTracer.IsTracingRefunds)
                        vm.TxTracer.ReportRefund(refundFromReversal);
                }
            }
        }

        // Compute MultiGas with proper categorization
        MultiGas multiGas = ArbitrumGas.GasSStoreEIP2929(isCold, currentValue, originalValue, newValue);

        // Deduct gas and add multigas
        MultiGasPolicy.ConsumeGasWithMultiGas(ref gasState, gasCost, in multiGas);
        if (MultiGasPolicy.GetRemainingGas(in gasState) < 0)
            return EvmExceptionType.OutOfGas;

        // Only update storage if the new value differs from the current value
        if (!newSameAsCurrent)
            vm.WorldState.Set(in storageCell, newIsZero ? BytesZero : newValue.ToArray());

        // Report storage changes for tracing if enabled
        if (TTracingInst.IsActive)
            TraceSstore(vm, newIsZero, in storageCell, newValue);

        if (vm.TxTracer.IsTracingStorage)
            vm.TxTracer.SetOperationStorage(storageCell.Address, key, newValue, currentValue);

        return EvmExceptionType.None;
    }

    /// <summary>
    /// SSTORE instruction with multi-dimensional gas tracking (legacy/unmetered).
    /// Matches base implementation but computes MultiGas using ArbitrumGas.GasSStoreEIP2929.
    /// </summary>
    [SkipLocalsInit]
    internal static EvmExceptionType InstructionSStoreUnmetered<TTracingInst>(
        VirtualMachine<MultiGasPolicy> vm, ref EvmStack stack, ref GasState gasState, ref int programCounter)
        where TTracingInst : struct, IFlag
    {
        // Increment the SSTORE opcode metric
        Metrics.IncrementSStoreOpcode();

        EvmState vmState = vm.EvmState;
        IReleaseSpec spec = vm.Spec;

        // Disallow storage modifications in static calls
        if (vmState.IsStatic)
            return EvmExceptionType.StaticCallViolation;

        // For legacy metering: charge SStoreResetCost upfront
        long initialGasCost = spec.GetSStoreResetCost();
        gasState.RemainingGas -= initialGasCost;
        if (gasState.RemainingGas < 0)
            return EvmExceptionType.OutOfGas;

        // Pop the key and value from stack
        if (!stack.PopUInt256(out UInt256 key))
            return EvmExceptionType.StackUnderflow;
        ReadOnlySpan<byte> newValue = stack.PopWord256();

        // Determine if the new value is zero and normalize
        bool newIsZero = newValue.IsZero();
        newValue = !newIsZero ? newValue.WithoutLeadingZeros() : BytesZero;

        // Construct the storage cell
        StorageCell storageCell = new(vmState.Env.ExecutingAccount, in key);

        // Check cold/warm BEFORE warming up
        bool isCold = spec.UseHotAndColdStorage && vmState.AccessTracker.IsCold(in storageCell);
        if (isCold)
        {
            gasState.RemainingGas -= GasCostOf.ColdSLoad;
            if (gasState.RemainingGas < 0)
                return EvmExceptionType.OutOfGas;
            vmState.AccessTracker.WarmUp(in storageCell);
        }

        // Get current value
        ReadOnlySpan<byte> currentValue = vm.WorldState.Get(in storageCell);
        bool currentIsZero = currentValue.IsZero();
        bool newSameAsCurrent = (newIsZero && currentIsZero) || Bytes.AreEqual(currentValue, newValue);

        // Get original value for MultiGas calculation
        Span<byte> originalValue = vm.WorldState.GetOriginal(in storageCell);

        // Retrieve the refund value
        long sClearRefunds = RefundOf.SClear(spec.IsEip3529Enabled);

        // Legacy metering gas adjustments
        long additionalGasCost = 0;
        if (newIsZero)
        {
            if (!newSameAsCurrent)
            {
                vmState.Refund += sClearRefunds;
                if (vm.TxTracer.IsTracingRefunds)
                    vm.TxTracer.ReportRefund(sClearRefunds);
            }
        }
        else if (currentIsZero)
        {
            additionalGasCost = GasCostOf.SSet - GasCostOf.SReset;
        }

        if (additionalGasCost > 0)
        {
            gasState.RemainingGas -= additionalGasCost;
            if (gasState.RemainingGas < 0)
                return EvmExceptionType.OutOfGas;
        }

        // Compute MultiGas with proper categorization
        MultiGas multiGas = ArbitrumGas.GasSStoreEIP2929(isCold, currentValue, originalValue, newValue);

        // Add multigas (gas already deducted above)
        MultiGasPolicy.AddMultiGas(ref gasState, in multiGas);

        // Only update storage if the new value differs from the current value
        if (!newSameAsCurrent)
            vm.WorldState.Set(in storageCell, newIsZero ? BytesZero : newValue.ToArray());

        // Report storage changes for tracing if enabled
        if (TTracingInst.IsActive)
            TraceSstore(vm, newIsZero, in storageCell, newValue);

        if (vm.TxTracer.IsTracingStorage)
            vm.TxTracer.SetOperationStorage(storageCell.Address, key, newValue, currentValue);

        return EvmExceptionType.None;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TraceSstore(VirtualMachine<MultiGasPolicy> vm, bool newIsZero, in StorageCell storageCell, ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> valueToStore = newIsZero ? BytesZero.AsSpan() : bytes;
        byte[] storageBytes = new byte[32];
        storageCell.Index.ToBigEndian(storageBytes);
        vm.TxTracer.ReportStorageChange(storageBytes, valueToStore);
    }
}
