// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Evm;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Evm;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Stylus;

/// <summary>
/// Multi-dimensional gas cost calculations for WASM Host I/O operations.
/// </summary>
public static class WasmHostIOCosts
{
    /// <summary>
    /// Computes the multi-dimensional cost of loading state from storage in WASM.
    /// </summary>
    /// <param name="vm">The Stylus VM host providing access to state and specs</param>
    /// <param name="storageCell">The storage cell being accessed</param>
    /// <returns>Multi-dimensional gas cost</returns>
    public static MultiGas WasmStateLoadCost(IStylusVmHost vm, in StorageCell storageCell)
    {
        ref readonly StackAccessTracker accessTracker = ref vm.EvmState.AccessTracker;

        if (accessTracker.IsCold(in storageCell))
        {
            // Cold slot access: storage access + computation
            // ColdSLoad (2100) = StorageAccess (2000) + Computation (100)
            MultiGas cost = MultiGas.Zero;
            cost.SaturatingIncrementInto(ResourceKind.StorageAccess, GasCostOf.ColdSLoad - GasCostOf.WarmStateRead);
            cost.SaturatingIncrementInto(ResourceKind.Computation, GasCostOf.WarmStateRead);

            accessTracker.WarmUp(in storageCell);
            return cost;
        }

        // Warm slot access: computation only
        return MultiGas.ComputationGas(GasCostOf.WarmStateRead);
    }

    /// <summary>
    /// Computes the multi-dimensional cost of storing state to storage in WASM.
    /// The code follows EIP-2200/EIP-2929 SSTORE gas semantics (post-Merge parameters).
    /// </summary>
    /// <param name="vm">The Stylus VM host providing access to state and specs</param>
    /// <param name="storageCell">The storage cell being modified</param>
    /// <param name="newValue">The new value being stored</param>
    /// <returns>Multi-dimensional gas cost</returns>
    public static MultiGas WasmStateStoreCost(IStylusVmHost vm, in StorageCell storageCell, ReadOnlySpan<byte> newValue)
    {
        MultiGas cost = MultiGas.Zero;
        EvmState evmState = vm.EvmState;
        ref readonly StackAccessTracker accessTracker = ref evmState.AccessTracker;

        // Check slot presence in access list
        if (accessTracker.IsCold(in storageCell))
        {
            cost.SaturatingIncrementInto(ResourceKind.StorageAccess, GasCostOf.ColdSLoad);
            accessTracker.WarmUp(in storageCell);
        }

        ReadOnlySpan<byte> currentValue = vm.WorldState.Get(in storageCell);

        // No-op case
        if (Bytes.AreEqual(currentValue, newValue))
        {
            cost.SaturatingIncrementInto(ResourceKind.Computation, GasCostOf.WarmStateRead);
            return cost;
        }

        Span<byte> originalValue = vm.WorldState.GetOriginal(in storageCell);

        if (Bytes.AreEqual(originalValue, currentValue))
        {
            // Create slot (zero → non-zero)
            if (originalValue.IsZero())
            {
                cost.SaturatingIncrementInto(ResourceKind.StorageGrowth, GasCostOf.SSet);
                return cost;
            }

            // Delete slot (non-zero → zero)
            if (newValue.IsZero())
            {
                evmState.Refund += RefundOf.SClear(true);
            }

            // Write existing slot
            cost.SaturatingIncrementInto(ResourceKind.StorageAccess, GasCostOf.SReset - GasCostOf.ColdSLoad);
            return cost;
        }

        // Original was non-zero
        if (!originalValue.IsZero())
        {
            // Recreate slot (was deleted, now non-zero again)
            if (currentValue.IsZero())
            {
                evmState.Refund -= RefundOf.SClear(true);
            }
            // Delete slot (was non-zero, now zero)
            else if (newValue.IsZero())
            {
                evmState.Refund += RefundOf.SClear(true);
            }
        }

        // Reset to original value - apply refund
        if (Bytes.AreEqual(originalValue, newValue))
        {
            if (originalValue.IsZero())
            {
                // Reset to original inexistent slot
                evmState.Refund += GasCostOf.SSet - GasCostOf.WarmStateRead;
            }
            else
            {
                // Reset to original existing slot
                evmState.Refund += (GasCostOf.SReset - GasCostOf.ColdSLoad) - GasCostOf.WarmStateRead;
            }
        }

        // Dirty update
        cost.SaturatingIncrementInto(ResourceKind.Computation, GasCostOf.WarmStateRead);
        return cost;
    }

