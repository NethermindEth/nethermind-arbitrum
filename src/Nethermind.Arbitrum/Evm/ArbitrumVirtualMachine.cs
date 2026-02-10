// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Runtime.CompilerServices;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.GasPolicy;
using Nethermind.Evm.State;
using Nethermind.Logging;
using Nethermind.Evm.Tracing;
using Nethermind.Int256;
using PrecompileInfo = Nethermind.Arbitrum.Precompiles.PrecompileInfo;
using Nethermind.Arbitrum.Arbos.Storage;
using static Nethermind.Arbitrum.Precompiles.Exceptions.ArbitrumPrecompileException;
using static Nethermind.Evm.VirtualMachineStatics;
using System.Text.Json;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Math;

[assembly: InternalsVisibleTo("Nethermind.Arbitrum.Evm.Test")]
namespace Nethermind.Arbitrum.Evm;

using unsafe OpCode = delegate*<VirtualMachine<ArbitrumGasPolicy>, ref EvmStack, ref ArbitrumGasPolicy, ref int, EvmExceptionType>;

public sealed unsafe class ArbitrumVirtualMachine(
    IArbitrumSpecHelper specHelper,
    IBlockhashProvider? blockHashProvider,
    IWasmStore wasmStore,
    ISpecProvider? specProvider,
    ILogManager? logManager,
    IL1BlockCache? l1BlockCache = null
) : VirtualMachine<ArbitrumGasPolicy>(blockHashProvider, specProvider, logManager), IStylusVmHost
{
    public IWasmStore WasmStore => wasmStore;
    public ArbosState FreeArbosState { get; private set; } = null!;
    public ulong CurrentArbosVersion => FreeArbosState.CurrentArbosVersion;
    public ArbitrumTxExecutionContext ArbitrumTxExecutionContext { get; set; } = new();
    public IL1BlockCache L1BlockCache { get; } = l1BlockCache ?? new L1BlockCache();
    private Dictionary<Address, uint> Programs { get; } = new();
    private SystemBurner _systemBurner = null!;
    private BurnerHolder _precompileBurnerHolder = null!;
    private ArbosState _precompileArbosState = null!;
    private static readonly PrecompileExecutionFailureException PrecompileExecutionFailureException = new();
    private static readonly OutOfGasException PrecompileOutOfGasException = new();

    internal static readonly byte[] BytesZero32 =
    {
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0
    };

    public override TransactionSubstate ExecuteTransaction<TTracingInst>(
        VmState<ArbitrumGasPolicy> vmState,
        IWorldState worldState,
        ITxTracer txTracer)
    {
        wasmStore.ResetPages();

        if (FreeArbosState is null)
        {
            _systemBurner = new SystemBurner();
            FreeArbosState = ArbosState.OpenArbosState(worldState, _systemBurner, Logger);

            _precompileBurnerHolder = new BurnerHolder(_systemBurner);
            _precompileArbosState = ArbosState.OpenArbosState(worldState, _precompileBurnerHolder, Logger);
        }
        else
        {
            _systemBurner.ResetBurned();
        }

        TransactionSubstate result = base.ExecuteTransaction<TTracingInst>(vmState, worldState, txTracer);

        // Capture accumulated MultiGas for receipt
        ArbitrumTxExecutionContext.AccumulatedMultiGas = vmState.Gas.GetAccumulated();

        return result;
    }

    public StylusEvmResult StylusCall(ExecutionType kind, Address to, ReadOnlyMemory<byte> input, ulong gasLeftReportedByRust, ulong gasRequestedByRust, in UInt256 value)
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong((long)gasLeftReportedByRust);

        // Charge gas for accessing the account's code. Stylus doesn't charge for EIP-7702 delegation.
        if (!ArbitrumGasPolicy.ConsumeAccountAccessGas(ref gas, Spec, in VmState.AccessTracker, TxTracer.IsTracingAccess, to))
            goto OutOfGas;

        ExecutionEnvironment env = VmState.Env;

        // Determine the call value based on the call type.
        UInt256 callValue;
        if (kind == ExecutionType.STATICCALL)
        {
            // Static calls cannot transfer value.
            callValue = UInt256.Zero;
        }
        else if (kind == ExecutionType.DELEGATECALL)
        {
            // Delegate calls use the value from the current execution context.
            callValue = env.Value;
        }
        else
        {
            callValue = value;
        }

        // For non-delegate calls, the transfer value is the call value.
        UInt256 transferValue = kind == ExecutionType.DELEGATECALL ? UInt256.Zero : callValue;

        // Enforce static call restrictions: no value transfer allowed unless it's a CALLCODE.
        if (VmState.IsStatic && !transferValue.IsZero)
            return new StylusEvmResult([], 0, EvmExceptionType.StaticCallViolation);

        // Determine caller and target based on the call type.
        Address caller = kind == ExecutionType.DELEGATECALL ? env.Caller : env.ExecutingAccount;
        Address target = kind is ExecutionType.CALL or ExecutionType.STATICCALL
            ? to
            : env.ExecutingAccount;

        long gasExtra = 0L;

        // Add extra gas cost if value is transferred.
        if (!transferValue.IsZero)
            gasExtra += GasCostOf.CallValue;

        // Charge additional gas if the target account is new or considered empty.
        if (!Spec.ClearEmptyAccountWhenTouched && !WorldState.AccountExists(target))
        {
            gasExtra += GasCostOf.NewAccount;
        }
        else if (Spec.ClearEmptyAccountWhenTouched && transferValue != 0 && WorldState.IsDeadAccount(target))
        {
            gasExtra += GasCostOf.NewAccount;
        }

        if (!ArbitrumGasPolicy.UpdateGas(ref gas, gasExtra))
            goto OutOfGas;

        long gasAvailable = ArbitrumGasPolicy.GetRemainingGas(in gas);
        ulong baseCost = gasLeftReportedByRust - (ulong)gasAvailable;

        UInt256 gasLimit = UInt256.Min((UInt256)(gasAvailable * 63 / 64), gasRequestedByRust);

        // If gasLimit exceeds the host's representable range, treat as out-of-gas.
        if (gasLimit >= long.MaxValue)
            goto OutOfGas;

        long gasLimitUl = (long)gasLimit;
        if (!ArbitrumGasPolicy.UpdateGas(ref gas, gasLimitUl))
            goto OutOfGas;

        // Add call stipend if value is being transferred.
        if (!transferValue.IsZero)
            gasLimitUl += GasCostOf.CallStipend;

        if (env.CallDepth >= MaxCallDepth)
        {
            ReturnDataBuffer = Array.Empty<byte>();
            return new StylusEvmResult([], baseCost, EvmExceptionType.Other);
        }

        if (!transferValue.IsZero && WorldState.GetBalance(env.ExecutingAccount) < transferValue)
        {
            ReturnDataBuffer = Array.Empty<byte>();
            return new StylusEvmResult([], baseCost, EvmExceptionType.NotEnoughBalance);
        }

        // Take a snapshot of the state for potential rollback.
        Snapshot snapshot = WorldState.TakeSnapshot();
        // Subtract the transfer value from the caller's balance.
        WorldState.SubtractFromBalance(caller, in transferValue, Spec);

        // Retrieve code information for the call and schedule background analysis if needed.
        CodeInfo codeInfo = CodeInfoRepository.GetCachedCodeInfo(to, Spec);

        ReadOnlyMemory<byte> callData = input;

        // Construct the execution environment for the call.
        ExecutionEnvironment callEnv = ExecutionEnvironment.Rent(
            codeInfo: codeInfo,
            executingAccount: target,
            caller: caller,
            codeSource: to,
            callDepth: env.CallDepth + 1,
            transferValue: in transferValue,
            value: in callValue,
            inputData: in callData);

        // Rent a new call frame for executing the call.
        VmState<ArbitrumGasPolicy> returnData = VmState<ArbitrumGasPolicy>.RentFrame(
            gas: ArbitrumGasPolicy.FromLong(gasLimitUl),
            outputDestination: 0,
            outputLength: 0,
            executionType: kind,
            isStatic: kind == ExecutionType.STATICCALL || VmState.IsStatic,
            isCreateOnPreExistingAccount: false,
            env: callEnv,
            stateForAccessLists: in VmState.AccessTracker,
            snapshot: in snapshot,
            isTopLevel: true);

        ReturnData = returnData;
        CallResult callResult = new(returnData);
        TransactionSubstate txnSubstrate = ExecuteStylusEvmCallback(callResult);
        ulong gasLeftAfterExecution = (ulong)ArbitrumGasPolicy.GetRemainingGas(returnData.Gas);
        ulong gasCost = ((ulong)gasLimitUl).SaturateSub(gasLeftAfterExecution).SaturateAdd(baseCost);

        EvmExceptionType exceptionType = txnSubstrate.ShouldRevert
            ? EvmExceptionType.Revert
            : txnSubstrate.EvmExceptionType;

        return new StylusEvmResult(txnSubstrate.Output.Bytes.ToArray(), gasCost, exceptionType);
    OutOfGas:
        return new StylusEvmResult([], gasLeftReportedByRust, EvmExceptionType.OutOfGas);
    }

    public StylusEvmResult StylusCreate(ReadOnlyMemory<byte> initCode, in UInt256 endowment, UInt256? salt, ulong gasLimit)
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong((long)gasLimit);

        if (VmState.IsStatic)
            goto StaticCallViolation;

        // Reset the return data buffer as contract creation does not use previous return data.
        ReturnData = null!;
        ExecutionEnvironment env = VmState.Env;
        IWorldState state = WorldState;

        // Ensure the executing account exists in the world state. If not, create it with a zero balance.
        if (!state.AccountExists(env.ExecutingAccount))
            state.CreateAccount(env.ExecutingAccount, UInt256.Zero);


        UInt256 value = endowment;

        ExecutionType kind = ExecutionType.CREATE;
        if (salt != null)
            kind = ExecutionType.CREATE2;

        UInt256 initCodeLength = new((uint)initCode.Length);

        // EIP-3860: Limit the maximum size of the initialization code.
        if (Spec.IsEip3860Enabled)
        {
            if (initCodeLength > Spec.MaxInitCodeSize)
                goto OutOfGas;
        }

        bool outOfGas = false;
        // Calculate the gas cost for the creation, including fixed cost and per-word cost for init code.
        // Also include an extra cost for CREATE2 if applicable.
        long gasCost = GasCostOf.Create +
                       (Spec.IsEip3860Enabled ? GasCostOf.InitCodeWord * EvmCalculations.Div32Ceiling(in initCodeLength, out outOfGas) : 0) +
                       (kind == ExecutionType.CREATE2
                           ? GasCostOf.Sha3Word * EvmCalculations.Div32Ceiling(in initCodeLength, out outOfGas)
                           : 0);

        // Check gas sufficiency: if outOfGas flag was set during gas division or if gas update fails.
        if (outOfGas || !ArbitrumGasPolicy.UpdateGas(ref gas, gasCost))
            goto OutOfGas;

        long gasAvailable = ArbitrumGasPolicy.GetRemainingGas(in gas);

        // Verify call depth does not exceed the maximum allowed. If exceeded, return early with empty data.
        // This guard ensures we do not create nested contract calls beyond EVM limits.
        if (env.CallDepth >= MaxCallDepth)
        {
            ReturnDataBuffer = Array.Empty<byte>();
            return new StylusEvmResult([], (ulong)gasAvailable, EvmExceptionType.None, Address.Zero);
        }

        // Check that the executing account has sufficient balance to transfer the specified value.
        UInt256 balance = state.GetBalance(env.ExecutingAccount);
        if (value > balance)
        {
            ReturnDataBuffer = Array.Empty<byte>();
            return new StylusEvmResult([], (ulong)(gasAvailable), EvmExceptionType.None, Address.Zero);
        }

        // Retrieve the nonce of the executing account to ensure it hasn't reached the maximum.
        UInt256 accountNonce = state.GetNonce(env.ExecutingAccount);
        UInt256 maxNonce = ulong.MaxValue;
        if (accountNonce >= maxNonce)
        {
            ReturnDataBuffer = Array.Empty<byte>();
            return new StylusEvmResult([], (ulong)(gasAvailable), EvmExceptionType.None, Address.Zero);
        }

        // Calculate gas available for the contract creation call.
        // Use the 63/64 gas rule if specified in the current EVM specification.
        long callGas = Spec.Use63Over64Rule ? gasAvailable - gasAvailable / 64L : gasAvailable;
        if (!ArbitrumGasPolicy.UpdateGas(ref gas, callGas))
            goto OutOfGas;

        // Compute the contract address:
        // - For CREATE: based on the executing account and its current nonce.
        // - For CREATE2: based on the executing account, the provided salt, and the init code.
        Address contractAddress = kind == ExecutionType.CREATE
            ? ContractAddress.From(env.ExecutingAccount, state.GetNonce(env.ExecutingAccount))
            : ContractAddress.From(env.ExecutingAccount, salt!.Value.ToBigEndian(), initCode.Span);

        // For EIP-2929 support, pre-warm the contract address in the access tracker to account for hot/cold storage costs.
        if (Spec.UseHotAndColdStorage)
        {
            VmState.AccessTracker.WarmUp(contractAddress);
        }

        // Increment the nonce of the executing account to reflect the contract creation.
        state.IncrementNonce(env.ExecutingAccount);

        // Analyze and compile the initialization code.
        CodeInfoFactory.CreateInitCodeInfo(initCode, Spec, out CodeInfo? codeinfo, out _);

        // Take a snapshot of the current state. This allows the state to be reverted if contract creation fails.
        Snapshot snapshot = state.TakeSnapshot();

        // Check for contract address collision. If the contract already exists and contains code or non-zero state,
        // then the creation should be aborted.
        bool accountExists = state.AccountExists(contractAddress);
        if (accountExists && contractAddress.IsNonZeroAccount(Spec, CodeInfoRepository, state))
        {
            ReturnDataBuffer = Array.Empty<byte>();
            return new StylusEvmResult([], (ulong)(gasAvailable), EvmExceptionType.None, contractAddress);
        }

        // If the contract address refers to a dead account, clear its storage before creation.
        if (state.IsDeadAccount(contractAddress))
        {
            state.ClearStorage(contractAddress);
        }

        // Deduct the transfer value from the executing account's balance.
        state.SubtractFromBalance(env.ExecutingAccount, value, Spec);

        // Construct a new execution environment for the contract creation call.
        // This environment sets up the call frame for executing the contract's initialization code.
        ExecutionEnvironment callEnv = ExecutionEnvironment.Rent(
            codeInfo: codeinfo ?? throw new InvalidOperationException(),
            executingAccount: contractAddress,
            caller: env.ExecutingAccount,
            codeSource: null,
            callDepth: env.CallDepth + 1,
            transferValue: in value,
            value: in value,
            inputData: default);

        // Rent a new frame to run the initialization code in the new execution environment.
        VmState<ArbitrumGasPolicy> returnData = VmState<ArbitrumGasPolicy>.RentFrame(
            gas: ArbitrumGasPolicy.FromLong(callGas),
            outputDestination: 0,
            outputLength: 0,
            executionType: kind,
            isStatic: VmState.IsStatic,
            isCreateOnPreExistingAccount: accountExists,
            env: callEnv,
            stateForAccessLists: in VmState.AccessTracker,
            snapshot: in snapshot,
            isTopLevel: true);

        ReturnData = returnData;
        CallResult callResult = new(returnData);
        TransactionSubstate txnSubstrate = ExecuteStylusEvmCallback(callResult);

        // Gas consumed by the callback execution (not including gasCost which was already charged by UpdateGas)
        // The 1/64 reserved gas is returned to the caller, matching Nitro's behavior
        long one64th = gasAvailable / 64;
        ulong gasConsumed = (ulong)(gasAvailable - ArbitrumGasPolicy.GetRemainingGas(returnData.Gas) - one64th);

        if (txnSubstrate.EvmExceptionType == EvmExceptionType.OutOfGas)
        {
            gasConsumed = (ulong)(gasAvailable - one64th);
        }

        if (txnSubstrate.EvmExceptionType == EvmExceptionType.None && !txnSubstrate.ShouldRevert)
        {
            ReadOnlyMemory<byte> deployedCode = txnSubstrate.Output.Bytes;
            long codeDepositGasCost = CodeDepositHandler.CalculateCost(Spec, deployedCode.Length);
            bool invalidCode = !CodeDepositHandler.IsValidWithLegacyRules(Spec, deployedCode);

            long gasRemainingForCodeDeposit = ArbitrumGasPolicy.GetRemainingGas(returnData.Gas);

            if (gasRemainingForCodeDeposit >= codeDepositGasCost && !invalidCode)
            {
                CodeInfoRepository.InsertCode(deployedCode, contractAddress, Spec);
                gasConsumed += (ulong)codeDepositGasCost;
            }
            else if (Spec.FailOnOutOfGasCodeDeposit || invalidCode)
            {
                WorldState.Restore(snapshot);
                if (!accountExists)
                {
                    WorldState.DeleteAccount(contractAddress);
                }
                gasConsumed = (ulong)gasCost + (ulong)callGas;
                return new StylusEvmResult([], gasConsumed, EvmExceptionType.OutOfGas, Address.Zero);
            }
        }

        // Return gasCost - eip3860Cost + gasConsumed as total. The Stylus program passed gasLimit and needs to know
        // the total gas spent. Note: Include base CREATE cost (32000) and CREATE2 sha3 word cost if applicable,
        // but NOT EIP-3860 init code word cost (which is handled separately in the Stylus runtime).
        long eip3860Cost = Spec.IsEip3860Enabled
            ? GasCostOf.InitCodeWord * EvmCalculations.Div32Ceiling(in initCodeLength, out _)
            : 0;
        return new StylusEvmResult([], (ulong)(gasCost - eip3860Cost) + gasConsumed, txnSubstrate.EvmExceptionType, contractAddress);
    OutOfGas:
        return new StylusEvmResult([], gasLimit, EvmExceptionType.OutOfGas, Address.Zero);
    StaticCallViolation:
        return new StylusEvmResult([], (ulong)ArbitrumGasPolicy.GetRemainingGas(in gas), EvmExceptionType.StaticCallViolation, Address.Zero);
    }

    protected override CallResult RunByteCode<TTracingInst, TCancelable>(scoped ref EvmStack stack, scoped ref ArbitrumGasPolicy gas)
    {
        if (StylusCode.IsStylusProgram(VmState.Env.CodeInfo.CodeSpan))
            return RunWasmCode(ref gas);

        // Set the tracer on the gas struct for gas dimension capture.
        // The tracer is used by ArbitrumGasPolicy hooks (OnBeforeInstructionTrace/OnAfterInstructionTrace)
        // called from the base RunByteCode loop.
        IArbitrumTxTracer? arbTracer = TxTracer.GetTracer<IArbitrumTxTracer>();
        ArbitrumGasPolicy.SetTracer(ref gas, arbTracer);

        return base.RunByteCode<TTracingInst, TCancelable>(ref stack, ref gas);
    }

    protected override OpCode[] GenerateOpCodes<TTracingInst>(IReleaseSpec spec)
    {
        OpCode[] opcodes = (OpCode[])base.GenerateOpCodes<TTracingInst>(spec);
        opcodes[(int)Instruction.GASPRICE] = &ArbitrumEvmInstructions.InstructionBlkUInt256<TTracingInst>;
        opcodes[(int)Instruction.NUMBER] = &ArbitrumEvmInstructions.InstructionBlkUInt64<TTracingInst>;
        opcodes[(int)Instruction.BLOCKHASH] = &ArbitrumEvmInstructions.InstructionBlockHash<TTracingInst>;
        return opcodes;
    }

    protected override CallResult ExecutePrecompile(VmState<ArbitrumGasPolicy> currentState, bool isTracingActions, out Exception? failure, out string? substateError)
    {
        // If precompile is not an arbitrum specific precompile but a standard one
        if (currentState.Env.CodeInfo is not PrecompileInfo precompileInfo)
            return base.ExecutePrecompile(currentState, isTracingActions, out failure, out substateError);

        // Report the precompile action if tracing is enabled.
        if (isTracingActions)
        {
            _txTracer.ReportAction(
                ArbitrumGasPolicy.GetRemainingGas(currentState.Gas),
                currentState.Env.Value,
                currentState.From,
                currentState.To,
                currentState.Env.InputData,
                currentState.ExecutionType,
                true);
        }

        // Execute the precompile operation with the current state.
        CallResult callResult = RunPrecompile(currentState, precompileInfo);

        // If the precompile did not succeed without a revert, handle the failure conditions.
        if (!callResult.PrecompileSuccess!.Value && !callResult.ShouldRevert)
        {
            substateError = callResult.SubstateError;
            // Set a general execution failure exception except for OutOfGas
            failure = callResult.ExceptionType == EvmExceptionType.OutOfGas ? PrecompileOutOfGasException : PrecompileExecutionFailureException;

            // No need to reset currentState.Gas to 0 because:
            // - if top-level: state.Gas just gets ignored in Refund().
            // - if nested call: HandleFailure() ends up resetting the whole state to the previous call frame's state without any refund.

            // Return the default CallResult to signal failure, with the failure exception set via the out parameter.
            return default;
        }

        // If execution reaches here, the precompile operation is either successful, or gracefully reverts.
        failure = null;
        substateError = null;
        return callResult;
    }

    private CallResult RunPrecompile(VmState<ArbitrumGasPolicy> state, PrecompileInfo precompileInfo)
    {
        WorldState.AddToBalanceAndCreateIfNotExists(state.Env.ExecutingAccount, state.Env.Value, Spec);

        IArbitrumPrecompile precompile = precompileInfo.ArbitrumPrecompile;

        TracingInfo tracingInfo = new(
            TxTracer as IArbitrumTxTracer ?? ArbNullTxTracer.Instance,
            TracingScenario.TracingDuringEvm,
            state.Env
        );

        // Geth EVM has depth started from 1, Nethermind has depth starting from 0
        Address? grandCaller = state.Env.CallDepth > 0 ? StateStack.ElementAt(state.Env.CallDepth - 1).From : null;

        ArbitrumPrecompileExecutionContext context = new(
            state.Env.Caller, state.Env.Value, GasSupplied: (ulong)ArbitrumGasPolicy.GetRemainingGas(state.Gas), WorldState, WasmStore,
            BlockExecutionContext, ChainId.ToByteArray().ToULongFromBigEndianByteArrayWithoutLeadingZeros(),
            tracingInfo, Spec
        )
        {
            IsCallStatic = state.IsStatic,
            BlockHashProvider = BlockHashProvider,
            CallDepth = state.Env.CallDepth,
            GrandCaller = grandCaller,
            Origin = TxExecutionContext.Origin,
            TopLevelTxType = ArbitrumTxExecutionContext.TopLevelTxType,
            FreeArbosState = FreeArbosState,
            CurrentRetryable = ArbitrumTxExecutionContext.CurrentRetryable,
            CurrentRefundTo = ArbitrumTxExecutionContext.CurrentRefundTo,
            PosterFee = ArbitrumTxExecutionContext.PosterFee,
            ExecutingAccount = state.Env.ExecutingAccount,
        };

        return precompile.IsDebug
            ? DebugPrecompileCall(state, context, precompile)
            : precompile.IsOwner
                ? OwnerPrecompileCall(state, context, precompile)
                : NonOwnerPrecompileCall(state, context, precompile);
    }

    private CallResult DebugPrecompileCall(VmState<ArbitrumGasPolicy> state, ArbitrumPrecompileExecutionContext context, IArbitrumPrecompile precompile)
    {
        if (IsDebugMode())
            return NonOwnerPrecompileCall(state, context, precompile);

        if (Logger.IsWarn)
            Logger.Warn($"Debug precompiles are disabled for this chain");

        ConsumeAllGas(state); // Consumes all gas, and anyway call fails (not a revert), so, no refund
        return new(output: default, precompileSuccess: false, fromVersion: 0, shouldRevert: false, exceptionType: EvmExceptionType.PrecompileFailure)
        {
            SubstateError = "Debug precompiles are disabled for this chain"
        };
    }

    private CallResult OwnerPrecompileCall(VmState<ArbitrumGasPolicy> state, ArbitrumPrecompileExecutionContext context, IArbitrumPrecompile precompile)
    {
        ulong before = _systemBurner.Burned;
        bool isSenderAChainOwner = FreeArbosState.ChainOwners.IsMember(context.Caller);
        // We also simulate burning opening arbos (1 storage read) but we reuse the existing one for performances
        ulong gasUsed = _systemBurner.Burned + ArbosStorage.StorageReadCost - before;

        if (gasUsed > context.GasLeft)
        {
            ConsumeAllGas(state); // Does not matter as call fails (not a revert), no refund anyway
            return new(output: default, precompileSuccess: false, fromVersion: 0, shouldRevert: false, exceptionType: EvmExceptionType.OutOfGas)
            {
                SubstateError = "Out of gas checking chain owner status"
            };
        }

        if (!isSenderAChainOwner)
        {
            context.Burn(gasUsed); // non-owner has to pay for opening arbos + the IsMember operation

            if (Logger.IsTrace)
                Logger.Trace($"Unauthorized caller {context.Caller} attempted to access owner-only precompile {precompile.GetType().Name}");

            ReturnSomeGas(state, context.GasLeft); // Does not matter as call fails (not a revert), no refund anyway
            return new(output: default, precompileSuccess: false, fromVersion: 0, shouldRevert: false, exceptionType: EvmExceptionType.PrecompileFailure)
            {
                SubstateError = $"Caller {context.Caller} is not a chain owner"
            };
        }

        CallResult result = NonOwnerPrecompileCall(state, context, precompile);

        ReturnSomeGas(state, context.GasSupplied);
        if (Logger.IsTrace)
            Logger.Trace($"Resetting gas left to gas supplied as in owner precompile, gas left: {ArbitrumGasPolicy.GetRemainingGas(state.Gas)}");

        if (!result.PrecompileSuccess!.Value)
            return result;

        if (!context.IsCallStatic || context.ArbosState.CurrentArbosVersion < ArbosVersion.Eleven)
            OwnerLogic.EmitOwnerSuccessEvent(state, context, precompile);

        return result;
    }

    private CallResult NonOwnerPrecompileCall(VmState<ArbitrumGasPolicy> state, ArbitrumPrecompileExecutionContext context, IArbitrumPrecompile precompile)
    {
        try
        {
            return ExecutePrecompileWithPreChecks(state, context, precompile);
        }
        catch (DllNotFoundException exception)
        {
            if (Logger.IsError)
                Logger.Error($"Failed to load one of the dependencies of {precompile.GetType()} precompile", exception);
            throw;
        }
        catch (Exception exception)
        {
            return HandlePrecompileException(state, context, exception);
        }
    }

    private CallResult ExecutePrecompileWithPreChecks(VmState<ArbitrumGasPolicy> state, ArbitrumPrecompileExecutionContext context, IArbitrumPrecompile precompile)
    {
        ReadOnlySpan<byte> calldata = state.Env.InputData.Span;

        bool shouldRevert = true;
        ReadOnlySpan<byte> copyCalldata = calldata;

        // Revert if calldata does not contain method ID to be called or if method visibility does not match call parameters
        if (calldata.Length < 4 || !PrecompileHelper.TryCheckMethodVisibility(precompile, context, Logger, ref calldata, out shouldRevert, out PrecompileHandler? methodToExecute))
        {
            ReturnSomeGas(state, shouldRevert ? 0 : context.GasSupplied);
            EvmExceptionType exceptionType = shouldRevert ? EvmExceptionType.Revert : EvmExceptionType.None;

            string errorMsg = copyCalldata.Length < 4
                ? $"Calldata too short: {copyCalldata.Length} bytes (minimum 4 bytes required for method ID), calldata: {copyCalldata.ToHexString()}"
                : $"Method not found or visibility check failed, calldata: {copyCalldata.ToHexString()}";

            return new(output: default, precompileSuccess: !shouldRevert, fromVersion: 0, shouldRevert, exceptionType)
            {
                SubstateError = shouldRevert ? errorMsg : null
            };
        }

        // Burn gas for argument data supplied (excluding method id)
        ulong dataGasCost = GasCostOf.DataCopy * Math.Utils.Div32Ceiling((ulong)calldata.Length);
        // Revert if user cannot afford the argument data supplied
        if (dataGasCost > context.GasLeft)
        {
            ConsumeAllGas(state);
            return new(output: default, precompileSuccess: false, fromVersion: 0, shouldRevert: true, exceptionType: EvmExceptionType.Revert)
            {
                SubstateError = "Insufficient gas for calldata"
            };
        }
        context.Burn(dataGasCost);

        // Impure methods may need the ArbOS state, so open & update the call context now
        if (!context.IsMethodCalledPure)
        {
            // If user cannot afford opening arbos state, do not revert and fail instead
            if (ArbosStorage.StorageReadCost > context.GasLeft)
            {
                ConsumeAllGas(state);
                return new(output: default, precompileSuccess: false, fromVersion: 0, shouldRevert: false, exceptionType: EvmExceptionType.OutOfGas)
                {
                    SubstateError = "Out of gas opening ArbOS state"
                };
            }
            // Charge gas for the version read that would have happened in OpenArbosState
            context.Burn(ArbosStorage.StorageReadCost);
            // Reuse cached ArbosState, swapping the burner to this precompile's gas context
            _precompileBurnerHolder.Current = context;
            context.ArbosState = _precompileArbosState;
        }

        byte[] output = methodToExecute(context, calldata);

        // Add logs to evm state
        foreach (LogEntry log in context.EventLogs)
            state.AccessTracker.Logs.Add(log);

        // Burn gas for output data
        (shouldRevert, ulong gasToReturn, _) = PayForOutput(context, output, success: true);
        ReturnSomeGas(state, gasToReturn);

        return new(
            output: shouldRevert ? default : output,
            precompileSuccess: !shouldRevert,
            fromVersion: 0,
            shouldRevert,
            exceptionType: shouldRevert ? EvmExceptionType.Revert : EvmExceptionType.None
        )
        {
            SubstateError = shouldRevert ? "Insufficient gas for output data" : null
        };
    }

    private static PrecompileOutcome PayForOutput(ArbitrumPrecompileExecutionContext context, byte[] executionOutput, bool success)
    {
        ulong outputGasCost = GasCostOf.DataCopy * Math.Utils.Div32Ceiling((ulong)executionOutput.Length);

        // user cannot afford the result data returned
        if (outputGasCost > context.GasLeft)
            return new(ShouldRevert: true, GasLeft: 0L, RanOutOfGas: true);

        context.Burn(outputGasCost);
        return new(ShouldRevert: !success, GasLeft: context.GasLeft, RanOutOfGas: false);
    }

    private CallResult HandlePrecompileException(
        VmState<ArbitrumGasPolicy> state,
        ArbitrumPrecompileExecutionContext context,
        Exception exception)
    {
        (bool shouldRevert, ulong gasToReturn, bool ranOutOfGas) = exception switch
        {
            ArbitrumPrecompileException precompileException => precompileException switch
            {
                _ when precompileException.Type == PrecompileExceptionType.SolidityError
                    => PayForOutput(context, precompileException.Output, success: false),

                _ when precompileException.Type == PrecompileExceptionType.ProgramActivation
                    => new(false, 0UL, false),

                _ when precompileException.Type == PrecompileExceptionType.Revert
                    => new(true, precompileException.IsRevertDuringCalldataDecoding ? 0UL : context.GasLeft, false),

                _ => DefaultExceptionHandling(context, exception),
            },
            // Other exception types outside of direct precompile control should be handled by default
            _ => DefaultExceptionHandling(context, exception)
        };

        ReturnSomeGas(state, gasToReturn);

        if (shouldRevert && Logger.IsTrace)
            Logger.Trace($"Precompile reverted with exception: {exception.GetType()} and message {exception.Message}, refunding gas: {ArbitrumGasPolicy.GetRemainingGas(state.Gas)}");
        else if (Logger.IsTrace)
            Logger.Trace($"Precompile failed with exception: {exception.GetType()} and message {exception.Message}, consuming all gas");

        EvmExceptionType exceptionType = exception switch
        {
            _ when shouldRevert => EvmExceptionType.Revert,
            _ when ranOutOfGas => EvmExceptionType.OutOfGas,
            _ => EvmExceptionType.PrecompileFailure
        };

        byte[]? output = exception is ArbitrumPrecompileException e && e.Type == PrecompileExceptionType.SolidityError && !ranOutOfGas ? e.Output : default;

        string errorMessage = exception switch
        {
            ArbitrumPrecompileException arbEx => $"Arbitrum precompile failed: {arbEx.Message} with {arbEx.Type}",
            _ => $"Precompile execution error: {exception.Message} with type {exception.GetType()}"
        };

        return new(output, precompileSuccess: false, fromVersion: 0, shouldRevert, exceptionType)
        {
            SubstateError = errorMessage
        };

    }

    private PrecompileOutcome DefaultExceptionHandling(ArbitrumPrecompileExecutionContext context, Exception exception)
    {
        bool outOfGas = exception is ArbitrumPrecompileException e && e.OutOfGas;

        return FreeArbosState.CurrentArbosVersion >= ArbosVersion.Eleven
            ? new(true, context.GasLeft, outOfGas) : new(false, 0UL, outOfGas);
    }

    private CallResult RunWasmCode(scoped ref ArbitrumGasPolicy gas)
    {
        Address actingAddress = VmState.To;
        CodeInfo codeInfo = VmState.Env.CodeInfo;

        VmState.Gas = gas;

        // Track reentrant calls
        uint currentDepth = Programs.GetValueOrDefault(actingAddress);
        bool reentrant = currentDepth > 0;
        Programs[actingAddress] = currentDepth + 1;

        try
        {
            TracingInfo? tracingInfo = CreateTracingInfoIfNeeded();
            bool debugMode = IsDebugMode();

            StylusOperationResult<byte[]> output = FreeArbosState.Programs.CallProgram(this,
                tracingInfo,
                _specProvider.ChainId,
                FreeArbosState.Blockhashes.GetL1BlockNumber(),
                reentrant,
                MessageRunMode.MessageCommitMode,
                debugMode);

            return output.IsSuccess
                ? new CallResult(null, output.Value, null, codeInfo.Version)
                : CreateErrorResult(output, codeInfo);
        }
        finally
        {
            Programs[actingAddress] = currentDepth;
        }
    }

    private TracingInfo? CreateTracingInfoIfNeeded()
    {
        return TxTracer is IArbitrumTxTracer arbitrumTracer
            && arbitrumTracer.GetType() != typeof(ArbNullTxTracer)
            ? new TracingInfo(arbitrumTracer, TracingScenario.TracingDuringEvm, VmState.Env)
            : null;
    }

    private bool IsDebugMode()
    {
        return specHelper.AllowDebugPrecompiles;
    }

    private CallResult CreateErrorResult(StylusOperationResult<byte[]> output, CodeInfo codeInfo)
    {
        EvmExceptionType exceptionType = output.Error!.Value.OperationResultType.ToEvmExceptionType();
        byte[] errorData = output.Value ?? [];
        bool shouldRevert = exceptionType == EvmExceptionType.Revert;

        if (!shouldRevert)
            VmState.Gas = ArbitrumGasPolicy.FromLong(0);

        return new CallResult(
            errorData,
            precompileSuccess: null,
            fromVersion: codeInfo.Version,
            shouldRevert: shouldRevert,
            exceptionType: exceptionType);
    }

    private TransactionSubstate ExecuteStylusEvmCallback(CallResult result)
    {
        ZeroPaddedSpan previousCallOutput = ZeroPaddedSpan.Empty;
        PrepareNextCallFrame(in result, ref previousCallOutput);
        bool previousStateSucceeded = true;
        try
        {
            while (true)
            {
                previousStateSucceeded = true;

                // For non-continuation frames, clear any previously stored return data.
                if (!_currentState.IsContinuation)
                    ReturnDataBuffer = Array.Empty<byte>();

                Exception? failure;
                string? substateError = null;
                try
                {
                    CallResult callResult;
                    // If the current state represents a precompiled contract, handle it separately.
                    if (_currentState.IsPrecompile)
                    {
                        callResult = ExecutePrecompile(_currentState, _txTracer.IsTracingActions, out failure, out substateError);
                        if (failure is not null)
                            goto Failure;
                    }
                    else
                    {
                        // Start transaction tracing for non-continuation frames if tracing is enabled.
                        if (_txTracer.IsTracingActions && !_currentState.IsContinuation)
                        {
                            TraceTransactionActionStart(_currentState);
                        }

                        // Execute the regular EVM call if valid code is present; otherwise, mark as invalid.
                        if (_currentState.Env.CodeInfo is not null)
                        {
                            // TODO: tracing
                            callResult = ExecuteCall<OffFlag>(
                                _previousCallResult,
                                previousCallOutput,
                                _previousCallOutputDestination);
                        }
                        else
                        {
                            callResult = CallResult.InvalidCodeException;
                        }

                        // If the call did not finish with a return, set up the next call frame and continue.
                        if (!callResult.IsReturn)
                        {
                            PrepareNextCallFrame(in callResult, ref previousCallOutput);
                            continue;
                        }

                        // Handle exceptions raised during the call execution.
                        if (callResult.IsException)
                        {
                            // Set to ensure no refund is propagated from this call frame.
                            previousStateSucceeded = false;

                            // Reset access list and logs for top-level calls as it won't be reset during _currentState.Dispose()
                            if (_currentState.IsTopLevel)
                                _currentState.AccessTracker.Restore();

                            // here it will never finalize the transaction as it will never be a TopLevel state
                            TransactionSubstate substate = HandleException(in callResult, ref previousCallOutput, out bool terminate);

                            if (terminate && !substate.ShouldRevert)
                                _currentState.Gas = ArbitrumGasPolicy.FromLong(0);

                            if (terminate)
                                return substate;

                            // Continue execution if the exception did not immediately finalize the transaction.
                            continue;
                        }
                    }

                    if (_currentState.IsTopLevel)
                    {
                        // Rollback access list and logs on Revert
                        if (callResult.ShouldRevert)
                        {
                            // Set to ensure no refund is propagated from this call frame.
                            previousStateSucceeded = false;

                            _currentState.AccessTracker.Restore();
                            WorldState.Restore(_currentState.Snapshot);
                        }

                        return PrepareStylusTopLevelSubstate(callResult);
                    }

                    // For nested call frames, merge the results and restore the previous execution state.
                    using (VmState<ArbitrumGasPolicy> previousState = _currentState)
                    {
                        // Restore the previous state from the stack and mark it as a continuation.
                        _currentState = _stateStack.Pop();
                        _currentState.IsContinuation = true;
                        ArbitrumGasPolicy.Refund(ref _currentState.Gas, in previousState.Gas);

                        if (!callResult.ShouldRevert)
                        {
                            long gasAvailableForCodeDeposit = ArbitrumGasPolicy.GetRemainingGas(previousState.Gas);

                            // Process contract creation calls differently from regular calls.
                            if (previousState.ExecutionType.IsAnyCreate())
                            {
                                PrepareCreateData(previousState, ref previousCallOutput);
                                if (previousState.ExecutionType.IsAnyCreateLegacy())
                                {
                                    HandleLegacyCreate(
                                        in callResult,
                                        previousState,
                                        gasAvailableForCodeDeposit,
                                        ref previousStateSucceeded);
                                }
                                else if (previousState.ExecutionType.IsAnyCreateEof())
                                {
                                    HandleEofCreate(
                                        in callResult,
                                        previousState,
                                        gasAvailableForCodeDeposit,
                                        ref previousStateSucceeded);
                                }
                            }
                            else
                            {
                                previousCallOutput = HandleRegularReturn<OffFlag>(in callResult, previousState);
                            }

                            // Commit the changes from the completed call frame if execution was successful.
                            if (previousStateSucceeded)
                                previousState.CommitToParent(_currentState);
                        }
                        else
                        {
                            // Revert state changes for the previous call frame when a revert condition is signaled.
                            HandleRevert(previousState, callResult, ref previousCallOutput);
                        }
                    }
                }
                // Handle specific EVM or overflow exceptions by routing to the failure handling block.
                catch (Exception ex) when (ex is EvmException or OverflowException)
                {
                    failure = ex;
                    goto Failure;
                }

                continue;
            Failure:
                previousStateSucceeded = false;

                TransactionSubstate failSubstate = HandleFailure<OffFlag>(failure, substateError, ref previousCallOutput, out bool shouldExit);

                if (_currentState.IsTopLevel)
                    _currentState.AccessTracker.Restore();

                if (failure is OutOfGasException)
                    _currentState.Gas = ArbitrumGasPolicy.FromLong(0);

                if (shouldExit)
                {
                    return failSubstate;
                }
            }
        }
        finally
        {
            using VmState<ArbitrumGasPolicy> previousState = _currentState;
            _currentState = _stateStack.Pop();
            _currentState.IsContinuation = true;

            // Propagate refund only when the previous state succeeded
            if (previousStateSucceeded)
                _currentState.Refund += previousState.Refund;

            // Manually dispose ExecutionEnvironment for top-level frames.
            // Top-level frames (created by StylusCall/StylusCreate) skip Env disposal in EvmState.Dispose()
            // In Stylus callbacks, we create the Env internally,
            // so we must explicitly dispose it here to prevent memory leaks.
            // Nested frames (IsTopLevel=false) have their Env disposed automatically by EvmState.Dispose().
            if (previousState.IsTopLevel)
                previousState.Env.Dispose();
        }
    }

    private TransactionSubstate PrepareStylusTopLevelSubstate(CallResult callResult)
    {
        return new TransactionSubstate(
            callResult.Output,
            _currentState.Refund,
            _currentState.AccessTracker.DestroyList,
            _currentState.AccessTracker.Logs,
            callResult.ShouldRevert,
            isTracerConnected: _txTracer.IsTracing,
            callResult.ExceptionType,
            _logger);
    }

    private static void ConsumeAllGas(VmState<ArbitrumGasPolicy> state)
    {
        state.Gas = ArbitrumGasPolicy.FromLong(0);
    }

    private static void ReturnSomeGas(VmState<ArbitrumGasPolicy> state, ulong gasToReturn)
    {
        state.Gas = ArbitrumGasPolicy.FromLong((long)gasToReturn);
    }

    private readonly record struct PrecompileOutcome(
        bool ShouldRevert,
        ulong GasLeft,
        bool RanOutOfGas
    );
}
