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
using Nethermind.Logging;
using Nethermind.Int256;

[assembly: InternalsVisibleTo("Nethermind.Arbitrum.Evm.Test")]
namespace Nethermind.Arbitrum.Evm;

public sealed unsafe partial class ArbitrumVirtualMachine(
    IBlockhashProvider? blockHashProvider,
    ISpecProvider? specProvider,
    ILogManager? logManager
) : VirtualMachineBase(blockHashProvider, specProvider, logManager)
{
    public ArbitrumTxExecutionContext ArbitrumTxExecutionContext { get; set; } = new();

    private bool _noBaseFee;
    private UInt256? _originalBaseFee;
    private UInt256? _savedOriginalBaseFee;

    /// <summary>
    /// Configures NoBaseFee behavior to match Nitro's dual base fee system.
    /// When enabled:
    /// - EVM execution (BASEFEE opcode) sees 0
    /// - Gas calculations use the original base fee (stored separately)
    /// This matches Nitro's pattern where blockContext.BaseFee = 0 but BaseFeeInBlock preserves original.
    /// </summary>
    public void SetNoBaseFeeConfig(bool noBaseFee, UInt256? originalBaseFee = null)
    {
        if (noBaseFee && !_noBaseFee && _savedOriginalBaseFee == null)
        {
            // Save the current header BaseFee before we modify it
            _savedOriginalBaseFee = BlockExecutionContext.Header.BaseFeePerGas;
        }

        _noBaseFee = noBaseFee;
        _originalBaseFee = originalBaseFee;

        // Match Nitro: When NoBaseFee is enabled, EVM sees BaseFee = 0
        // This is what the BASEFEE opcode will return
        if (_noBaseFee && _originalBaseFee.HasValue)
        {
            BlockExecutionContext.Header.BaseFeePerGas = UInt256.Zero;
        }
        else if (!_noBaseFee && _savedOriginalBaseFee.HasValue)
        {
            // Restore the original header value when disabling NoBaseFee
            BlockExecutionContext.Header.BaseFeePerGas = _savedOriginalBaseFee.Value;
        }
    }

    /// <summary>
    /// Reset NoBaseFee configuration and restore original header value
    /// </summary>
    public void ResetNoBaseFeeConfig()
    {
        if (_savedOriginalBaseFee.HasValue)
        {
            BlockExecutionContext.Header.BaseFeePerGas = _savedOriginalBaseFee.Value;
        }

        _noBaseFee = false;
        _originalBaseFee = null;
        _savedOriginalBaseFee = null;
    }

    /// <summary>
    /// Returns the original base fee when NoBaseFee is active, for gas calculations.
    /// This matches Nitro's pattern where gas calculations use BaseFeeInBlock (original base fee)
    /// while EVM execution uses BaseFee (0 in NoBaseFee mode).
    /// </summary>
    public UInt256? GetOriginalBaseFeeForGasCalculations()
    {
        return _noBaseFee ? _originalBaseFee : null;
    }

    /// <summary>
    /// Creates a disposable scope for NoBaseFee configuration (automatic cleanup)
    /// </summary>
    public IDisposable UseNoBaseFee(UInt256 originalBaseFee)
    {
        SetNoBaseFeeConfig(true, originalBaseFee);
        return new NoBaseFeeScope(this);
    }

    private sealed class NoBaseFeeScope : IDisposable
    {
        private readonly ArbitrumVirtualMachine _vm;
        public NoBaseFeeScope(ArbitrumVirtualMachine vm) => _vm = vm;
        public void Dispose() => _vm.ResetNoBaseFeeConfig();
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

        var tracingInfo = new TracingInfo(
            TxTracer as IArbitrumTxTracer ?? ArbNullTxTracer.Instance,
            TracingScenario.TracingDuringEvm,
            state.Env
        );

        ArbitrumPrecompileExecutionContext context = new(
            state.From, GasSupplied: (ulong)state.GasAvailable,
            ReadOnly: state.IsStatic, WorldState, BlockExecutionContext,
            ChainId.ToByteArray().ToULongFromBigEndianByteArrayWithoutLeadingZeros(), tracingInfo, Spec
        )
        {
            CurrentRetryable = ArbitrumTxExecutionContext.CurrentRetryable,
            CurrentRefundTo = ArbitrumTxExecutionContext.CurrentRefundTo
        };
        //TODO: temporary fix but should change error management from Exceptions to returning errors instead i think
        bool unauthorizedCallerException = false;
        try
        {
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
            state.GasAvailable = context.ArbosState.CurrentArbosVersion >= ArbosVersion.Eleven ? (long)context.GasLeft : 0;
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
