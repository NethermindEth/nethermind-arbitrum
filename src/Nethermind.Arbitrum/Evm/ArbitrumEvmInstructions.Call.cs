// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.GasPolicy;
using Nethermind.Evm.State;
using Nethermind.Int256;
using static Nethermind.Evm.EvmInstructions;
using EvmMetrics = Nethermind.Evm.Metrics;

namespace Nethermind.Arbitrum.Evm;

internal static partial class ArbitrumEvmInstructions
{
    /// <summary>
    /// Exactly same implementation as the submodule's, except for the AccountExists and IsDeadAccount
    /// lookups over the target address, which record trie nodes useful for witness generation,
    /// before any out-of-gas check is made.
    /// This mimics nitro's behaviour in the "oldCalculator" parameter passed to makeCallVariantGasCallEIP7702 method.
    [SkipLocalsInit]
    public static EvmExceptionType InstructionCall<TGasPolicy, TOpCall, TTracingInst, TEip8037, TEip7708>(VirtualMachine<TGasPolicy> vm,
        ref EvmStack stack,
        ref TGasPolicy gas,
        ref int programCounter)
        where TGasPolicy : struct, IGasPolicy<TGasPolicy>
        where TOpCall : struct, IOpCall
        where TTracingInst : struct, IFlag
        where TEip8037 : struct, IFlag
        where TEip7708 : struct, IFlag
    {
        // Increment global call metrics.
        EvmMetrics.IncrementCalls();

        // Clear previous return data.
        vm.ReturnData = null!;

        // Pop the gas limit for the call.
        if (!stack.PopUInt256(out UInt256 gasLimit))
            goto StackUnderflow;
        // Pop the code source address from the stack.
        Address? codeSource = stack.PopAddress();
        if (codeSource is null)
            goto StackUnderflow;

        ExecutionEnvironment env = vm.VmState.Env;
        // Determine the call value based on the call type.
        UInt256 callValue;
        if (TOpCall.ExecutionType == ExecutionType.STATICCALL)
        {
            // Static calls cannot transfer value.
            callValue = default;
        }
        else if (TOpCall.ExecutionType == ExecutionType.DELEGATECALL)
        {
            // Delegate calls use the value from the current execution context.
            callValue = env.Value;
        }
        else if (!stack.PopUInt256(out callValue))
        {
            goto StackUnderflow;
        }

        // Pop additional parameters: data offset, data length, output offset, and output length.
        if (!stack.PopUInt256(out UInt256 dataOffset) ||
            !stack.PopUInt256(out UInt256 dataLength) ||
            !stack.PopUInt256(out UInt256 outputOffset) ||
            !stack.PopUInt256(out UInt256 outputLength))
        {
            goto StackUnderflow;
        }

        // For non-delegate calls, the transfer value is the call value.
        bool hasValueTransfer = TOpCall.ExecutionType != ExecutionType.DELEGATECALL && !callValue.IsZero;
        // Enforce static call restrictions: no value transfer allowed unless it's a CALLCODE.
        if (vm.VmState.IsStatic && hasValueTransfer && TOpCall.ExecutionType != ExecutionType.CALLCODE)
            return EvmExceptionType.StaticCallViolation;

        // Determine caller and target based on the call type.
        Address caller = TOpCall.ExecutionType == ExecutionType.DELEGATECALL ? env.Caller : env.ExecutingAccount;
        Address target = TOpCall.ExecutionType != ExecutionType.DELEGATECALL && TOpCall.ExecutionType != ExecutionType.CALLCODE
            ? codeSource
            : env.ExecutingAccount;

        IReleaseSpec spec = vm.Spec;

        IWorldState state = vm.WorldState;

        // Charge additional gas if the target account is new or considered empty.
        bool chargesNewAccount = spec.ClearEmptyAccountWhenTouched switch
        {
            false => !state.AccountExists(target),
            true => hasValueTransfer && state.IsDeadAccount(target),
        };

        // Add extra gas cost if value is transferred.
        if (hasValueTransfer && !TGasPolicy.ConsumeCallValueTransfer(ref gas))
            goto OutOfGas;

        // Update gas: call cost and memory expansion for input and output.
        if (!TGasPolicy.UpdateGas(ref gas, spec.GasCosts.CallCost) ||
            !TGasPolicy.UpdateMemoryCost(ref gas, in dataOffset, dataLength, vm.VmState) ||
            !TGasPolicy.UpdateMemoryCost(ref gas, in outputOffset, outputLength, vm.VmState))
            goto OutOfGas;

        // Charge gas for accessing the account's code (including delegation logic if applicable).
        if (!TGasPolicy.ConsumeAccountAccessGas(ref gas, vm.Spec, in vm.VmState.AccessTracker, vm.TxTracer.IsTracingAccess, codeSource))
            goto OutOfGas;

        CodeInfo codeInfo = vm.CodeInfoRepository.GetCachedCodeInfo(codeSource, followDelegation: false, vmSpec: spec, delegationAddress: out Address? delegated);

        if (spec.UseHotAndColdStorage &&
            delegated is not null &&
            !TGasPolicy.ConsumeAccountAccessGas(ref gas, vm.Spec, in vm.VmState.AccessTracker, vm.TxTracer.IsTracingAccess, delegated))
            goto OutOfGas;

        bool newAccountOutOfGas = chargesNewAccount && !TGasPolicy.ConsumeNewAccountCreation<TEip8037>(ref gas);

        if (newAccountOutOfGas)
            goto OutOfGas;

        // EIP-7702: load delegated code after cold-access charge above.
        if (delegated is not null)
        {
            // EIP-7928: decorator fast-path skips world-state reads; record explicitly.
            state.AddAccountRead(delegated);

            // EIP-7702: precompile MUST NOT execute via delegation; the decorator would route to the precompile CodeInfo.
            codeInfo = spec.IsPrecompile(delegated)
                ? CodeInfo.Empty
                : vm.CodeInfoRepository.GetCachedCodeInfoNoDelegation(delegated, spec);
        }

        // Get remaining gas for 63/64 calculation
        long gasAvailable = TGasPolicy.GetRemainingGas(in gas);
        long gasLimitUl;

        if (spec.Use63Over64Rule)
        {
            // EIP-150: cap is a non-negative long, so min(gasLimit, cap) fits without 256-bit math.
            Debug.Assert(gasAvailable >= 0, "GetRemainingGas must be non-negative; (ulong)cap below would otherwise wrap.");
            long cap = gasAvailable - gasAvailable / 64;
            gasLimitUl = gasLimit.IsUint64 && gasLimit.u0 <= (ulong)cap
                ? (long)gasLimit.u0
                : cap;
        }
        else
        {
            if (!gasLimit.IsUint64 || gasLimit.u0 >= long.MaxValue)
                goto OutOfGas;

            gasLimitUl = (long)gasLimit.u0;
        }

        if (!TGasPolicy.UpdateGas(ref gas, gasLimitUl))
            goto OutOfGas;

        // Add call stipend if value is being transferred.
        if (hasValueTransfer)
        {
            if (vm.TxTracer.IsTracingRefunds)
                vm.TxTracer.ReportExtraGasPressure(GasCostOf.CallStipend);
            gasLimitUl += GasCostOf.CallStipend;
        }

        // Check call depth and balance of the caller.
        if (env.CallDepth >= VirtualMachineStatics.MaxCallDepth ||
            (hasValueTransfer && state.GetBalance(env.ExecutingAccount) < callValue))
        {
            // If the call cannot proceed, return an empty response and push zero on the stack.
            vm.ReturnDataBuffer = Array.Empty<byte>();
            EvmExceptionType pushResult = stack.PushZero<TTracingInst>();

            // Optionally report memory changes for refund tracing.
            if (vm.TxTracer.IsTracingRefunds)
            {
                // Specific to Parity tracing: inspect 32 bytes from data offset.
                ReadOnlyMemory<byte>? memoryTrace = vm.VmState.Memory.Inspect(in dataOffset, 32);
                vm.TxTracer.ReportMemoryChange(dataOffset, memoryTrace is null ? default : memoryTrace.Value.Span);
            }

            if (TTracingInst.IsActive)
            {
                vm.TxTracer.ReportOperationRemainingGas(TGasPolicy.GetRemainingGas(in gas));
                vm.TxTracer.ReportOperationError(EvmExceptionType.NotEnoughBalance);
            }

            // Refund the remaining gas to the caller.
            TGasPolicy.UpdateGasUp(ref gas, gasLimitUl);
            if (TTracingInst.IsActive)
            {
                vm.TxTracer.ReportGasUpdateForVmTrace(gasLimitUl, TGasPolicy.GetRemainingGas(in gas));
            }

            return pushResult;
        }

        // Fast-path for calls to externally owned accounts (non-contracts)
        if (codeInfo.IsEmpty && !TTracingInst.IsActive && !vm.TxTracer.IsTracingActions)
        {
            vm.ReturnDataBuffer = default;
            // Mutate balances only after the success byte is on the stack; this fast path has no snapshot to roll back a failed push.
            EvmExceptionType pushResult = stack.PushBytes<TTracingInst>(StatusCode.SuccessBytes.Span);
            if (pushResult != EvmExceptionType.None)
                return pushResult;

            TGasPolicy.UpdateGasUp(ref gas, gasLimitUl);
            // Self-call (always true for CALLCODE; runtime for CALL/STATICCALL when target == executing account):
            // the +/- value balance ops cancel and target is the currently-executing account (alive),
            // so skip both writes. AddTransferLog already no-ops when from == to.
            bool isSelfCall = caller == target;
            if (!isSelfCall)
            {
                if (hasValueTransfer)
                {
                    state.SubtractFromBalance(caller, in callValue, spec);
                    vm.AddTransferLog<TEip7708>(caller, target, in callValue);
                }

                state.AddToBalanceAndCreateIfNotExists(target, TOpCall.ExecutionType, in callValue, spec);
            }

            EvmMetrics.IncrementEmptyCalls();
            vm.ReturnData = null!;
            return EvmExceptionType.None;
        }

        return CreateFullCallFrame(vm, ref gas, in dataOffset, dataLength, outputOffset, outputLength, codeInfo, target, caller, codeSource, env, in callValue,
            gasLimitUl);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static EvmExceptionType CreateFullCallFrame(
            VirtualMachine<TGasPolicy> vm,
            ref TGasPolicy gas,
            in UInt256 dataOffset,
            UInt256 dataLength,
            UInt256 outputOffset,
            UInt256 outputLength,
            CodeInfo codeInfo,
            Address target,
            Address caller,
            Address codeSource,
            ExecutionEnvironment env,
            in UInt256 callValue,
            long gasLimitUl)
        {
            IWorldState state = vm.WorldState;
            // Take a snapshot of the state for potential rollback.
            Snapshot snapshot = state.TakeSnapshot();
            // Subtract the transfer value from the caller's balance.
            if (TOpCall.ExecutionType != ExecutionType.DELEGATECALL && !callValue.IsZero)
                state.SubtractFromBalance(caller, in callValue, vm.Spec);

            // Load call data from memory.
            if (!vm.VmState.Memory.TryLoad(in dataOffset, dataLength, out ReadOnlyMemory<byte> callData))
                return EvmExceptionType.OutOfGas;

            // Construct the execution environment for the call.
            ExecutionEnvironment callEnv = ExecutionEnvironment.Rent(
                codeInfo: codeInfo,
                executingAccount: target,
                caller: caller,
                codeSource: codeSource,
                callDepth: env.CallDepth + 1,
                value: in callValue,
                inputData: in callData);

            // Normalize output offset if output length is zero.
            if (outputLength.IsZero)
            {
                // Output offset is inconsequential when output length is 0.
                outputOffset = default;
            }

            // Rent a new call frame for executing the call.
            vm.ReturnData = VmState<TGasPolicy>.RentFrame(
                gas: TGasPolicy.CreateChildFrameGas(ref gas, gasLimitUl),
                outputDestination: outputOffset.ToLong(),
                outputLength: outputLength.ToLong(),
                executionType: TOpCall.ExecutionType,
                isStatic: TOpCall.ExecutionType == ExecutionType.STATICCALL || vm.VmState.IsStatic,
                isCreateOnPreExistingAccount: false,
                env: callEnv,
                stateForAccessLists: in vm.VmState.AccessTracker,
                snapshot: in snapshot);

            return EvmExceptionType.None;
        }

        // Jump forward to be unpredicted by the branch predictor.
    StackUnderflow:
        return EvmExceptionType.StackUnderflow;
    OutOfGas:
        return EvmExceptionType.OutOfGas;
    }
}
