using System.Runtime.CompilerServices;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Logging;

[assembly: InternalsVisibleTo("Nethermind.Arbitrum.Evm.Test")]
namespace Nethermind.Arbitrum.Evm;

public sealed unsafe partial class ArbVirtualMachine(
    IBlockhashProvider? blockHashProvider,
    ISpecProvider? specProvider,
    ILogManager? logManager
): VirtualMachineBase(blockHashProvider, specProvider, logManager)
{
    protected override CallResult RunPrecompile(EvmState state)
    {
        if (state.Env.CodeInfo is Nethermind.Evm.CodeAnalysis.PrecompileInfo)
        {
            return base.RunPrecompile(state);
        }

        ReadOnlyMemory<byte> callData = state.Env.InputData;
        IArbitrumPrecompile precompile = ((PrecompileInfo)state.Env.CodeInfo).Precompile;

        try
        {
            ArbitrumPrecompileExecutionContext context = new(
                state.From, (ulong)state.GasAvailable, (ulong)state.GasAvailable, TxTracer, false
            );
            context.ArbosState = ArbosState.OpenArbosState(WorldState, context, Logger);

            // Revert if calldata does not contain method ID to be called
            if (callData.Length < 4)
            {
                return new(default, false, 0, true);
            }
            // Burn gas for argument data supplied (excluding method id)
            ulong dataGasCost = GasCostOf.DataCopy * (ulong)EvmPooledMemory.Div32Ceiling((Int256.UInt256)callData.Length-4);
            context.Burn(dataGasCost);

            (ReadOnlyMemory<byte> output, bool success) = precompile.RunAdvanced(context, this, callData);

            // TODO: Nitro burns gas for solidity errors!
            // Need to bridge c# errors to evm errors
            // Maybe c# precompiles' respective parser class should already return EVM-encoded errors?
            //   -> probably more efficient than using reflection like Nitro does
            // Implement that once we handle precompile containing solidity errors

            // Burn gas for output data
            ulong outputGasCost = GasCostOf.ReturnDataLoad * (ulong)EvmPooledMemory.Div32Ceiling((Int256.UInt256)output.Length);
            context.Burn(outputGasCost);

            state.GasAvailable = (long)context.GasLeft;

            return new(output, precompileSuccess: success, fromVersion: 0, shouldRevert: !success);
        }
        catch (DllNotFoundException exception)
        {
            if (Logger.IsError) Logger.Error($"Failed to load one of the dependencies of {precompile.GetType()} precompile", exception);
            throw;
        }
        catch (Exception exception)
        {
            if (Logger.IsError) Logger.Error($"Precompiled contract ({precompile.GetType()}) execution exception", exception);
            CallResult callResult = new(output: default, precompileSuccess: false, fromVersion: 0, shouldRevert: true);
            return callResult;
        }
    }
}