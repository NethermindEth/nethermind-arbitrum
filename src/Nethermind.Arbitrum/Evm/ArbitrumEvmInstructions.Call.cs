// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

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
        if (typeof(TOpCall) == typeof(OpStaticCall))
        {
            // Static calls cannot transfer value.
            callValue = UInt256.Zero;
        }
        else if (typeof(TOpCall) == typeof(OpDelegateCall))
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
        UInt256 transferValue = typeof(TOpCall) == typeof(OpDelegateCall) ? UInt256.Zero : callValue;
        // Enforce static call restrictions: no value transfer allowed unless it's a CALLCODE.
        if (vm.VmState.IsStatic && !transferValue.IsZero && typeof(TOpCall) != typeof(OpCallCode))
            return EvmExceptionType.StaticCallViolation;

        // Determine caller and target based on the call type.
        Address caller = typeof(TOpCall) == typeof(OpDelegateCall) ? env.Caller : env.ExecutingAccount;
        Address target = (typeof(TOpCall) == typeof(OpCall) || typeof(TOpCall) == typeof(OpStaticCall))
            ? codeSource
            : env.ExecutingAccount;

        IReleaseSpec spec = vm.Spec;

        IWorldState state = vm.WorldState;

        // Charge additional gas if the target account is new or considered empty.
        bool chargesNewAccount = spec.ClearEmptyAccountWhenTouched switch
        {
            false => !state.AccountExists(target),
            true => transferValue != 0 && state.IsDeadAccount(target),
        };

        // Add extra gas cost if value is transferred.
        if (!transferValue.IsZero)
        {
            if (!TGasPolicy.ConsumeCallValueTransfer(ref gas))
                goto OutOfGas;
        }

        // Update gas: call cost and memory expansion for input and output.
        if (!TGasPolicy.UpdateGas(ref gas, spec.GasCosts.CallCost) ||
            !TGasPolicy.UpdateMemoryCost(ref gas, in dataOffset, dataLength, vm.VmState) ||
            !TGasPolicy.UpdateMemoryCost(ref gas, in outputOffset, outputLength, vm.VmState))
            goto OutOfGas;

        // Charge gas for accessing the account's code (including delegation logic if applicable).
        if (!TGasPolicy.ConsumeAccountAccessGas(ref gas, vm.Spec, in vm.VmState.AccessTracker,
                vm.TxTracer.IsTracingAccess, codeSource))
            goto OutOfGas;
        bool _ = vm.TxExecutionContext.CodeInfoRepository
            .TryGetDelegation(codeSource, vm.Spec, out Address? delegated);

        if (spec.UseHotAndColdStorage && delegated is not null)
        {
            if (!TGasPolicy.ConsumeAccountAccessGas(ref gas, vm.Spec, in vm.VmState.AccessTracker,
                    vm.TxTracer.IsTracingAccess, delegated))
                goto OutOfGas;
        }

        bool newAccountOutOfGas = chargesNewAccount && !TGasPolicy.ConsumeNewAccountCreation<TEip8037>(ref gas);

        if (newAccountOutOfGas)
            goto OutOfGas;

        // Retrieve code information for the call and schedule background analysis if needed.
        CodeInfo codeInfo = vm.CodeInfoRepository.GetCachedCodeInfo(codeSource, spec);

        // Get remaining gas for 63/64 calculation
        long gasAvailable = TGasPolicy.GetRemainingGas(in gas);

        // Apply the 63/64 gas rule if enabled.
        if (spec.Use63Over64Rule)
        {
            gasLimit = UInt256.Min((UInt256)(gasAvailable - gasAvailable / 64), gasLimit);
        }

        // If gasLimit exceeds the host's representable range, treat as out-of-gas.
        if (gasLimit >= long.MaxValue)
            goto OutOfGas;

        long gasLimitUl = (long)gasLimit;
        if (!TGasPolicy.UpdateGas(ref gas, gasLimitUl))
            goto OutOfGas;

        // Add call stipend if value is being transferred.
        if (!transferValue.IsZero)
        {
            if (vm.TxTracer.IsTracingRefunds)
                vm.TxTracer.ReportExtraGasPressure(GasCostOf.CallStipend);
            gasLimitUl += GasCostOf.CallStipend;
        }

        // Check call depth and balance of the caller.
        if (env.CallDepth >= VirtualMachineStatics.MaxCallDepth ||
            (!transferValue.IsZero && state.GetBalance(env.ExecutingAccount) < transferValue))
        {
            // If the call cannot proceed, return an empty response and push zero on the stack.
            vm.ReturnDataBuffer = Array.Empty<byte>();
            stack.PushZero<TTracingInst>();

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
            return EvmExceptionType.None;
        }

        // Take a snapshot of the state for potential rollback.
        Snapshot snapshot = state.TakeSnapshot();
        // Subtract the transfer value from the caller's balance.
        state.SubtractFromBalance(caller, in transferValue, spec);

        // Fast-path for calls to externally owned accounts (non-contracts)
        if (codeInfo.IsEmpty && !TTracingInst.IsActive && !vm.TxTracer.IsTracingActions)
        {
            vm.ReturnDataBuffer = default;
            stack.PushBytes<TTracingInst>(StatusCode.SuccessBytes.Span);
            TGasPolicy.UpdateGasUp(ref gas, gasLimitUl);
            vm.AddTransferLog<TEip7708>(caller, target, transferValue);
            return FastCall(vm, spec, in transferValue, target);
        }

        // Load call data from memory.
        if (!vm.VmState.Memory.TryLoad(in dataOffset, dataLength, out ReadOnlyMemory<byte> callData))
            goto OutOfGas;
        // Construct the execution environment for the call.
        ExecutionEnvironment callEnv = ExecutionEnvironment.Rent(
            codeInfo: codeInfo,
            executingAccount: target,
            caller: caller,
            codeSource: codeSource,
            callDepth: env.CallDepth + 1,
            transferValue: in transferValue,
            value: in callValue,
            inputData: in callData);

        // Normalize output offset if output length is zero.
        if (outputLength == 0)
        {
            // Output offset is inconsequential when output length is 0.
            outputOffset = 0;
        }

        // Rent a new call frame for executing the call.
        vm.ReturnData = VmState<TGasPolicy>.RentFrame(
            gas: TGasPolicy.CreateChildFrameGas(ref gas, gasLimitUl),
            outputDestination: outputOffset.ToLong(),
            outputLength: outputLength.ToLong(),
            executionType: TOpCall.ExecutionType,
            isStatic: TOpCall.IsStatic || vm.VmState.IsStatic,
            isCreateOnPreExistingAccount: false,
            env: callEnv,
            stateForAccessLists: in vm.VmState.AccessTracker,
            snapshot: in snapshot);

        return EvmExceptionType.None;

        // Fast-call path for non-contract calls:
        // Directly credit the target account and avoid constructing a full call frame.
        static EvmExceptionType FastCall(VirtualMachine<TGasPolicy> vm, IReleaseSpec spec, in UInt256 transferValue, Address target)
        {
            IWorldState state = vm.WorldState;
            state.AddToBalanceAndCreateIfNotExists(target, transferValue, spec);
            EvmMetrics.IncrementEmptyCalls();

            vm.ReturnData = null!;
            return EvmExceptionType.None;
        }

        // Jump forward to be unpredicted by the branch predictor.
    StackUnderflow:
        return EvmExceptionType.StackUnderflow;
    OutOfGas:
        return EvmExceptionType.OutOfGas;
    }
}
