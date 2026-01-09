using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Core;
using Nethermind.Evm;

namespace Nethermind.Arbitrum.Precompiles;

public class OwnerLogic
{
    public static void EmitOwnerSuccessEvent(VmState<ArbitrumGas> state, ArbitrumPrecompileExecutionContext context, IArbitrumPrecompile precompile)
    {
        ReadOnlyMemory<byte> callData = state.Env.InputData;

        ReadOnlySpan<byte> fullCalldata = callData.Span;
        ReadOnlySpan<byte> modifyableCalldata = callData.Span;
        ReadOnlySpan<byte> methodId = ArbitrumBinaryReader.ReadBytesOrFail(ref modifyableCalldata, 4);

        switch (precompile)
        {
            case ArbOwnerParser _:
                LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(ArbOwner.OwnerActsEvent, ArbOwner.Address, methodId.ToArray(), context.Caller, fullCalldata.ToArray());
                state.AccessTracker.Logs.Add(eventLog);
                break;
            default:
                throw new ArgumentException($"EmitSuccessEvent is not registered for precompile: {precompile.GetType()}");
        }
    }
}
