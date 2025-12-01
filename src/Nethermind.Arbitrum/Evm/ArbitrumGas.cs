// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Runtime.CompilerServices;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.State;

namespace Nethermind.Arbitrum.Evm;

/// <summary>
/// Gas functions for Arbitrum EVM operations.
/// Mirrors Nitro's gasFunc pattern - returns MultiGas directly with state access.
/// Similar to WasmHostIOCosts but for EVM instructions.
/// </summary>
public static class ArbitrumGas
{
    /// <summary>
    /// SSTORE gas calculation matching Nitro's makeGasSStoreFunc (operations_acl.go:29-108).
    /// Returns MultiGas with proper categorization based on current/original values.
    /// Does NOT deduct gas or update storage - caller handles that.
    /// </summary>
    /// <param name="accessTracker">Access tracker for cold/warm slot detection</param>
    /// <param name="isCold">Whether the slot is cold (checked before warm-up)</param>
    /// <param name="currentValue">Current value in storage</param>
    /// <param name="originalValue">Original value at transaction start</param>
    /// <param name="newValue">New value being stored</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MultiGas GasSStoreEIP2929(
        bool isCold,
        ReadOnlySpan<byte> currentValue,
        ReadOnlySpan<byte> originalValue,
        ReadOnlySpan<byte> newValue)
    {
        MultiGas multiGas = MultiGas.Zero;

        // Cold slot → StorageAccess
        if (isCold)
            multiGas.SaturatingIncrementInto(ResourceKind.StorageAccess, GasCostOf.ColdSLoad);

        bool newIsZero = newValue.IsZero();
        bool currentIsZero = currentValue.IsZero();
        bool originalIsZero = originalValue.IsZero();

        // No-op case: current == new
        bool newSameAsCurrent = (newIsZero && currentIsZero) || Bytes.AreEqual(currentValue, newValue);
        if (newSameAsCurrent)
        {
            // Noop case - Nitro uses StorageAccess, not Computation
            // See: operations_acl.go:54 - multiGas.SafeIncrement(multigas.ResourceKindStorageAccess, params.WarmStorageReadCostEIP2929)
            multiGas.SaturatingIncrementInto(ResourceKind.StorageAccess, GasCostOf.WarmStateRead);
            return multiGas;
        }

        // Current same as original?
        bool currentSameAsOriginal = Bytes.AreEqual(originalValue, currentValue);
        if (currentSameAsOriginal)
        {
            if (currentIsZero)
            {
                // Create slot (0 → nonzero)
                multiGas.SaturatingIncrementInto(ResourceKind.StorageGrowth, GasCostOf.SSet);
            }
            else
            {
                // Write existing slot (nonzero → different nonzero or nonzero → zero)
                // SReset - ColdSLoad = 5000 - 2100 = 2900
                multiGas.SaturatingIncrementInto(ResourceKind.StorageAccess, GasCostOf.SReset - GasCostOf.ColdSLoad);
            }
            return multiGas;
        }

        // Dirty update (current != original) - Nitro uses StorageAccess, not Computation
        // See: operations_acl.go:88 - multiGas.SafeIncrement(multigas.ResourceKindStorageAccess, params.WarmStorageReadCostEIP2929)
        multiGas.SaturatingIncrementInto(ResourceKind.StorageAccess, GasCostOf.WarmStateRead);
        return multiGas;
    }

    /// <summary>
    /// SLOAD gas calculation matching Nitro's gasSLoadEIP2929 (operations_acl.go:116-132).
    /// Nitro uses UnknownGas here - see TODO(NIT-3484) in operations_acl.go.
    /// </summary>
    /// <param name="isCold">Whether the slot is cold</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MultiGas GasSLoadEIP2929(bool isCold)
    {
        // Match Nitro exactly - uses UnknownGas for SLOAD (TODO NIT-3484)
        // See: operations_acl.go:108 - return multigas.UnknownGas(params.ColdSloadCostEIP2929), nil
        // See: operations_acl.go:112 - return multigas.UnknownGas(params.WarmStorageReadCostEIP2929), nil
        return isCold
            ? MultiGas.UnknownGas(GasCostOf.ColdSLoad)
            : MultiGas.UnknownGas(GasCostOf.WarmStateRead);
    }

    /// <summary>
    /// CALL gas calculation matching Nitro's gasCall (gas_table.go:420-464).
    /// Returns MultiGas with proper categorization.
    /// </summary>
    /// <param name="isCold">Whether the target address is cold</param>
    /// <param name="transfersValue">Whether value is being transferred</param>
    /// <param name="isDeadAccount">Whether target is a dead account (for new account cost)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MultiGas GasCall(bool isCold, bool transfersValue, bool isDeadAccount)
    {
        MultiGas multiGas = MultiGas.Zero;

        // Base warm → Computation
        multiGas.SaturatingIncrementInto(ResourceKind.Computation, GasCostOf.WarmStateRead);

        // Cold account → StorageAccess
        if (isCold)
            multiGas.SaturatingIncrementInto(ResourceKind.StorageAccess, GasCostOf.ColdAccountAccess - GasCostOf.WarmStateRead);

        if (transfersValue)
        {
            // Value transfer → Computation
            multiGas.SaturatingIncrementInto(ResourceKind.Computation, GasCostOf.CallValue);

            // New account → StorageGrowth
            if (isDeadAccount)
                multiGas.SaturatingIncrementInto(ResourceKind.StorageGrowth, GasCostOf.NewAccount);
        }

        return multiGas;
    }

    /// <summary>
    /// LOG gas calculation matching Nitro's makeLogGasFunc.
    /// Returns MultiGas with proper categorization.
    /// </summary>
    /// <param name="topicCount">Number of topics (0-4)</param>
    /// <param name="dataLength">Length of log data in bytes</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MultiGas GasLog(int topicCount, int dataLength)
    {
        MultiGas multiGas = MultiGas.Zero;

        // Base LOG → Computation
        multiGas.SaturatingIncrementInto(ResourceKind.Computation, GasCostOf.Log);

        // Topics: split between HistoryGrowth (256) and Computation (119)
        const long LogTopicHistory = GasCostOf.LogData * 32;  // 8 * 32 = 256
        const long LogTopicComputation = GasCostOf.LogTopic - LogTopicHistory;  // 375 - 256 = 119

        if (topicCount > 0)
        {
            multiGas.SaturatingIncrementInto(ResourceKind.HistoryGrowth, (ulong)(topicCount * LogTopicHistory));
            multiGas.SaturatingIncrementInto(ResourceKind.Computation, (ulong)(topicCount * LogTopicComputation));
        }

        // Data → HistoryGrowth
        if (dataLength > 0)
            multiGas.SaturatingIncrementInto(ResourceKind.HistoryGrowth, (ulong)(dataLength * GasCostOf.LogData));

        return multiGas;
    }

    /// <summary>
    /// CREATE/CREATE2 gas calculation.
    /// Returns MultiGas with proper categorization.
    /// </summary>
    /// <param name="isCreate2">Whether this is CREATE2 (vs CREATE)</param>
    /// <param name="initCodeLength">Length of init code</param>
    /// <param name="spec">Release spec for EIP-3860 check</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MultiGas GasCreate(bool isCreate2, int initCodeLength, bool isEip3860Enabled)
    {
        MultiGas multiGas = MultiGas.Zero;

        // Account creation → StorageGrowth
        multiGas.SaturatingIncrementInto(ResourceKind.StorageGrowth, GasCostOf.Create);

        // EIP-3860: init code word cost → Computation
        if (isEip3860Enabled)
        {
            long wordCount = (initCodeLength + 31) / 32;
            multiGas.SaturatingIncrementInto(ResourceKind.Computation, (ulong)(wordCount * GasCostOf.InitCodeWord));
        }

        // CREATE2 hash cost → Computation
        if (isCreate2)
        {
            long wordCount = (initCodeLength + 31) / 32;
            multiGas.SaturatingIncrementInto(ResourceKind.Computation, (ulong)(wordCount * GasCostOf.Sha3Word));
        }

        return multiGas;
    }

    /// <summary>
    /// Memory expansion gas → Computation.
    /// </summary>
    /// <param name="memoryCost">Memory expansion cost</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MultiGas GasMemory(long memoryCost)
    {
        return MultiGas.ComputationGas((ulong)memoryCost);
    }

    /// <summary>
    /// SELFDESTRUCT gas calculation matching Nitro's addConstantMultiGas special case.
    /// Split between Computation (100) and StorageAccess (remaining).
    /// </summary>
    /// <param name="gasCost">Total gas cost</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MultiGas GasSelfDestruct(long gasCost)
    {
        if (gasCost == GasCostOf.SelfDestructEip150)
        {
            MultiGas multiGas = MultiGas.Zero;
            multiGas.SaturatingIncrementInto(ResourceKind.Computation, GasCostOf.WarmStateRead);
            multiGas.SaturatingIncrementInto(ResourceKind.StorageAccess, (ulong)(gasCost - GasCostOf.WarmStateRead));
            return multiGas;
        }

        // Fallback for other costs (shouldn't happen post-EIP150)
        return MultiGas.StorageAccessGas((ulong)gasCost);
    }

    /// <summary>
    /// SELFDESTRUCT dynamic gas matching Nitro's makeSelfdestructGasFn (operations_acl.go:219-250).
    /// Handles cold account access and new account creation costs.
    /// </summary>
    /// <param name="isCold">Whether the beneficiary address is cold</param>
    /// <param name="isNewAccount">Whether creating a new account for the beneficiary</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MultiGas GasSelfDestructDynamic(bool isCold, bool isNewAccount)
    {
        MultiGas multiGas = MultiGas.Zero;

        // Cold account access → StorageAccess
        if (isCold)
            multiGas.SaturatingIncrementInto(ResourceKind.StorageAccess, GasCostOf.ColdAccountAccess);

        // New account creation → StorageGrowth (CreateBySelfDestructGas = 25000 = NewAccount)
        if (isNewAccount)
            multiGas.SaturatingIncrementInto(ResourceKind.StorageGrowth, GasCostOf.NewAccount);

        return multiGas;
    }
}