    /// <summary>
    /// Computes the multi-dimensional cost of starting a call from WASM.
    /// Follows EIP-2929 account access costs and EIP-2930 access list semantics.
    /// </summary>
    /// <param name="vm">The Stylus VM host providing access to state and specs</param>
    /// <param name="contract">The contract address being called</param>
    /// <param name="value">The value being transferred</param>
    /// <param name="budget">The gas budget available</param>
    /// <returns>Multi-dimensional gas cost, or error if out of gas</returns>
    public static (MultiGas Cost, bool OutOfGas) WasmCallCost(IStylusVmHost vm, Address contract, in UInt256 value, ulong budget)
    {
        MultiGas total = MultiGas.Zero;
        ref readonly StackAccessTracker accessTracker = ref vm.EvmState.AccessTracker;

        // EIP-2929: static cost considered as computation
        total.SaturatingIncrementInto(ResourceKind.Computation, GasCostOf.WarmStateRead);
        if (total.SingleGas() > budget)
        {
            return (total, true);
        }

        // EIP-2929: first dynamic cost if cold
        bool warmAccess = !accessTracker.IsCold(contract);
        ulong coldCost = GasCostOf.ColdAccountAccess - GasCostOf.WarmStateRead;

        if (!warmAccess)
        {
            accessTracker.WarmUp(contract);

            // Cold account access: storage access + computation
            total.SaturatingIncrementInto(ResourceKind.StorageAccess, coldCost);
            total.SaturatingIncrementInto(ResourceKind.Computation, GasCostOf.WarmStateRead);
            if (total.SingleGas() > budget)
            {
                return (total, true);
            }
        }

        // gasCall() logic
        bool transfersValue = !value.IsZero;
        if (transfersValue && vm.WorldState.IsDeadAccount(contract))
        {
            // Storage slot writes (zero → nonzero) considered as storage growth
            total.SaturatingIncrementInto(ResourceKind.StorageGrowth, GasCostOf.NewAccount);
            if (total.SingleGas() > budget)
            {
                return (total, true);
            }
        }

        if (transfersValue)
        {
            // Value transfer to non-empty account considered as computation
            total.SaturatingIncrementInto(ResourceKind.Computation, GasCostOf.CallValue);
            if (total.SingleGas() > budget)
            {
                return (total, true);
            }
        }

        return (total, false);
    }

    /// <summary>
    /// Computes the multi-dimensional cost of touching an account in WASM.
    /// Follows EIP-2929 account access cost semantics.
    /// </summary>
    /// <param name="vm">The Stylus VM host providing access to state and specs</param>
    /// <param name="address">The account address being touched</param>
    /// <param name="withCode">Whether code size penalty should be applied</param>
    /// <returns>Multi-dimensional gas cost</returns>
    public static MultiGas WasmAccountTouchCost(IStylusVmHost vm, Address address, bool withCode)
    {
        MultiGas cost = MultiGas.Zero;
        ref readonly StackAccessTracker accessTracker = ref vm.EvmState.AccessTracker;

        if (withCode)
        {
            // Code size penalty: scaled based on max code size
            ulong extCodeCost = (ulong)(vm.Spec.MaxCodeSize / 24576) * GasCostOf.ExtCodeEip150;
            cost.SaturatingIncrementInto(ResourceKind.StorageAccess, extCodeCost);
        }

        if (accessTracker.IsCold(address))
        {
            accessTracker.WarmUp(address);

            // Cold account read: storage access + computation
            cost.SaturatingIncrementInto(ResourceKind.StorageAccess, GasCostOf.ColdAccountAccess - GasCostOf.WarmStateRead);
            cost.SaturatingIncrementInto(ResourceKind.Computation, GasCostOf.WarmStateRead);
        }
        else
        {
            // Warm account read: computation only
            cost.SaturatingIncrementInto(ResourceKind.Computation, GasCostOf.WarmStateRead);
        }

        return cost;
    }

    /// <summary>
    /// Computes the history growth part cost of log operation in WASM.
    /// Full cost is charged on the WASM side at the emit_log function.
    /// </summary>
    /// <param name="numTopics">Number of topics in the log</param>
    /// <param name="dataBytes">Number of data bytes in the log</param>
    /// <returns>Multi-dimensional gas cost</returns>
    public static MultiGas WasmLogCost(ulong numTopics, ulong dataBytes)
    {
        // Bloom/topic history growth: LogTopic gas per topic (375 each)
        ulong bloomHistoryGrowthCost = GasCostOf.LogTopic * numTopics;

        // Payload history growth: LogData gas per payload byte (8 per byte)
        ulong payloadHistoryGrowthCost = GasCostOf.LogData * dataBytes;

        return MultiGas.HistoryGrowthGas(bloomHistoryGrowthCost + payloadHistoryGrowthCost);
    }
}
