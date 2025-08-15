using System.Runtime.CompilerServices;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.Precompiles;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Evm.Tracing;
using PrecompileInfo = Nethermind.Arbitrum.Precompiles.PrecompileInfo;

[assembly: InternalsVisibleTo("Nethermind.Arbitrum.Evm.Test")]
namespace Nethermind.Arbitrum.Evm;

using unsafe OpCode = delegate*<VirtualMachineBase, ref EvmStack, ref long, ref int, EvmExceptionType>;

public sealed unsafe class ArbitrumVirtualMachine(
    IBlockhashProvider? blockHashProvider,
    ISpecProvider? specProvider,
    ILogManager? logManager
) : VirtualMachineBase(blockHashProvider, specProvider, logManager), IStylusVmHost
{
    public ArbosState FreeArbosState { get; private set; } = null!;
    public ArbitrumTxExecutionContext ArbitrumTxExecutionContext { get; set; } = new();

    public override TransactionSubstate ExecuteTransaction<TTracingInst>(
        EvmState evmState,
        IWorldState worldState,
        ITxTracer txTracer)
    {
        FreeArbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), Logger);
        return base.ExecuteTransaction<TTracingInst>(evmState, worldState, txTracer);
    }

    protected override OpCode[] GenerateOpCodes<TTracingInst>(IReleaseSpec spec)
    {
        OpCode[] opcodes = base.GenerateOpCodes<TTracingInst>(spec);
        opcodes[(int)Instruction.GASPRICE] = &ArbitrumEvmInstructions.InstructionBlkUInt256<TTracingInst>;
        opcodes[(int)Instruction.NUMBER] = &ArbitrumEvmInstructions.InstructionBlkUInt64<TTracingInst>;
        return opcodes;
    }

    protected override CallResult RunPrecompile(EvmState state)
    {
        // If precompile is not an arbitrum specific precompile but a standard one
        if (state.Env.CodeInfo is Nethermind.Evm.CodeAnalysis.PrecompileInfo)
        {
            return base.RunPrecompile(state);
        }

        ReadOnlyMemory<byte> callData = state.Env.InputData;
        IArbitrumPrecompile precompile = ((PrecompileInfo)state.Env.CodeInfo).Precompile;

        TracingInfo tracingInfo = new(
            TxTracer as IArbitrumTxTracer ?? ArbNullTxTracer.Instance,
            TracingScenario.TracingDuringEvm,
            state.Env
        );

        // I think state.Env.CallDepth == StateStack.Count (invariant)
        Address? grandCaller = state.Env.CallDepth >= 2 ? StateStack.ElementAt(state.Env.CallDepth - 2).From : null;

        ArbitrumPrecompileExecutionContext context = new(
            state.From, GasSupplied: (ulong)state.GasAvailable,
            ReadOnly: state.IsStatic, WorldState, BlockExecutionContext,
            ChainId.ToByteArray().ToULongFromBigEndianByteArrayWithoutLeadingZeros(), tracingInfo, Spec
        )
        {
            BlockHashProvider = BlockHashProvider,
            CallDepth = state.Env.CallDepth,
            GrandCaller = grandCaller,
            Origin = TxExecutionContext.Origin,
            Value = state.Env.Value,
            TopLevelTxType = ArbitrumTxExecutionContext.TopLevelTxType,
            FreeArbosState = FreeArbosState,
            CurrentRetryable = ArbitrumTxExecutionContext.CurrentRetryable,
            CurrentRefundTo = ArbitrumTxExecutionContext.CurrentRefundTo
        };

        //TODO: temporary fix but should change error management from Exceptions to returning errors instead i think
        bool unauthorizedCallerException = false;
        try
        {
            // Arbos opening could throw if there is not enough gas
            context.ArbosState = ArbosState.OpenArbosState(WorldState, context, Logger);

            // Revert if calldata does not contain method ID to be called
            if (callData.Length < 4)
            {
                return new(default, false, 0, true);
            }

            if (!precompile.IsOwner)
            {
                // Burn gas for argument data supplied (excluding method id)
                ulong dataGasCost = GasCostOf.DataCopy * Math.Utils.Div32Ceiling((ulong)callData.Length - 4);
                context.Burn(dataGasCost);
            }

            byte[] output = precompile.RunAdvanced(context, callData);

            // Add logs
            foreach (LogEntry log in context.EventLogs)
            {
                state.AccessTracker.Logs.Add(log);
            }

            // Burn gas for output data
            return PayForOutput(state, context, output, true);
        }
        catch (DllNotFoundException exception)
        {
            if (Logger.IsError) Logger.Error($"Failed to load one of the dependencies of {precompile.GetType()} precompile", exception);
            throw;
        }
        catch (PrecompileSolidityError exception)
        {
            if (Logger.IsError) Logger.Error($"Solidity error in precompiled contract ({precompile.GetType()}), execution exception", exception);
            return PayForOutput(state, context, exception.ErrorData, false);
        }
        catch (Exception exception)
        {
            if (Logger.IsError) Logger.Error($"Precompiled contract ({precompile.GetType()}) execution exception", exception);
            unauthorizedCallerException = OwnerWrapper.UnauthorizedCallerException().Equals(exception);
            //TODO: Additional check needed for ErrProgramActivation --> add check when doing ArbWasm precompile
            state.GasAvailable = FreeArbosState.CurrentArbosVersion >= ArbosVersion.Eleven ? (long)context.GasLeft : 0;
            return new(output: default, precompileSuccess: false, fromVersion: 0, shouldRevert: true);
        }
        finally
        {
            if (precompile.IsOwner && !unauthorizedCallerException)
            {
                // we don't deduct gas since we don't want to charge the owner
                state.GasAvailable = (long)context.GasSupplied;
            }
        }
    }

    private static CallResult PayForOutput(EvmState state, ArbitrumPrecompileExecutionContext context, byte[] executionOutput, bool success)
    {
        ulong outputGasCost = GasCostOf.DataCopy * Math.Utils.Div32Ceiling((ulong)executionOutput.Length);
        try
        {
            context.Burn(outputGasCost);
        }
        catch (Exception)
        {
            return new(output: default, precompileSuccess: false, fromVersion: 0, shouldRevert: true);
        }
        finally
        {
            state.GasAvailable = (long)context.GasLeft;
        }

        return new(executionOutput, precompileSuccess: success, fromVersion: 0, shouldRevert: !success);
    }

    private static bool ChargeAccountAccessGas(ref long gasAvailable, VirtualMachineBase vm, Address address, bool chargeForWarm = true)
    {
        bool result = true;
        IReleaseSpec spec = vm.Spec;
        if (spec.UseHotAndColdStorage)
        {
            EvmState vmState = vm.EvmState;
            if (vm.TxTracer.IsTracingAccess)
            {
                // Ensure that tracing simulates access-list behavior.
                vmState.AccessTracker.WarmUp(address);
            }

            // If the account is cold (and not a precompile), charge the cold access cost.
            if (vmState.AccessTracker.IsCold(address) && !address.IsPrecompile(spec))
            {
                result = UpdateGas(GasCostOf.ColdAccountAccess, ref gasAvailable);
                vmState.AccessTracker.WarmUp(address);
            }
            else if (chargeForWarm)
            {
                // Otherwise, if warm access should be charged, apply the warm read cost.
                result = UpdateGas(GasCostOf.WarmStateRead, ref gasAvailable);
            }
        }

        return result;
    }

    public (byte[] ret, ulong cost, EvmExceptionType? err) StylusCall(ExecutionType kind, Address to, ReadOnlySpan<byte> input,
        ulong gasLeftReportedByRust, ulong gasRequestedByRust, in UInt256 value)
    {
        long gasAvailable = (long)gasLeftReportedByRust;
        Address codeSource = to;

        // Charge gas for accessing the account's code.
        if (!ChargeAccountAccessGas(ref gasAvailable, this, codeSource)) goto OutOfGas;

        ref readonly ExecutionEnvironment env = ref EvmState.Env;

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
        if (EvmState.IsStatic && !transferValue.IsZero)
            return ([], 0 , EvmExceptionType.StaticCallViolation);

        // Determine caller and target based on the call type.
        Address caller = kind == ExecutionType.DELEGATECALL ? env.Caller : env.ExecutingAccount;
        Address target = (kind == ExecutionType.CALL || kind == ExecutionType.STATICCALL)
            ? codeSource
            : env.ExecutingAccount;

        long gasExtra = 0L;

        // Add extra gas cost if value is transferred.
        if (!transferValue.IsZero)
        {
            gasExtra += GasCostOf.CallValue;
        }

        // Charge additional gas if the target account is new or considered empty.
        if (!Spec.ClearEmptyAccountWhenTouched && !WorldState.AccountExists(target))
        {
            gasExtra += GasCostOf.NewAccount;
        }
        else if (Spec.ClearEmptyAccountWhenTouched && transferValue != 0 && WorldState.IsDeadAccount(target))
        {
            gasExtra += GasCostOf.NewAccount;
        }

        if(!UpdateGas(gasExtra, ref gasAvailable))
            goto OutOfGas;

        UInt256 gasLimit = UInt256.Min((UInt256)(gasAvailable - gasAvailable / 64), new UInt256(gasRequestedByRust));

        // If gasLimit exceeds the host's representable range, treat as out-of-gas.
        if (gasLimit >= long.MaxValue) goto OutOfGas;

        long gasLimitUl = (long)gasLimit;
        if (!UpdateGas(gasLimitUl, ref gasAvailable)) goto OutOfGas;

        // Add call stipend if value is being transferred.
        if (!transferValue.IsZero)
        {
            gasLimitUl += GasCostOf.CallStipend;
        }

        // Check call depth and balance of the caller.
        if (env.CallDepth >= MaxCallDepth ||
            (!transferValue.IsZero && WorldState.GetBalance(env.ExecutingAccount) < transferValue))
        {
            // If the call cannot proceed, return an empty response and push zero on the stack.
            ReturnDataBuffer = Array.Empty<byte>();

            // Optionally report memory changes for refund tracing.

            // Refund the remaining gas to the caller.
            gasAvailable += gasLimitUl;
            return ([], (ulong)gasAvailable, EvmExceptionType.None);
        }

        // Take a snapshot of the state for potential rollback.
        Snapshot snapshot = WorldState.TakeSnapshot();
        // Subtract the transfer value from the caller's balance.
        WorldState.SubtractFromBalance(caller, in transferValue, Spec);

        // Retrieve code information for the call and schedule background analysis if needed.
        ICodeInfo codeInfo = CodeInfoRepository.GetCachedCodeInfo(WorldState, codeSource, Spec);

        ReadOnlyMemory<byte> callData = input.ToArray().AsMemory();

        // Construct the execution environment for the call.
        ExecutionEnvironment callEnv = new(
            codeInfo: codeInfo,
            executingAccount: target,
            caller: caller,
            codeSource: codeSource,
            callDepth: env.CallDepth + 1,
            transferValue: in transferValue,
            value: in callValue,
            inputData: in callData);

        // Rent a new call frame for executing the call.
        EvmState returnData = EvmState.RentFrame(
            gasAvailable: gasLimitUl,
            outputDestination: 0,
            outputLength: 0,
            executionType: kind,
            isStatic: kind == ExecutionType.STATICCALL || EvmState.IsStatic,
            isCreateOnPreExistingAccount: false,
            env: in callEnv,
            stateForAccessLists: in EvmState.AccessTracker,
            snapshot: in snapshot);

        ReturnData = returnData;
        CallResult callResult = new(returnData);
        TransactionSubstate txnSubstrate = ExecuteStylusTransaction(callResult);
        return ([], (ulong)(txnSubstrate.Refund + gasAvailable), EvmExceptionType.None);
    OutOfGas:
        return ([], (ulong)gasAvailable, EvmExceptionType.OutOfGas);
    }

    public (Address created, byte[] returnData, ulong cost, EvmExceptionType? err) StylusCreate(ReadOnlySpan<byte> initCode, in UInt256 endowment,
        UInt256? salt, ulong gasLimit)
    {
        var gasAvailable = (long)gasLimit;

        if (EvmState.IsStatic)
            goto StaticCallViolation;

        // Reset the return data buffer as contract creation does not use previous return data.
        ReturnData = null;
        ref readonly ExecutionEnvironment env = ref EvmState.Env;
        IWorldState state = WorldState;

        // Ensure the executing account exists in the world state. If not, create it with a zero balance.
        if (!state.AccountExists(env.ExecutingAccount)) state.CreateAccount(env.ExecutingAccount, UInt256.Zero);


        UInt256 value = endowment;

        ExecutionType kind = ExecutionType.CREATE;
        if (salt != null) kind = ExecutionType.CREATE2;

        UInt256 initCodeLength = new ((uint)initCode.Length);

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
                       (Spec.IsEip3860Enabled ? GasCostOf.InitCodeWord * EvmPooledMemory.Div32Ceiling(in initCodeLength, out outOfGas) : 0) +
                       (kind==ExecutionType.CREATE2
                           ? GasCostOf.Sha3Word * EvmPooledMemory.Div32Ceiling(in initCodeLength, out outOfGas)
                           : 0);

        // Check gas sufficiency: if outOfGas flag was set during gas division or if gas update fails.
        if (outOfGas || !UpdateGas(gasCost, ref gasAvailable))
            goto OutOfGas;

        // Verify call depth does not exceed the maximum allowed. If exceeded, return early with empty data.
        // This guard ensures we do not create nested contract calls beyond EVM limits.
        if (env.CallDepth >= MaxCallDepth)
        {
            ReturnDataBuffer = Array.Empty<byte>();
            return (Address.Zero, [], (ulong)gasAvailable, EvmExceptionType.None);
        }

        // Check that the executing account has sufficient balance to transfer the specified value.
        UInt256 balance = state.GetBalance(env.ExecutingAccount);
        if (value > balance)
        {
            ReturnDataBuffer = Array.Empty<byte>();
            return (Address.Zero, [], (ulong)(gasAvailable), EvmExceptionType.None);
        }

        // Retrieve the nonce of the executing account to ensure it hasn't reached the maximum.
        UInt256 accountNonce = state.GetNonce(env.ExecutingAccount);
        UInt256 maxNonce = ulong.MaxValue;
        if (accountNonce >= maxNonce)
        {
            ReturnDataBuffer = Array.Empty<byte>();
            return (Address.Zero, [], (ulong)(gasAvailable), EvmExceptionType.None);
        }

        // Calculate gas available for the contract creation call.
        // Use the 63/64 gas rule if specified in the current EVM specification.
        long callGas = Spec.Use63Over64Rule ? gasAvailable - gasAvailable / 64L : gasAvailable;
        if (!UpdateGas(callGas, ref gasAvailable))
            goto OutOfGas;

        // Compute the contract address:
        // - For CREATE: based on the executing account and its current nonce.
        // - For CREATE2: based on the executing account, the provided salt, and the init code.
        Address contractAddress = kind == ExecutionType.CREATE
            ? ContractAddress.From(env.ExecutingAccount, state.GetNonce(env.ExecutingAccount))
            : ContractAddress.From(env.ExecutingAccount, salt!.Value.ToBigEndian(), initCode);

        // For EIP-2929 support, pre-warm the contract address in the access tracker to account for hot/cold storage costs.
        if (Spec.UseHotAndColdStorage)
        {
            EvmState.AccessTracker.WarmUp(contractAddress);
        }

        // Increment the nonce of the executing account to reflect the contract creation.
        state.IncrementNonce(env.ExecutingAccount);

        // Analyze and compile the initialization code.
        CodeInfoFactory.CreateInitCodeInfo(initCode.ToArray(), Spec, out ICodeInfo codeinfo, out _);

        // Take a snapshot of the current state. This allows the state to be reverted if contract creation fails.
        Snapshot snapshot = state.TakeSnapshot();

        // Check for contract address collision. If the contract already exists and contains code or non-zero state,
        // then the creation should be aborted.
        bool accountExists = state.AccountExists(contractAddress);
        if (accountExists && contractAddress.IsNonZeroAccount(Spec, CodeInfoRepository, state))
        {
            ReturnDataBuffer = Array.Empty<byte>();
            return (contractAddress, [], (ulong)(gasAvailable), EvmExceptionType.None);
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
        ExecutionEnvironment callEnv = new(
            codeInfo: codeinfo,
            executingAccount: contractAddress,
            caller: env.ExecutingAccount,
            codeSource: null,
            callDepth: env.CallDepth + 1,
            transferValue: in value,
            value: in value,
            inputData: default);

        // Rent a new frame to run the initialization code in the new execution environment.
        EvmState returnData = EvmState.RentFrame(
            gasAvailable: callGas,
            outputDestination: 0,
            outputLength: 0,
            executionType: kind,
            isStatic: EvmState.IsStatic,
            isCreateOnPreExistingAccount: accountExists,
            env: in callEnv,
            stateForAccessLists: in EvmState.AccessTracker,
            snapshot: in snapshot);

        ReturnData = returnData;
        CallResult callResult = new(returnData);
        TransactionSubstate txnSubstrate = ExecuteStylusTransaction(callResult);
        return (contractAddress, [], (ulong)(txnSubstrate.Refund + gasAvailable), EvmExceptionType.None);
    OutOfGas:
        return (Address.Zero, [], (ulong)gasAvailable, EvmExceptionType.OutOfGas);
    StaticCallViolation:
        return (Address.Zero, [], (ulong)gasAvailable,EvmExceptionType.StaticCallViolation);
    }

    // TODO: implement correct functionality
    private static bool IsStylusCall(ICodeInfo codeInfo)
    {
        return true;
    }

    private TransactionSubstate ExecuteStylusTransaction(CallResult result)
    {
        ZeroPaddedSpan previousCallOutput = ZeroPaddedSpan.Empty;
        PrepareNextCallFrame(in result, ref previousCallOutput);

        CallResult callResult = default;
        while (true)
        {
            // For non-continuation frames, clear any previously stored return data.
            if (!_currentState.IsContinuation) ReturnDataBuffer = Array.Empty<byte>();

            if (IsStylusCall(_currentState.Env.CodeInfo!)) return PrepareTopLevelSubstate(in callResult);

            Exception? failure;
            try
            {
                callResult = default;
                // If the current state represents a precompiled contract, handle it separately.
                if (_currentState.IsPrecompile)
                {
                    callResult = ExecutePrecompile(_currentState, _txTracer.IsTracingActions, out failure);
                    if (failure is not null)
                    {
                        // Jump to the failure handler if a precompile error occurred.
                        goto Failure;
                    }
                }
                // TODO: add step to check if stylus contract and then execute stylus
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
                            _currentState,
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
                        TransactionSubstate? substate = HandleException(in callResult, ref previousCallOutput);
                        if (substate is not null)
                        {
                            return substate;
                        }
                        // Continue execution if the exception did not immediately finalize the transaction.
                        continue;
                    }
                }

                // For nested call frames, merge the results and restore the previous execution state.
                using (EvmState previousState = _currentState)
                {
                    // Restore the previous state from the stack and mark it as a continuation.
                    _currentState = _stateStack.Pop();
                    _currentState.IsContinuation = true;

                    bool previousStateSucceeded = true;

                    if (!callResult.ShouldRevert)
                    {
                        long gasAvailableForCodeDeposit = previousState.GasAvailable;

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
                                    Spec,
                                    ref previousStateSucceeded);
                            }
                            else if (previousState.ExecutionType.IsAnyCreateEof())
                            {
                                HandleEofCreate(
                                    in callResult,
                                    previousState,
                                    gasAvailableForCodeDeposit,
                                    Spec,
                                    ref previousStateSucceeded);
                            }
                        }
                        else
                        {
                            // Process a standard call return.
                            // TODO: tracing
                            previousCallOutput = HandleRegularReturn<OffFlag>(in callResult, previousState);
                        }

                        // Commit the changes from the completed call frame if execution was successful.
                        if (previousStateSucceeded)
                        {
                            previousState.CommitToParent(_currentState);
                        }
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

            // Continue with the next iteration of the execution loop.
            continue;

        // Failure handling: attempts to process and possibly finalize the transaction after an error.
        Failure:
            // TODO: tracing
            TransactionSubstate? failSubstate = HandleFailure<OffFlag>(failure, ref previousCallOutput);
            if (failSubstate is not null)
            {
                return failSubstate;
            }
        }
    }
}
