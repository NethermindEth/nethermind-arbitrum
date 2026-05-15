// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Runtime.CompilerServices;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Int256;
using EvmMetrics = Nethermind.Evm.Metrics;

namespace Nethermind.Arbitrum.Evm;

internal static partial class ArbitrumEvmInstructions
{
    /// <summary>
    /// Reverts SELFDESTRUCT when executed by a Stylus contract; otherwise performs the full
    /// EIP-6780 SELFDESTRUCT semantics identically to <see cref="EvmInstructions.InstructionSelfDestruct{TGasPolicy, TEip8037, TEip7708}"/>.
    /// </summary>
    /// <remarks>
    /// Mirrors Nitro's go-ethereum/core/vm/instructions.go:949-958. By the time Nitro's Stylus
    /// check fires, the interpreter has already deducted constant SelfdestructGasEIP150 (5000)
    /// and dynamic gas (cold beneficiary access 2600, CreateBySelfdestructGas 25000 when the
    /// beneficiary is empty and the actor has non-zero balance) and warmed the beneficiary in
    /// the access list. The override here is a near-exact copy of base
    /// <see cref="EvmInstructions.InstructionSelfDestruct{TGasPolicy, TEip8037, TEip7708}"/>
    /// (submodule EvmInstructions.ControlFlow.cs:198-284) with one structural change: all gas
    /// charges that Nitro performs before opSelfdestruct6780 are pulled in front of the
    /// Stylus prefix check, and the check itself is placed before any state mutation. The
    /// non-Stylus path then continues with destruct logic in base's original order.
    /// </remarks>
    [SkipLocalsInit]
    public static EvmExceptionType InstructionSelfDestruct<TEip8037, TEip7708>(
        VirtualMachine<ArbitrumGasPolicy> vm, ref EvmStack stack, ref ArbitrumGasPolicy gas, ref int programCounter)
        where TEip8037 : struct, IFlag
        where TEip7708 : struct, IFlag
    {
        EvmMetrics.IncrementSelfDestructs();

        VmState<ArbitrumGasPolicy> vmState = vm.VmState;
        IReleaseSpec spec = vm.Spec;
        IWorldState state = vm.WorldState;

        // SELFDESTRUCT is forbidden during static calls. Matches Nitro's readOnly check at
        // opSelfdestruct6780:950 — both return an exceptional halt that burns remaining frame gas.
        if (vmState.IsStatic)
            goto StaticCallViolation;

        // Constant SelfdestructGasEIP150 (5000). Matches Nitro's interpreter.go:213 constantGas,
        // deducted before opSelfdestruct6780 begins.
        if (spec.UseShanghaiDDosProtection)
        {
            if (!ArbitrumGasPolicy.ConsumeSelfDestructGas(ref gas))
                goto OutOfGas;
        }

        // Pop beneficiary. Nitro peeks during dynamicGas (operations_acl.go:258) and pops later
        // in opSelfdestruct6780:960; semantically equivalent once the gas accounting matches.
        Address? inheritor = stack.PopAddress();
        if (inheritor is null)
            goto StackUnderflow;

        // Cold-access delta + access-list warmup. Matches Nitro's gasSelfdestructEIP3529 cold
        // branch at operations_acl.go:260-266.
        if (!ArbitrumGasPolicy.ConsumeAccountAccessGas(ref gas, spec, in vmState.AccessTracker,
                vm.TxTracer.IsTracingAccess, inheritor, chargeForWarm: false))
            goto OutOfGas;

        // New-account creation gas (CreateBySelfdestructGas = 25000). Matches Nitro's
        // gasSelfdestructEIP3529 empty-beneficiary branch at operations_acl.go:267-272. State
        // reads only at this stage; safe to perform before the Stylus check.
        Address executingAccount = vmState.Env.ExecutingAccount;
        UInt256 result = state.GetBalance(executingAccount);
        bool inheritorAccountExists = state.AccountExists(inheritor);
        bool chargesNewAccount = spec.ClearEmptyAccountWhenTouched switch
        {
            true => !result.IsZero && state.IsDeadAccount(inheritor),
            false => !inheritorAccountExists && spec.UseShanghaiDDosProtection,
        };
        if (chargesNewAccount && !ArbitrumGasPolicy.ConsumeNewAccountCreation<TEip8037>(ref gas))
            goto OutOfGas;

        // NITRO PARITY: opSelfdestruct6780:954-958. Stylus guard fires after every gas charge
        // Nitro performs in its interpreter and dynamicGas layers, and before any state mutation
        // (the ToBeDestroyed call below is the first). Same gas consumed, same Revert exit type.
        // AsSpan handles the IWorldState.GetCode nullable return: a null array maps to a default
        // span, which IsStylusComponentPrefix's internal length check short-circuits as false.
        byte[]? code = state.GetCode(executingAccount);
        ArbitrumVirtualMachine avm = AsArbitrum(vm);
        if (StylusCode.IsStylusComponentPrefix(code.AsSpan(), avm.CurrentArbosVersion))
            goto Revert;

        bool createInSameTx = vmState.AccessTracker.CreateList.Contains(executingAccount);
        bool selfdestructOnlyOnSameTx = spec.SelfdestructOnlyOnSameTransaction;
        if (!selfdestructOnlyOnSameTx || createInSameTx)
            vmState.AccessTracker.ToBeDestroyed(executingAccount);

        if (vm.TxTracer.IsTracingActions)
            vm.TxTracer.ReportSelfDestruct(executingAccount, result, inheritor);

        if (!inheritorAccountExists)
        {
            state.CreateAccount(inheritor, result);
        }
        else if (!inheritor.Equals(executingAccount))
        {
            state.AddToBalance(inheritor, result, spec);
        }

        // EIP-6780 same-tx-only path: when the actor wasn't created in this tx and is
        // destroying itself, no balance moves and no transfer log is emitted.
        if (selfdestructOnlyOnSameTx && !createInSameTx && inheritor.Equals(executingAccount))
            goto Stop;

        vm.AddSelfDestructLog<TEip8037, TEip7708>(executingAccount, inheritor, result);

        state.SubtractFromBalance(executingAccount, result, spec);

    // Jump forward to be unpredicted by the branch predictor — mirrors base
    // EvmInstructions.InstructionSelfDestruct and sibling override InstructionCall.
    Stop:
        return EvmExceptionType.Stop;
    Revert:
        return EvmExceptionType.Revert;
    OutOfGas:
        return EvmExceptionType.OutOfGas;
    StackUnderflow:
        return EvmExceptionType.StackUnderflow;
    StaticCallViolation:
        return EvmExceptionType.StaticCallViolation;
    }
}
