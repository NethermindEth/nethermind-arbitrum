// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Runtime.CompilerServices;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.Gas;
using Nethermind.Evm.State;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Evm;

/// <summary>
/// Interface for call-like opcodes (plugin-local version of internal EvmInstructions.IOpCall).
/// </summary>
internal interface IArbitrumOpCall
{
    static abstract bool IsStatic { get; }
    static abstract ExecutionType ExecutionType { get; }
}

internal struct ArbitrumOpCall : IArbitrumOpCall
{
    public static bool IsStatic => false;
    public static ExecutionType ExecutionType => ExecutionType.CALL;
}

internal struct ArbitrumOpCallCode : IArbitrumOpCall
{
    public static bool IsStatic => false;
    public static ExecutionType ExecutionType => ExecutionType.CALLCODE;
}

internal struct ArbitrumOpDelegateCall : IArbitrumOpCall
{
    public static bool IsStatic => false;
    public static ExecutionType ExecutionType => ExecutionType.DELEGATECALL;
}

internal struct ArbitrumOpStaticCall : IArbitrumOpCall
{
    public static bool IsStatic => true;
    public static ExecutionType ExecutionType => ExecutionType.STATICCALL;
}

/// <summary>
/// Custom CALL instruction handlers for Arbitrum with multi-dimensional gas tracking.
/// </summary>
internal static partial class ArbitrumEvmInstructions
{
    /// <summary>
    /// Executes a call-like operation with multi-dimensional gas tracking.
    /// Cold account access → StorageAccess, value transfer → Computation, new account → StorageGrowth.
    /// </summary>
    [SkipLocalsInit]
    internal static EvmExceptionType InstructionCall<TOpCall, TTracingInst>(
        VirtualMachine<MultiGasPolicy> vm,
        ref EvmStack stack,
        ref GasState gasState,
        ref int programCounter)
        where TOpCall : struct, IArbitrumOpCall
        where TTracingInst : struct, IFlag
    {
        Metrics.IncrementCalls();
        vm.ReturnData = null!;

        // Pop call parameters
        if (!stack.PopUInt256(out UInt256 gasLimit)) goto StackUnderflow;
        Address? codeSource = stack.PopAddress();
        if (codeSource is null) goto StackUnderflow;

        ref readonly ExecutionEnvironment env = ref vm.EvmState.Env;
        IReleaseSpec spec = vm.Spec;
        EvmState vmState = vm.EvmState;

        // Determine call value based on call type
        UInt256 callValue;
        if (typeof(TOpCall) == typeof(ArbitrumOpStaticCall))
            callValue = UInt256.Zero;
        else if (typeof(TOpCall) == typeof(ArbitrumOpDelegateCall))
            callValue = env.Value;
        else if (!stack.PopUInt256(out callValue))
            goto StackUnderflow;

        // Pop additional parameters
        if (!stack.PopUInt256(out UInt256 dataOffset) ||
            !stack.PopUInt256(out UInt256 dataLength) ||
            !stack.PopUInt256(out UInt256 outputOffset) ||
            !stack.PopUInt256(out UInt256 outputLength))
            goto StackUnderflow;

        // Determine instruction type
        Instruction instruction = TOpCall.ExecutionType switch
        {
            ExecutionType.CALL => Instruction.CALL,
            ExecutionType.CALLCODE => Instruction.CALLCODE,
            ExecutionType.DELEGATECALL => Instruction.DELEGATECALL,
            ExecutionType.STATICCALL => Instruction.STATICCALL,
            _ => Instruction.CALL
        };

        // Check cold/warm BEFORE warming up for multigas
        bool isCold = spec.UseHotAndColdStorage &&
                      !spec.IsPrecompile(codeSource) &&
                      vmState.AccessTracker.IsCold(codeSource);

        // Charge gas for account access with proper MultiGas
        if (!ChargeAccountAccessGasWithMultiGas(ref gasState, vm, codeSource, instruction, spec))
            goto OutOfGas;

        // Handle delegation if needed
        if (spec.UseHotAndColdStorage &&
            vm.TxExecutionContext.CodeInfoRepository.TryGetDelegation(codeSource, spec, out Address? delegated) &&
            delegated is not null)
        {
            if (!ChargeAccountAccessGasWithMultiGas(ref gasState, vm, delegated, instruction, spec))
                goto OutOfGas;
        }

        // Determine transfer value and target
        UInt256 transferValue = typeof(TOpCall) == typeof(ArbitrumOpDelegateCall) ? UInt256.Zero : callValue;

        // Static call violation check
        if (vmState.IsStatic && !transferValue.IsZero && typeof(TOpCall) != typeof(ArbitrumOpCallCode))
            return EvmExceptionType.StaticCallViolation;

        Address caller = typeof(TOpCall) == typeof(ArbitrumOpDelegateCall) ? env.Caller : env.ExecutingAccount;
        Address target = (typeof(TOpCall) == typeof(ArbitrumOpCall) || typeof(TOpCall) == typeof(ArbitrumOpStaticCall))
            ? codeSource
            : env.ExecutingAccount;

        // Calculate extra gas and corresponding MultiGas
        long gasExtra = 0L;
        MultiGas extraMultiGas = MultiGas.Zero;
        IWorldState state = vm.WorldState;

        // Value transfer gas → Computation
        if (!transferValue.IsZero)
        {
            gasExtra += GasCostOf.CallValue;
            extraMultiGas.SaturatingIncrementInto(ResourceKind.Computation, GasCostOf.CallValue);
        }

        // New account creation gas → StorageGrowth
        bool isNewAccount = false;
        if (!spec.ClearEmptyAccountWhenTouched && !state.AccountExists(target))
        {
            gasExtra += GasCostOf.NewAccount;
            isNewAccount = true;
        }
        else if (spec.ClearEmptyAccountWhenTouched && transferValue != 0 && state.IsDeadAccount(target))
        {
            gasExtra += GasCostOf.NewAccount;
            isNewAccount = true;
        }

        if (isNewAccount)
            extraMultiGas.SaturatingIncrementInto(ResourceKind.StorageGrowth, GasCostOf.NewAccount);

        // Add cold access MultiGas (delta from warm)
        MultiGas coldMultiGas = ArbitrumGas.GasAccountAccess(isCold);

        // Charge call cost → Computation
        MultiGasPolicy.ConsumeGas(ref gasState, spec.GetCallCost(), instruction);
        if (MultiGasPolicy.GetRemainingGas(in gasState) < 0)
            goto OutOfGas;

        // Update memory cost → Computation
        if (!EvmCalculations.UpdateMemoryCost<MultiGasPolicy>(vmState, ref gasState, in dataOffset, dataLength, instruction) ||
            !EvmCalculations.UpdateMemoryCost<MultiGasPolicy>(vmState, ref gasState, in outputOffset, outputLength, instruction))
            goto OutOfGas;

        // Charge extra gas with MultiGas (value transfer + new account)
        if (gasExtra > 0)
        {
            // Combine cold access MultiGas with extra MultiGas
            coldMultiGas.SaturatingAddInto(in extraMultiGas);
            MultiGasPolicy.ConsumeGasWithMultiGas(ref gasState, gasExtra, in coldMultiGas);
        }
        else if (!coldMultiGas.IsZero())
        {
            // Only cold access MultiGas (no extra gas)
            MultiGasPolicy.AddMultiGas(ref gasState, in coldMultiGas);
        }

        if (MultiGasPolicy.GetRemainingGas(in gasState) < 0)
            goto OutOfGas;

        // Get code info
        ICodeInfo codeInfo = vm.CodeInfoRepository.GetCachedCodeInfo(codeSource, spec);

        // EIP-7907: Large contract access
        if (spec.IsEip7907Enabled)
        {
            uint excessContractSize = (uint)System.Math.Max(0, codeInfo.CodeSpan.Length - CodeSizeConstants.MaxCodeSizeEip170);
            if (excessContractSize > 0 &&
                !ChargeForLargeContractAccess(excessContractSize, codeSource, in vmState.AccessTracker, ref gasState, instruction))
                goto OutOfGas;
        }

        // Get remaining gas for 63/64 rule
        long gasAvailable = MultiGasPolicy.GetRemainingGas(in gasState);

        // Apply 63/64 rule
        if (spec.Use63Over64Rule)
            gasLimit = UInt256.Min((UInt256)(gasAvailable - gasAvailable / 64), gasLimit);

        if (gasLimit >= long.MaxValue) goto OutOfGas;

        long gasLimitUl = (long)gasLimit;

        // Charge gas for the call
        MultiGasPolicy.ConsumeGas(ref gasState, gasLimitUl, instruction);
        if (MultiGasPolicy.GetRemainingGas(in gasState) < 0)
            goto OutOfGas;

        // Add call stipend if value is transferred
        if (!transferValue.IsZero)
        {
            if (vm.TxTracer.IsTracingRefunds)
                vm.TxTracer.ReportExtraGasPressure(GasCostOf.CallStipend);
            gasLimitUl += GasCostOf.CallStipend;
        }

        // Check call depth and balance
        if (env.CallDepth >= VirtualMachine<MultiGasPolicy>.MaxCallDepth ||
            (!transferValue.IsZero && state.GetBalance(env.ExecutingAccount) < transferValue))
        {
            vm.ReturnDataBuffer = Array.Empty<byte>();
            stack.PushZero<TTracingInst>();

            if (vm.TxTracer.IsTracingRefunds)
            {
                ReadOnlyMemory<byte>? memoryTrace = vmState.Memory.Inspect(in dataOffset, 32);
                vm.TxTracer.ReportMemoryChange(dataOffset, memoryTrace is null ? default : memoryTrace.Value.Span);
            }

            if (TTracingInst.IsActive)
            {
                long gasRemaining = MultiGasPolicy.GetRemainingGas(in gasState);
                vm.TxTracer.ReportOperationRemainingGas(gasRemaining);
                vm.TxTracer.ReportOperationError(EvmExceptionType.NotEnoughBalance);
            }

            MultiGasPolicy.RefundGas(ref gasState, gasLimitUl);
            if (TTracingInst.IsActive)
            {
                long gasRemaining = MultiGasPolicy.GetRemainingGas(in gasState);
                vm.TxTracer.ReportGasUpdateForVmTrace(gasLimitUl, gasRemaining);
            }
            return EvmExceptionType.None;
        }

        // Take snapshot and transfer value
        Snapshot snapshot = state.TakeSnapshot();
        state.SubtractFromBalance(caller, in transferValue, spec);

        // Fast-path for non-contract calls
        if (codeInfo.IsEmpty && !TTracingInst.IsActive && !vm.TxTracer.IsTracingActions)
        {
            vm.ReturnDataBuffer = default;
            stack.PushBytes<TTracingInst>(StatusCode.SuccessBytes.Span);
            MultiGasPolicy.RefundGas(ref gasState, gasLimitUl);
            state.AddToBalanceAndCreateIfNotExists(target, transferValue, spec);
            Metrics.IncrementEmptyCalls();
            vm.ReturnData = null!;
            return EvmExceptionType.None;
        }

        // Load call data and construct execution environment
        ReadOnlyMemory<byte> callData = vmState.Memory.Load(in dataOffset, dataLength);
        ExecutionEnvironment callEnv = new(
            codeInfo: codeInfo,
            executingAccount: target,
            caller: caller,
            codeSource: codeSource,
            callDepth: env.CallDepth + 1,
            transferValue: in transferValue,
            value: in callValue,
            inputData: in callData);

        if (outputLength == 0)
            outputOffset = 0;

        // Initialize child frame
        GasState childGasState = MultiGasPolicy.InitializeChildFrame(gasLimitUl);

        vm.ReturnData = EvmState.RentFrame(
            gasAvailable: gasLimitUl,
            outputDestination: outputOffset.ToLong(),
            outputLength: outputLength.ToLong(),
            executionType: TOpCall.ExecutionType,
            isStatic: TOpCall.IsStatic || vmState.IsStatic,
            isCreateOnPreExistingAccount: false,
            env: in callEnv,
            stateForAccessLists: in vmState.AccessTracker,
            in snapshot,
            frameGasState: childGasState);

        return EvmExceptionType.None;

    StackUnderflow:
        return EvmExceptionType.StackUnderflow;
    OutOfGas:
        return EvmExceptionType.OutOfGas;
    }

