using System.Runtime.CompilerServices;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Logging;
using Nethermind.Evm.Tracing;

[assembly: InternalsVisibleTo("Nethermind.Arbitrum.Evm.Test")]
namespace Nethermind.Arbitrum.Evm;

using unsafe OpCode = delegate*<VirtualMachine, ref EvmStack, ref long, ref int, EvmExceptionType>;

public sealed unsafe class ArbitrumVirtualMachine(
    IBlockhashProvider? blockHashProvider,
    ISpecProvider? specProvider,
    ILogManager? logManager
) : VirtualMachine(blockHashProvider, specProvider, logManager)
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
            state.From, state.Env.Value, GasSupplied: (ulong)state.GasAvailable,
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
            CurrentRefundTo = ArbitrumTxExecutionContext.CurrentRefundTo,
            PosterFee = ArbitrumTxExecutionContext.PosterFee
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
}
