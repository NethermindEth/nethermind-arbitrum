// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Runtime.CompilerServices;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.Gas;
using Nethermind.Evm.State;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Evm;

/// <summary>
/// Custom SELFDESTRUCT instruction handler for Arbitrum with multi-dimensional gas tracking.
/// </summary>
internal static partial class ArbitrumEvmInstructions
{
    /// <summary>
    /// SELFDESTRUCT instruction with multi-dimensional gas tracking.
    /// Constant gas: 100 → Computation, 4900 → StorageAccess.
    /// Cold account access → StorageAccess.
    /// New account creation → StorageGrowth.
    /// </summary>
    [SkipLocalsInit]
    internal static EvmExceptionType InstructionSelfDestruct<TTracingInst>(
        VirtualMachine<MultiGasPolicy> vm,
        ref EvmStack stack,
        ref GasState gasState,
        ref int programCounter)
        where TTracingInst : struct, IFlag
    {
        Metrics.IncrementSelfDestructs();

        EvmState vmState = vm.EvmState;
        IReleaseSpec spec = vm.Spec;
        IWorldState state = vm.WorldState;

        // SELFDESTRUCT is forbidden during static calls
        if (vmState.IsStatic)
            return EvmExceptionType.StaticCallViolation;

        // If Shanghai DDoS protection is active, charge the appropriate gas cost with MultiGas
        if (spec.UseShanghaiDDosProtection)
        {
            // Compute MultiGas for constant gas (EIP-150): 100 → Computation, 4900 → StorageAccess
            MultiGas constantMultiGas = ArbitrumGas.GasSelfDestruct(GasCostOf.SelfDestructEip150);
            MultiGasPolicy.ConsumeGasWithMultiGas(ref gasState, GasCostOf.SelfDestructEip150, in constantMultiGas);
            if (MultiGasPolicy.GetRemainingGas(in gasState) < 0)
                return EvmExceptionType.OutOfGas;
        }

        // Pop the inheritor address from the stack
        Address? inheritor = stack.PopAddress();
        if (inheritor is null)
            return EvmExceptionType.StackUnderflow;

        // Determine cold/warm BEFORE warming up for multigas
        bool isCold = spec.UseHotAndColdStorage &&
                      !spec.IsPrecompile(inheritor) &&
                      vmState.AccessTracker.IsCold(inheritor);

        // Charge gas for account access with MultiGas
        if (!ChargeAccountAccessGasForSelfDestruct(ref gasState, vm, inheritor, spec, isCold))
            return EvmExceptionType.OutOfGas;

        Address executingAccount = vmState.Env.ExecutingAccount;
        bool createInSameTx = vmState.AccessTracker.CreateList.Contains(executingAccount);

        // Mark the executing account for destruction if allowed
        if (!spec.SelfdestructOnlyOnSameTransaction || createInSameTx)
            vmState.AccessTracker.ToBeDestroyed(executingAccount);

        // Retrieve the current balance for transfer
        UInt256 result = state.GetBalance(executingAccount);
        if (vm.TxTracer.IsTracingActions)
            vm.TxTracer.ReportSelfDestruct(executingAccount, result, inheritor);

        // For certain specs, charge gas if transferring to a dead account
        if (spec.ClearEmptyAccountWhenTouched && !result.IsZero && state.IsDeadAccount(inheritor))
        {
            // New account → StorageGrowth
            MultiGas newAccountMultiGas = MultiGas.Zero;
            newAccountMultiGas.SaturatingIncrementInto(ResourceKind.StorageGrowth, GasCostOf.NewAccount);
            MultiGasPolicy.ConsumeGasWithMultiGas(ref gasState, GasCostOf.NewAccount, in newAccountMultiGas);
            if (MultiGasPolicy.GetRemainingGas(in gasState) < 0)
                return EvmExceptionType.OutOfGas;
        }

        // If account creation rules apply, ensure gas is charged for new accounts
        bool inheritorAccountExists = state.AccountExists(inheritor);
        if (!spec.ClearEmptyAccountWhenTouched && !inheritorAccountExists && spec.UseShanghaiDDosProtection)
        {
            // New account → StorageGrowth
            MultiGas newAccountMultiGas = MultiGas.Zero;
            newAccountMultiGas.SaturatingIncrementInto(ResourceKind.StorageGrowth, GasCostOf.NewAccount);
            MultiGasPolicy.ConsumeGasWithMultiGas(ref gasState, GasCostOf.NewAccount, in newAccountMultiGas);
            if (MultiGasPolicy.GetRemainingGas(in gasState) < 0)
                return EvmExceptionType.OutOfGas;
        }

        // Create or update the inheritor account with the transferred balance
        if (!inheritorAccountExists)
            state.CreateAccount(inheritor, result);
        else if (!inheritor.Equals(executingAccount))
            state.AddToBalance(inheritor, result, spec);

        // Special handling when SELFDESTRUCT is limited to the same transaction
        if (spec.SelfdestructOnlyOnSameTransaction && !createInSameTx && inheritor.Equals(executingAccount))
            return EvmExceptionType.Stop; // Avoid burning ETH if contract is not destroyed per EIP clarification

        // Subtract the balance from the executing account
        state.SubtractFromBalance(executingAccount, result, spec);

        return EvmExceptionType.Stop;
    }

    /// <summary>
    /// Charge account access gas with proper MultiGas tracking for SELFDESTRUCT.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ChargeAccountAccessGasForSelfDestruct(
        ref GasState gasState,
        VirtualMachine<MultiGasPolicy> vm,
        Address address,
        IReleaseSpec spec,
        bool isCold)
    {
        if (!spec.UseHotAndColdStorage)
            return true;

        EvmState vmState = vm.EvmState;
        ref readonly StackAccessTracker accessTracker = ref vmState.AccessTracker;

        if (vm.TxTracer.IsTracingAccess)
            accessTracker.WarmUp(address);

        // Warm up the account
        if (!spec.IsPrecompile(address))
            accessTracker.WarmUp(address);

        long gasCost;
        if (isCold)
            gasCost = GasCostOf.ColdAccountAccess;
        else
            gasCost = GasCostOf.WarmStateRead;

        // Compute MultiGas: cold → StorageAccess, warm → Computation
        if (isCold)
        {
            // Cold: charge to StorageAccess
            MultiGas multiGas = MultiGas.Zero;
            multiGas.SaturatingIncrementInto(ResourceKind.StorageAccess, (ulong)gasCost);
            MultiGasPolicy.ConsumeGasWithMultiGas(ref gasState, gasCost, in multiGas);
        }
        else
        {
            // Warm: all to Computation
            MultiGasPolicy.ConsumeGas(ref gasState, gasCost, Instruction.SELFDESTRUCT);
        }

        return MultiGasPolicy.GetRemainingGas(in gasState) >= 0;
    }
}
