// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Evm;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Evm;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Arbos.Programs;

public static class WasmGas
{
    public static ulong WasmAccountTouchCost(IStylusVmHost vm, Address address, bool withCode)
    {
        ulong gasCost = 0;
        long maxCodeSize = vm.Spec.MaxCodeSize;
        if (withCode)
            gasCost = ((ulong)maxCodeSize / 24576) * GasCostOf.ExtCodeEip150;

        if (vm.VmState.AccessTracker.IsCold(address))
        {
            gasCost += GasCostOf.ColdAccountAccess;
            vm.VmState.AccessTracker.WarmUp(address);
        }
        else
        {
            gasCost += GasCostOf.WarmStateRead;
        }

        return gasCost;
    }

    public static ulong WasmStateLoadCost(IStylusVmHost vm, StorageCell storageCell)
    {
        ulong gasCost = 0;
        ref readonly StackAccessTracker accessTracker = ref vm.VmState.AccessTracker;
        if (accessTracker.IsCold(in storageCell))
        {
            gasCost += GasCostOf.ColdSLoad;
            accessTracker.WarmUp(in storageCell);
        }
        else
        {
            gasCost += GasCostOf.WarmStateRead;
        }

        return gasCost;
    }

    public static ulong WasmStateStoreCost(IStylusVmHost vm, StorageCell storageCell, ReadOnlySpan<byte> newValue)
    {
        VmState<ArbitrumGas> vmState = vm.VmState;
        ref readonly StackAccessTracker accessTracker = ref vmState.AccessTracker;

        ulong gasCost = 0;
        if (accessTracker.IsCold(in storageCell))
        {
            gasCost += GasCostOf.ColdSLoad;
            accessTracker.WarmUp(in storageCell);
        }

        ReadOnlySpan<byte> currentValue = vm.WorldState.Get(in storageCell);

        long sClearRefunds = RefundOf.SClear(true);

        bool newSameAsCurrent = Bytes.AreEqual(currentValue, newValue);
        if (newSameAsCurrent)
        {
            return gasCost + GasCostOf.WarmStateRead;
        }

        Span<byte> originalValue = vm.WorldState.GetOriginal(in storageCell);
        if (Bytes.AreEqual(originalValue, currentValue))
        {
            if (originalValue.IsZero())
                return gasCost + GasCostOf.SSet;

            if (newValue.IsZero())
                vmState.Refund += sClearRefunds;

            return gasCost + GasCostOf.SReset - GasCostOf.ColdSLoad;
        }

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
            {
                vmState.Refund += (GasCostOf.SSet - GasCostOf.WarmStateRead);
            }
            else
            {
                vmState.Refund += (GasCostOf.SReset - GasCostOf.ColdSLoad) - GasCostOf.WarmStateRead;
            }
        }

        return gasCost + GasCostOf.WarmStateRead;
    }
}
