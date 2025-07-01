using System.Runtime.CompilerServices;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Logging;

[assembly: InternalsVisibleTo("Nethermind.Arbitrum.Evm.Test")]
namespace Nethermind.Arbitrum.Evm;

public sealed unsafe partial class ArbitrumVirtualMachine(
    IBlockhashProvider? blockHashProvider,
    ISpecProvider? specProvider,
    ILogManager? logManager
) : VirtualMachineBase(blockHashProvider, specProvider, logManager)
{
    public ArbitrumTxExecutionContext ArbitrumTxExecutionContext { get; set; }

    protected override CallResult RunPrecompile(EvmState state)
    {
        // If precompile is not an arbitrum specific precompile but a standard one
        if (state.Env.CodeInfo is Nethermind.Evm.CodeAnalysis.PrecompileInfo)
        {
            return base.RunPrecompile(state);
        }

        ReadOnlyMemory<byte> callData = state.Env.InputData;
        IArbitrumPrecompile precompile = ((PrecompileInfo)state.Env.CodeInfo).Precompile;

        var tracingInfo = new TracingInfo(TxTracer as IArbitrumTxTracer, TracingScenario.TracingDuringEvm, null);

        ArbitrumPrecompileExecutionContext context = new(
            state.From, GasSupplied: (ulong)state.GasAvailable,
            ReadOnly: false, WorldState, BlockExecutionContext,
            ChainId.ToByteArray().ToULongFromBigEndianByteArrayWithoutLeadingZeros(), tracingInfo, Spec
        )
        {
            CurrentRetryable = ArbitrumTxExecutionContext.CurrentRetryable,
            CurrentRefundTo = ArbitrumTxExecutionContext.CurrentRefundTo
        };
        try
        {
            context.ArbosState = ArbosState.OpenArbosState(WorldState, context, Logger);

            // Revert if calldata does not contain method ID to be called
            if (callData.Length < 4)
            {
                return new(default, false, 0, true);
            }
            // Burn gas for argument data supplied (excluding method id)
            ulong dataGasCost = GasCostOf.DataCopy * Math.Utils.Div32Ceiling((ulong)callData.Length - 4);
            context.Burn(dataGasCost);

            byte[] output = precompile.RunAdvanced(context, callData, TxTracer as IArbitrumTxTracer);

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
            //TODO: Additional check needed for ErrProgramActivation --> add check when doing ArbWasm precompile
            state.GasAvailable = context.ArbosState.CurrentArbosVersion >= ArbosVersion.Eleven ? (long)context.GasLeft : 0;
            return new(output: default, precompileSuccess: false, fromVersion: 0, shouldRevert: true);
        }
    }

    private CallResult PayForOutput(EvmState state, ArbitrumPrecompileExecutionContext context, byte[] executionOutput, bool success)
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
