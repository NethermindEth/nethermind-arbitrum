// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Runtime.CompilerServices;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.EvmObjectFormat;
using Nethermind.Evm.Gas;
using Nethermind.Evm.State;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Evm;

/// <summary>
/// Interface for CREATE opcode types (plugin-local version).
/// </summary>
internal interface IArbitrumOpCreate
{
    static abstract ExecutionType ExecutionType { get; }
}

internal struct ArbitrumOpCreate : IArbitrumOpCreate
{
    public static ExecutionType ExecutionType => ExecutionType.CREATE;
}

internal struct ArbitrumOpCreate2 : IArbitrumOpCreate
{
    public static ExecutionType ExecutionType => ExecutionType.CREATE2;
}

/// <summary>
/// Custom CREATE instruction handlers for Arbitrum with multi-dimensional gas tracking.
/// </summary>
internal static partial class ArbitrumEvmInstructions
{
    private static readonly ReadOnlyMemory<byte> EmptyMemory = default;

    /// <summary>
    /// CREATE/CREATE2 instruction with multi-dimensional gas tracking.
    /// Account creation → StorageGrowth, init code word cost → Computation, CREATE2 hash cost → Computation.
    /// </summary>
    [SkipLocalsInit]
    internal static EvmExceptionType InstructionCreate<TOpCreate, TTracingInst>(
        VirtualMachine<MultiGasPolicy> vm,
        ref EvmStack stack,
        ref GasState gasState,
        ref int programCounter)
        where TOpCreate : struct, IArbitrumOpCreate
        where TTracingInst : struct, IFlag
    {
        Metrics.IncrementCreates();

        IReleaseSpec spec = vm.Spec;
        if (vm.EvmState.IsStatic)
            return EvmExceptionType.StaticCallViolation;

        vm.ReturnData = null!;
        ref readonly ExecutionEnvironment env = ref vm.EvmState.Env;
        IWorldState state = vm.WorldState;

        // Pop parameters off the stack
        if (!stack.PopUInt256(out UInt256 value) ||
            !stack.PopUInt256(out UInt256 memoryPositionOfInitCode) ||
            !stack.PopUInt256(out UInt256 initCodeLength))
            return EvmExceptionType.StackUnderflow;

        Span<byte> salt = default;
        if (typeof(TOpCreate) == typeof(ArbitrumOpCreate2))
            salt = stack.PopWord256();

        // EIP-3860: Limit the maximum size of the initialization code
        if (spec.IsEip3860Enabled && initCodeLength > spec.MaxInitCodeSize)
            return EvmExceptionType.OutOfGas;

        Instruction instruction = typeof(TOpCreate) == typeof(ArbitrumOpCreate2)
            ? Instruction.CREATE2
            : Instruction.CREATE;

        bool isCreate2 = typeof(TOpCreate) == typeof(ArbitrumOpCreate2);

        // Calculate scalar gas cost
        bool outOfGas = false;
        long gasCost = GasCostOf.Create +
                       (spec.IsEip3860Enabled ? GasCostOf.InitCodeWord * EvmCalculations.Div32Ceiling(in initCodeLength, out outOfGas) : 0) +
                       (isCreate2 ? GasCostOf.Sha3Word * EvmCalculations.Div32Ceiling(in initCodeLength, out outOfGas) : 0);

        if (outOfGas)
            return EvmExceptionType.OutOfGas;

        // Compute MultiGas with proper categorization
        MultiGas multiGas = ArbitrumGas.GasCreate(isCreate2, (int)initCodeLength, spec.IsEip3860Enabled);

        // Charge gas with MultiGas tracking
        MultiGasPolicy.ConsumeGasWithMultiGas(ref gasState, gasCost, in multiGas);
        if (MultiGasPolicy.GetRemainingGas(in gasState) < 0)
            return EvmExceptionType.OutOfGas;

        // Update memory cost → Computation
        if (!EvmCalculations.UpdateMemoryCost<MultiGasPolicy>(vm.EvmState, ref gasState, in memoryPositionOfInitCode,
                in initCodeLength, instruction))
            return EvmExceptionType.OutOfGas;

        // Verify call depth
        if (env.CallDepth >= VirtualMachine<MultiGasPolicy>.MaxCallDepth)
        {
            vm.ReturnDataBuffer = Array.Empty<byte>();
            stack.PushZero<TTracingInst>();
            return EvmExceptionType.None;
        }

        // Load the initialization code from memory
        ReadOnlyMemory<byte> initCode = vm.EvmState.Memory.Load(in memoryPositionOfInitCode, in initCodeLength);

        // Check balance
        UInt256 balance = state.GetBalance(env.ExecutingAccount);
        if (value > balance)
        {
            vm.ReturnDataBuffer = Array.Empty<byte>();
            stack.PushZero<TTracingInst>();
            return EvmExceptionType.None;
        }

        // Check nonce
        UInt256 accountNonce = state.GetNonce(env.ExecutingAccount);
        if (accountNonce >= ulong.MaxValue)
        {
            vm.ReturnDataBuffer = Array.Empty<byte>();
            stack.PushZero<TTracingInst>();
            return EvmExceptionType.None;
        }

        // Get remaining gas for the create operation
        long gasAvailable = MultiGasPolicy.GetRemainingGas(in gasState);

        // Apply 63/64 rule
        long callGas = spec.Use63Over64Rule ? gasAvailable - gasAvailable / 64L : gasAvailable;

        MultiGasPolicy.ConsumeGas(ref gasState, callGas, instruction);
        if (MultiGasPolicy.GetRemainingGas(in gasState) < 0)
            return EvmExceptionType.OutOfGas;

        // Compute contract address
        Address contractAddress = typeof(TOpCreate) == typeof(ArbitrumOpCreate)
            ? ContractAddress.From(env.ExecutingAccount, state.GetNonce(env.ExecutingAccount))
            : ContractAddress.From(env.ExecutingAccount, salt, initCode.Span);

        // EIP-2929: pre-warm contract address
        if (spec.UseHotAndColdStorage)
            vm.EvmState.AccessTracker.WarmUp(contractAddress);

        // EOF check
        if (spec.IsEofEnabled && initCode.Span.StartsWith(EofValidator.MAGIC))
        {
            vm.ReturnDataBuffer = Array.Empty<byte>();
            stack.PushZero<TTracingInst>();
            MultiGasPolicy.RefundGas(ref gasState, callGas);
            return EvmExceptionType.None;
        }

        // Increment nonce
        state.IncrementNonce(env.ExecutingAccount);

        // Analyze and compile the initialization code
        CodeInfoFactory.CreateInitCodeInfo(initCode.ToArray(), spec, out ICodeInfo? codeInfo, out _);
        if (codeInfo is null)
        {
            vm.ReturnDataBuffer = Array.Empty<byte>();
            stack.PushZero<TTracingInst>();
            return EvmExceptionType.None;
        }

        // Take a snapshot
        Snapshot snapshot = state.TakeSnapshot();

        // Check for contract address collision
        bool accountExists = state.AccountExists(contractAddress);
        if (accountExists && contractAddress.IsNonZeroAccount(spec, vm.CodeInfoRepository, state))
        {
            vm.ReturnDataBuffer = Array.Empty<byte>();
            stack.PushZero<TTracingInst>();
            return EvmExceptionType.None;
        }

        // Deduct the transfer value
        state.SubtractFromBalance(env.ExecutingAccount, value, spec);

        // Construct a new execution environment
        ExecutionEnvironment callEnv = new(
            codeInfo: codeInfo,
            executingAccount: contractAddress,
            caller: env.ExecutingAccount,
            codeSource: null,
            callDepth: env.CallDepth + 1,
            transferValue: in value,
            value: in value,
            inputData: in EmptyMemory);

        // Initialize child frame
        GasState childGasState = MultiGasPolicy.InitializeChildFrame(callGas);

        // Rent a new frame
        vm.ReturnData = EvmState.RentFrame(
            gasAvailable: callGas,
            outputDestination: 0,
            outputLength: 0,
            executionType: TOpCreate.ExecutionType,
            isStatic: vm.EvmState.IsStatic,
            isCreateOnPreExistingAccount: accountExists,
            env: in callEnv,
            stateForAccessLists: in vm.EvmState.AccessTracker,
            in snapshot,
            frameGasState: childGasState);

        return EvmExceptionType.None;
    }
}
