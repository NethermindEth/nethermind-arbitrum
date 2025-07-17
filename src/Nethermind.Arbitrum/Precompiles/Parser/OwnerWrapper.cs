using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Core;
using Nethermind.Evm;
using Nethermind.Logging;

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
        EventsEncoder.EmitEvent(context, eventLog, isFree: true);
    }

    public byte[] RunAdvanced(ArbitrumPrecompileExecutionContext context, ReadOnlyMemory<byte> inputData)
    {
        SystemBurner freeBurner = new();
        ArbosState freeArbosState = ArbosState.OpenArbosState(context.WorldState, freeBurner, NullLogger.Instance);
        ulong before = freeBurner.Burned;
        if (!freeArbosState.ChainOwners.IsMember(context.Caller))
        {
            context.Burn(freeBurner.Burned - before); // non-owner has to pay for this IsMember operation
            throw OwnerWrapper.UnauthorizedCallerException();
        }

        // Burn gas for argument data supplied (excluding method id)
        ulong dataGasCost = GasCostOf.DataCopy * Math.Utils.Div32Ceiling((ulong)inputData.Length - 4);
        context.Burn(dataGasCost);

        byte[] output = wrappedPrecompile.RunAdvanced(context, inputData);

        if (!context.ReadOnly || context.ArbosState.CurrentArbosVersion < ArbosVersion.Eleven)
        {
            ReadOnlySpan<byte> fullCalldata = inputData.Span;
            ReadOnlySpan<byte> modifyableCalldata = inputData.Span;
            ReadOnlySpan<byte> methodId = ArbitrumBinaryReader.ReadBytesOrFail(ref modifyableCalldata, 4);


            EmitSuccessEvent(context, methodId.ToArray(), context.Caller, fullCalldata.ToArray());
        }

        return output;
    }
}

public static class OwnerWrapper
{
    public static InvalidOperationException UnauthorizedCallerException()
        => new("Unauthorized caller to access-controlled method");
}

