// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Evm;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Evm;

namespace Nethermind.Arbitrum.Arbos.Programs;

public static class WasmGas
{

    public static MultiGas WasmAccountTouchCost(IStylusVmHost vm, Address address, bool withCode)
    {
        MultiGas gas = new();

        // Code access cost -> StorageAccess
        if (withCode)
        {
            long maxCodeSize = vm.Spec.MaxCodeSize;
            ulong codeAccessGas = ((ulong)maxCodeSize / 24576) * GasCostOf.ExtCodeEip150;
            gas.Increment(ResourceKind.StorageAccess, codeAccessGas);
        }

        if (vm.VmState.AccessTracker.IsCold(address))
        {
            vm.VmState.AccessTracker.WarmUp(address);
            // Cold: StorageAccess + Computation split
            gas.Increment(ResourceKind.StorageAccess, GasCostOf.ColdAccountAccess - GasCostOf.WarmStateRead);
            gas.Increment(ResourceKind.Computation, GasCostOf.WarmStateRead);
        }
        else
            // Warm: Computation only
            gas.Increment(ResourceKind.Computation, GasCostOf.WarmStateRead);

        return gas;
    }

    /// <summary>
    /// Calculates gas cost for WASM contract calls.
    /// </summary>
    public static (MultiGas Cost, bool OutOfGas) WasmCallCost(IStylusVmHost vm, Address contract, bool hasValue, ulong gasLeft)
    {
        MultiGas cost = new();
        cost.Increment(ResourceKind.Computation, GasCostOf.WarmStateRead);

        // Cold account access
        if (vm.VmState.AccessTracker.IsCold(contract))
        {
            vm.VmState.AccessTracker.WarmUp(contract);
            cost.Increment(ResourceKind.StorageAccess, GasCostOf.ColdAccountAccess - GasCostOf.WarmStateRead);
        }

        // New account creation with value transfer
        if (hasValue)
        {
            if (!vm.WorldState.AccountExists(contract) || vm.WorldState.IsDeadAccount(contract))
                cost.Increment(ResourceKind.StorageGrowth, GasCostOf.NewAccount);

            // Value transfer cost -> Computation
            cost.Increment(ResourceKind.Computation, GasCostOf.CallValue);
        }

        bool outOfGas = cost.SingleGas() > gasLeft;
        return (cost, outOfGas);
    }

    /// <summary>
    /// Calculates gas cost for emitting logs from WASM contracts.
    /// </summary>
    public static MultiGas WasmLogCost(uint numTopics, uint dataBytes)
    {
        ulong gas = (ulong)(ArbitrumGasCostOf.LogTopicHistoryGas * numTopics + GasCostOf.LogData * dataBytes);
        MultiGas result = new();
        result.Increment(ResourceKind.HistoryGrowth, gas);
        return result;
    }

    public static MultiGas WasmStateLoadCost(IStylusVmHost vm, StorageCell storageCell)
    {
        MultiGas gas = new();
        ref readonly StackAccessTracker accessTracker = ref vm.VmState.AccessTracker;
        if (accessTracker.IsCold(in storageCell))
        {
            accessTracker.WarmUp(in storageCell);
            // Cold: StorageAccess + Computation split
            gas.Increment(ResourceKind.StorageAccess, GasCostOf.ColdSLoad - GasCostOf.WarmStateRead);
            gas.Increment(ResourceKind.Computation, GasCostOf.WarmStateRead);
        }
        else
            // Warm: Computation only
            gas.Increment(ResourceKind.Computation, GasCostOf.WarmStateRead);

        return gas;
    }

    public static MultiGas WasmStateStoreCost(IStylusVmHost vm, StorageCell storageCell, ReadOnlySpan<byte> newValue)
    {
        VmState<ArbitrumGasPolicy> vmState = vm.VmState;
        ref readonly StackAccessTracker accessTracker = ref vmState.AccessTracker;

        MultiGas cost = new();

        // Cold access -> StorageAccess
        if (accessTracker.IsCold(in storageCell))
        {
            cost.Increment(ResourceKind.StorageAccess, GasCostOf.ColdSLoad);
            accessTracker.WarmUp(in storageCell);
        }

        ReadOnlySpan<byte> currentValue = vm.WorldState.Get(in storageCell);
        long sClearRefunds = RefundOf.SClear(true);

        // Same value -> just warm read (Computation)
        if (Bytes.AreEqual(currentValue, newValue))
        {
            cost.Increment(ResourceKind.Computation, GasCostOf.WarmStateRead);
            return cost;
        }

        Span<byte> originalValue = vm.WorldState.GetOriginal(in storageCell);
        if (Bytes.AreEqual(originalValue, currentValue))
        {
            if (originalValue.IsZero())
            {
                // New slot creation -> StorageGrowth
                cost.Increment(ResourceKind.StorageGrowth, GasCostOf.SSet);
                return cost;
            }

            if (newValue.IsZero())
                vmState.Refund += sClearRefunds;

            // Reset existing slot -> StorageAccess (SReset - ColdSLoad)
            cost.Increment(ResourceKind.StorageAccess, GasCostOf.SReset - GasCostOf.ColdSLoad);
            return cost;
        }

        // Handle refund logic (unchanged)
        if (!originalValue.IsZero())
        {
            if (currentValue.IsZero())
                vmState.Refund -= sClearRefunds;
            else if (newValue.IsZero())
                vmState.Refund += sClearRefunds;
        }

        if (Bytes.AreEqual(originalValue, newValue))
        {
            if (originalValue.IsZero())
                vmState.Refund += (GasCostOf.SSet - GasCostOf.WarmStateRead);
            else
                vmState.Refund += (GasCostOf.SReset - GasCostOf.ColdSLoad) - GasCostOf.WarmStateRead;
        }

        // Default: warm state read -> Computation
        cost.Increment(ResourceKind.Computation, GasCostOf.WarmStateRead);
        return cost;
    }
}
