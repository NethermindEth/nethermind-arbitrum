// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Runtime.CompilerServices;
using Nethermind.Evm;
using Nethermind.Evm.Gas;

namespace Nethermind.Arbitrum.Evm;

/// <summary>
/// Arbitrum multi-dimensional gas policy.
/// Tracks gas across multiple resource dimensions (Computation, Storage, L1 calldata, etc.).
/// </summary>
public readonly struct MultiGasPolicy : IGasPolicy<MultiGasPolicy>
{
    /// <summary>
    /// Internal state for multigas tracking.
    /// Stored in GasState.PolicyData.
    /// </summary>
    private sealed class PolicyState
    {
        public MultiGas Multigas; // Current multigas accumulation
        public MultiGas Retained; // Gas forwarded to child frames
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GasState InitializeForTransaction(long gasLimit, long intrinsicGas)
    {
        // Convert intrinsic gas to multigas (all Computation for now)
        // This is used by non-Arbitrum code paths or when multigas is not enabled
        MultiGas intrinsicMultigas = MultiGas.Zero.SaturatingIncrement(
            ResourceKind.Computation, (ulong)intrinsicGas);

        PolicyState state = new()
        {
            Multigas = intrinsicMultigas,
            Retained = MultiGas.Zero
        };

        // NOTE: gasLimit is already post-intrinsic (TransactionProcessor deducts intrinsic before calling VM)
        // We only track intrinsic in Multigas for receipt data, not in RemainingGas
        return new GasState(gasLimit, state);
    }

    /// <summary>
    /// Initialize gas state for transaction with multi-dimensional intrinsic gas.
    /// Used by Arbitrum to properly categorize intrinsic gas across resource dimensions.
    /// Intrinsic multigas is added BEFORE execution starts.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GasState InitializeForTransaction(long gasLimit, MultiGas intrinsicMultigas)
    {
        PolicyState state = new()
        {
            Multigas = intrinsicMultigas,
            Retained = MultiGas.Zero
        };

        // NOTE: gasLimit is already post-intrinsic (TransactionProcessor deducts intrinsic before calling VM)
        // We only track intrinsic in Multigas for receipt data, not in RemainingGas
        return new GasState(gasLimit, state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetRemainingGas(in GasState gasState)
    {
        return gasState.RemainingGas;
    }

    public static void ConsumeGas(ref GasState gasState, long gasCost, Instruction instruction)
    {
        // Deduct from single-dimensional gas
        gasState.RemainingGas -= gasCost;

        // Match Nitro's addConstantMultiGas exactly:
        // - SELFDESTRUCT constant (5000): 100 Computation + 4900 StorageAccess
        // - Everything else: ALL → Computation
        //
        // Complex instructions (SSTORE, SLOAD, BALANCE, CALL, etc.) use custom handlers
        // that call ConsumeGasWithMultiGas() or AddMultiGas() directly, bypassing this method.
        PolicyState state = (PolicyState)gasState.PolicyData!;

        if (instruction == Instruction.SELFDESTRUCT && gasCost == GasCostOf.SelfDestructEip150)
        {
            state.Multigas.SaturatingIncrementInto(ResourceKind.Computation, GasCostOf.WarmStateRead);
            state.Multigas.SaturatingIncrementInto(ResourceKind.StorageAccess,
                GasCostOf.SelfDestructEip150 - GasCostOf.WarmStateRead);
        }
        else
        {
            state.Multigas.SaturatingIncrementInto(ResourceKind.Computation, (ulong)gasCost);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ApplyRefund(ref GasState gasState, long refundAmount)
    {
        // No-op: Refunds are tracked in EvmState.Refund (standard EVM pattern).
        // At transaction end, FinalizeRefund copies the accumulated refund into MultiGas.
    }

    /// <summary>
    /// Add pre-computed MultiGas directly to the policy state.
    /// Used by custom instruction handlers that compute MultiGas with full state access
    /// (matching Nitro's dynamicGas pattern).
    /// </summary>
    /// <param name="gasState">The gas state containing PolicyData</param>
    /// <param name="multiGas">Pre-computed MultiGas to add</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddMultiGas(ref GasState gasState, in MultiGas multiGas)
    {
        if (gasState.PolicyData is PolicyState state)
            state.Multigas.SaturatingAddInto(in multiGas);
    }

    /// <summary>
    /// Consume gas and add pre-computed MultiGas in one operation.
    /// Used by custom instruction handlers that bypass CategorizeGasInto.
    /// </summary>
    /// <param name="gasState">The gas state</param>
    /// <param name="gasCost">Gas cost to deduct from RemainingGas</param>
    /// <param name="multiGas">Pre-computed MultiGas to add</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ConsumeGasWithMultiGas(ref GasState gasState, long gasCost, in MultiGas multiGas)
    {
        // Deduct from single-dimensional gas
        gasState.RemainingGas -= gasCost;

        // Add pre-computed multigas directly
        if (gasState.PolicyData is PolicyState state)
            state.Multigas.SaturatingAddInto(in multiGas);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RefundGas(ref GasState gasState, long gasAmount)
    {
        gasState.RemainingGas += gasAmount;
        // Note: Refunded gas is not categorized back into multigas dimensions.
        // Returned gas doesn't affect accumulated multigas tracking.
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetOutOfGas(ref GasState gasState)
    {
        gasState.RemainingGas = 0;
        // PolicyData (multigas) remains unchanged - shows what was used before OOG
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GasState InitializeChildFrame(long gasProvided)
    {
        // Child starts with fresh multigas
        PolicyState childState = new()
        {
            Multigas = MultiGas.Zero,
            Retained = MultiGas.Zero
        };

        return new GasState(gasProvided, childState);
    }

    public static void MergeChildFrame(
        ref GasState parentState,
        in GasState childState,
        long gasProvided)
    {
        // NOTE: RemainingGas is handled by VirtualMachine.ExecuteTransaction separately
        // This method only merges multigas data

        PolicyState parentPolicy = (PolicyState)parentState.PolicyData!;
        PolicyState childPolicy = (PolicyState)childState.PolicyData!;

        // Add child's entire multigas to parent (mutates in place for performance).
        // We add entire Used, not net (Used - Retained). The Retained is for tracking
        // what was forwarded, not for subtraction during merge.
        parentPolicy.Multigas.SaturatingAddInto(in childPolicy.Multigas);

        // Track forwarded gas as Computation in Retained.
        // Cold access is already tracked at charge time.
        parentPolicy.Retained.SaturatingIncrementInto(ResourceKind.Computation, (ulong)gasProvided);
    }

    /// <summary>
    /// Get final gas used from the multigas structure.
    /// For multigas policy, this is derived from multigas.SingleGas() (Total - Refund),
    /// NOT from single-dimensional (gasLimit - remainingGas).
    /// This ensures receipt.GasUsed always equals multigas.SingleGas().
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetFinalGasUsed(in GasState gasState, long gasLimit)
    {
        if (gasState.PolicyData is not PolicyState state)
            return gasLimit - gasState.RemainingGas; // Fallback to single-dim

        // Derive from multigas: SingleGas() = Total - Refund
        return (long)state.Multigas.SingleGas();
    }

    /// <summary>
    /// Finalize gas state by copying the accumulated refund from EvmState into MultiGas.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FinalizeRefund(ref GasState gasState, long refund)
    {
        if (gasState.PolicyData is not PolicyState state)
            return;

        // Copy refund from EvmState into MultiGas (mutates in place)
        state.Multigas.SetRefund((ulong)refund);
    }

    public static object? GetReceiptData(in GasState gasState)
    {
        if (gasState.PolicyData is not PolicyState state)
            return null;

        // Return multigas directly - intrinsic multigas is already included in state.Multigas
        // (added at initialization before execution starts)
        return state.Multigas;
    }

    // NOTE: Complex instructions (SSTORE, SLOAD, BALANCE, CALL, LOG, etc.) use custom handlers
    // in ArbitrumEvmInstructions.*.cs that call ConsumeGasWithMultiGas() or AddMultiGas() directly.
    // This matches Nitro's architecture where:
    // - constantGas → addConstantMultiGas() → Computation (handled by ConsumeGas above)
    // - dynamicGas → gasFunc() → returns MultiGas directly (handled by custom handlers)
}
