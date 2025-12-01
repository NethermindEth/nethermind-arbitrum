// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Runtime.CompilerServices;
using Nethermind.Core.Extensions;
using Nethermind.Evm;

namespace Nethermind.Arbitrum.Evm;

/// <summary>
/// Gas functions for Arbitrum EVM operations.
/// Returns MultiGas with proper resource categorization for multi-dimensional gas tracking.
/// </summary>
public static class ArbitrumGas
{
    /// <summary>
    /// SSTORE gas calculation with EIP-2929 access lists.
    /// Returns MultiGas with proper categorization based on current/original values.
    /// Does NOT deduct gas or update storage - caller handles that.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MultiGas GasSStoreEIP2929(
        bool isCold,
        ReadOnlySpan<byte> currentValue,
        ReadOnlySpan<byte> originalValue,
        ReadOnlySpan<byte> newValue)
    {
        MultiGas multiGas = MultiGas.Zero;

        // Cold slot access → StorageAccess
        if (isCold)
            multiGas.SaturatingIncrementInto(ResourceKind.StorageAccess, GasCostOf.ColdSLoad);

        bool newIsZero = newValue.IsZero();
        bool currentIsZero = currentValue.IsZero();

        // No-op case: current == new → Computation (warm slot read)
        bool newSameAsCurrent = (newIsZero && currentIsZero) || Bytes.AreEqual(currentValue, newValue);
        if (newSameAsCurrent)
        {
            multiGas.SaturatingIncrementInto(ResourceKind.Computation, GasCostOf.WarmStateRead);
            return multiGas;
        }

        // Current same as original?
        bool currentSameAsOriginal = Bytes.AreEqual(originalValue, currentValue);
        if (currentSameAsOriginal)
        {
            if (currentIsZero)
            {
                // Create slot (0 → nonzero) → StorageGrowth
                multiGas.SaturatingIncrementInto(ResourceKind.StorageGrowth, GasCostOf.SSet);
            }
            else
            {
                // Write existing slot → StorageAccess (SReset - ColdSLoad = 2900)
                multiGas.SaturatingIncrementInto(ResourceKind.StorageAccess, GasCostOf.SReset - GasCostOf.ColdSLoad);
            }
            return multiGas;
        }

        // Dirty update (current != original) → Computation (warm slot read)
        multiGas.SaturatingIncrementInto(ResourceKind.Computation, GasCostOf.WarmStateRead);
        return multiGas;
    }

    /// <summary>
    /// SLOAD gas calculation with EIP-2929 access lists.
    /// Cold: StorageAccess(cold - warm) + Computation(warm)
    /// Warm: Computation(warm)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MultiGas GasSLoadEIP2929(bool isCold)
    {
        if (isCold)
        {
            // Cold: split between StorageAccess and Computation
            MultiGas multiGas = MultiGas.Zero;
            multiGas.SaturatingIncrementInto(ResourceKind.StorageAccess,
                GasCostOf.ColdSLoad - GasCostOf.WarmStateRead);
            multiGas.SaturatingIncrementInto(ResourceKind.Computation,
                GasCostOf.WarmStateRead);
            return multiGas;
        }
        // Warm: Computation only
        return MultiGas.ComputationGas(GasCostOf.WarmStateRead);
    }

    /// <summary>
    /// CALL gas calculation with EIP-2929 access lists.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MultiGas GasCall(bool isCold, bool transfersValue, bool isDeadAccount)
    {
        MultiGas multiGas = MultiGas.Zero;

        // Base warm access → Computation
        multiGas.SaturatingIncrementInto(ResourceKind.Computation, GasCostOf.WarmStateRead);

        // Cold account access → StorageAccess (delta only, warm already counted)
        if (isCold)
            multiGas.SaturatingIncrementInto(ResourceKind.StorageAccess, GasCostOf.ColdAccountAccess - GasCostOf.WarmStateRead);

        if (transfersValue)
        {
            // Value transfer → Computation
            multiGas.SaturatingIncrementInto(ResourceKind.Computation, GasCostOf.CallValue);

            // New account creation → StorageGrowth
            if (isDeadAccount)
                multiGas.SaturatingIncrementInto(ResourceKind.StorageGrowth, GasCostOf.NewAccount);
        }

        return multiGas;
    }

    /// <summary>
    /// LOG gas calculation.
    /// Base cost → Computation
    /// Topics → split between HistoryGrowth (256/topic) and Computation (119/topic)
    /// Data → HistoryGrowth
    /// </summary>
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
    /// Account creation → StorageGrowth
    /// Init code word cost (EIP-3860) → Computation
    /// CREATE2 hash cost → Computation
    /// </summary>
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MultiGas GasMemory(long memoryCost)
    {
        return MultiGas.ComputationGas((ulong)memoryCost);
    }

    /// <summary>
    /// SELFDESTRUCT constant gas (EIP-150).
    /// Split: Computation(100) + StorageAccess(remaining)
    /// </summary>
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

        // Fallback for other costs (pre-EIP150)
        return MultiGas.StorageAccessGas((ulong)gasCost);
    }

    /// <summary>
    /// SELFDESTRUCT dynamic gas with EIP-2929 access lists.
    /// Cold account access → StorageAccess
    /// New account creation → StorageGrowth
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MultiGas GasSelfDestructDynamic(bool isCold, bool isNewAccount)
    {
        MultiGas multiGas = MultiGas.Zero;

        // Cold account access → StorageAccess
        if (isCold)
            multiGas.SaturatingIncrementInto(ResourceKind.StorageAccess, GasCostOf.ColdAccountAccess);

        // New account creation → StorageGrowth
        if (isNewAccount)
            multiGas.SaturatingIncrementInto(ResourceKind.StorageGrowth, GasCostOf.NewAccount);

        return multiGas;
    }
}
