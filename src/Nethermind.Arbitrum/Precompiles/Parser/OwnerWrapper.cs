using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Precompiles.Parser;

// OwnerWrapper is a precompile wrapper for those only chain owners may use
public class OwnerWrapper<T>(T wrappedPrecompile, AbiEventDescription successEvent) : IArbitrumPrecompile
    where T : IArbitrumPrecompile
{
    public bool IsOwner => true;

    private readonly AbiEventDescription SuccessEvent = successEvent;

    private void EmitSuccessEvent(ArbitrumPrecompileExecutionContext context, byte[] methodCalled, Address owner, byte[] methodData)
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(SuccessEvent, T.Address, methodCalled, owner, methodData);
        EventsEncoder.EmitEvent(context, eventLog);
    }

    public byte[] RunAdvanced(ArbitrumPrecompileExecutionContext context, ReadOnlyMemory<byte> inputData)
    {
        if (!context.ArbosState.ChainOwners.IsMember(context.Caller))
            throw new InvalidOperationException("Unauthorized caller to access-controlled method");

        byte[] output = wrappedPrecompile.RunAdvanced(context, inputData);

        if (!context.ReadOnly || context.ArbosState.CurrentArbosVersion < ArbosVersion.Eleven)
        {
            ReadOnlySpan<byte> calldata = inputData.Span;
            ReadOnlySpan<byte> methodId = ArbitrumBinaryReader.ReadBytesOrFail(ref calldata, 4);

            EmitSuccessEvent(context, methodId.ToArray(), context.Caller, calldata.ToArray());
        }

        return output;
    }
}