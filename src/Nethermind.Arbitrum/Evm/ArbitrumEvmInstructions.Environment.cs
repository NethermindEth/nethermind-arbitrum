using System.Runtime.CompilerServices;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;
using Nethermind.Evm;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Evm;

internal static class ArbitrumEvmInstructions
{
    /// <summary>
    /// Executes an environment introspection opcode that returns a UInt256 value.
    /// </summary>
    /// <param name="vm">The virtual machine instance.</param>
    /// <param name="stack">The execution stack.</param>
    /// <param name="gasAvailable">The available gas which is reduced by the operation's cost.</param>
    /// <param name="programCounter">The program counter.</param>
    /// <returns>An EVM exception type if an error occurs.</returns>
    [SkipLocalsInit]
    public static EvmExceptionType InstructionBlkUInt256<TTracingInst>(VirtualMachineBase vm, ref EvmStack stack, ref long gasAvailable, ref int programCounter)
        where TTracingInst : struct, IFlag
    {
        gasAvailable -= OpGasPrice.GasCost;

        ref readonly UInt256 result = ref OpGasPrice.Operation(vm);

        stack.PushUInt256<TTracingInst>(in result);

        return EvmExceptionType.None;
    }

    /// <summary>
    /// Returns the gas price for the transaction.
    /// </summary>
    public struct OpGasPrice
    {
        public static long GasCost => GasCostOf.Base;

        public static ref readonly UInt256 Operation(VirtualMachineBase vm)
        {
            ArbitrumVirtualMachine arbvm = (ArbitrumVirtualMachine)vm;

            if (arbvm.FreeArbosState.CurrentArbosVersion < ArbosVersion.Three ||
                arbvm.FreeArbosState.CurrentArbosVersion == ArbosVersion.Nine)
            {
                return ref vm.TxExecutionContext.GasPrice;
            }

            return ref vm.BlockExecutionContext.Header.BaseFeePerGas;
        }
    }
}
