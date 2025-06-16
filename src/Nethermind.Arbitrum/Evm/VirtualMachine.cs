using System.Runtime.CompilerServices;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Logging;

[assembly: InternalsVisibleTo("Nethermind.Arbitrum.Evm.Test")]
namespace Nethermind.Arbitrum.Evm;

public sealed unsafe partial class ArbVirtualMachine(
    IBlockhashProvider? blockHashProvider,
    ISpecProvider? specProvider,
    ILogManager? logManager
): VirtualMachine(blockHashProvider, specProvider, logManager)
{
    public override CallResult RunPrecompile(EvmState state)
    {
        if (state.Env.CodeInfo is Nethermind.Evm.CodeAnalysis.PrecompileInfo)
        {
            return base.RunPrecompile(state);
        }

        ReadOnlyMemory<byte> callData = state.Env.InputData;
        IArbitrumPrecompile precompile = ((PrecompileInfo)state.Env.CodeInfo).Precompile;

        ArbitrumPrecompileExecutionContext context = new(
            state.From, (ulong)state.GasAvailable, (ulong)state.GasAvailable, TxTracer, false
        );
        try
        {
            context.ArbosState = ArbosState.OpenArbosState(WorldState, context, Logger);

            // Revert if calldata does not contain method ID to be called
            if (callData.Length < 4)
            {
                return new(default, false, 0, true);
            }
            // Burn gas for argument data supplied (excluding method id)
            ulong dataGasCost = GasCostOf.DataCopy * (ulong)EvmPooledMemory.Div32Ceiling((Int256.UInt256)callData.Length-4);
            context.Burn(dataGasCost);

            //TODO success is always true here, as we use Exception if false
            (byte[] output, bool success) = precompile.RunAdvanced(context, this, callData);

            // Add logs
            foreach (LogEntry log in context.EventLogs)
            {
                state.AccessTracker.Logs.Add(log);
            }

            // Burn gas for output data
            return PayForOutput(state, context, output, success);
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
            CallResult callResult = new(output: default, precompileSuccess: false, fromVersion: 0, shouldRevert: true);
            return callResult;
        }
    }

    private CallResult PayForOutput(EvmState state, ArbitrumPrecompileExecutionContext context, byte[] executionOutput, bool success)
    {
        ulong outputGasCost = GasCostOf.DataCopy * (ulong)EvmPooledMemory.Div32Ceiling((Int256.UInt256)executionOutput.Length);
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