    /// <summary>
    /// Charge account access gas with proper MultiGas tracking.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ChargeAccountAccessGasWithMultiGas(
        ref GasState gasState,
        VirtualMachine<MultiGasPolicy> vm,
        Address address,
        Instruction instruction,
        IReleaseSpec spec)
    {
        if (!spec.UseHotAndColdStorage)
            return true;

        EvmState vmState = vm.EvmState;
        ref readonly StackAccessTracker accessTracker = ref vmState.AccessTracker;

        if (vm.TxTracer.IsTracingAccess)
            accessTracker.WarmUp(address);

        // Determine cold/warm BEFORE warming up
        bool isCold = !spec.IsPrecompile(address) && accessTracker.IsCold(address);

        // Warm up the account
        if (!spec.IsPrecompile(address))
            accessTracker.WarmUp(address);

        long gasCost;
        if (isCold)
            gasCost = GasCostOf.ColdAccountAccess;
        else
            gasCost = GasCostOf.WarmStateRead;

        // Compute MultiGas: cold delta → StorageAccess, warm → Computation (via ConsumeGas)
        if (isCold)
        {
            // Cold: charge warm portion to Computation, cold delta to StorageAccess
            MultiGas multiGas = ArbitrumGas.GasAccountAccess(true);
            MultiGasPolicy.ConsumeGasWithMultiGas(ref gasState, gasCost, in multiGas);
        }
        else
        {
            // Warm: all to Computation
            MultiGasPolicy.ConsumeGas(ref gasState, gasCost, instruction);
        }

        return MultiGasPolicy.GetRemainingGas(in gasState) >= 0;
    }

    /// <summary>
    /// Charge for large contract access.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ChargeForLargeContractAccess(
        uint excessContractSize,
        Address codeAddress,
        in StackAccessTracker accessTracker,
        ref GasState gasState,
        Instruction instruction)
    {
        if (accessTracker.WarmUpLargeContract(codeAddress))
        {
            long largeContractCost = GasCostOf.InitCodeWord * EvmCalculations.Div32Ceiling(excessContractSize, out bool outOfGas);
            if (outOfGas)
                return false;
            // Large contract access → Computation
            MultiGasPolicy.ConsumeGas(ref gasState, largeContractCost, instruction);
            return MultiGasPolicy.GetRemainingGas(in gasState) >= 0;
        }

        return true;
    }
}
