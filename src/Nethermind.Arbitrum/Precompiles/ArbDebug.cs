using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles;

// All calls to this precompile are authorized by the DebugPrecompile wrapper,
// which ensures these methods are not accessible in production.
public static class ArbDebug
{
    public static Address Address => ArbosAddresses.ArbDebugAddress;

    public static readonly string Abi =
        "[{\"inputs\":[],\"name\":\"becomeChainOwner\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint64\",\"name\":\"number\",\"type\":\"uint64\"}],\"name\":\"customRevert\",\"outputs\":[],\"stateMutability\":\"pure\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bool\",\"name\":\"flag\",\"type\":\"bool\"},{\"internalType\":\"bytes32\",\"name\":\"value\",\"type\":\"bytes32\"}],\"name\":\"events\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"\",\"type\":\"address\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"payable\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"eventsView\",\"outputs\":[],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"legacyError\",\"outputs\":[],\"stateMutability\":\"pure\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"panic\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":false,\"internalType\":\"bool\",\"name\":\"flag\",\"type\":\"bool\"},{\"indexed\":true,\"internalType\":\"bytes32\",\"name\":\"value\",\"type\":\"bytes32\"}],\"name\":\"Basic\",\"type\":\"event\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"bool\",\"name\":\"flag\",\"type\":\"bool\"},{\"indexed\":false,\"internalType\":\"bool\",\"name\":\"not\",\"type\":\"bool\"},{\"indexed\":true,\"internalType\":\"bytes32\",\"name\":\"value\",\"type\":\"bytes32\"},{\"indexed\":false,\"internalType\":\"address\",\"name\":\"conn\",\"type\":\"address\"},{\"indexed\":true,\"internalType\":\"address\",\"name\":\"caller\",\"type\":\"address\"}],\"name\":\"Mixed\",\"type\":\"event\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"bool\",\"name\":\"flag\",\"type\":\"bool\"},{\"indexed\":true,\"internalType\":\"address\",\"name\":\"field\",\"type\":\"address\"},{\"indexed\":false,\"internalType\":\"uint24\",\"name\":\"number\",\"type\":\"uint24\"},{\"indexed\":false,\"internalType\":\"bytes32\",\"name\":\"value\",\"type\":\"bytes32\"},{\"indexed\":false,\"internalType\":\"bytes\",\"name\":\"store\",\"type\":\"bytes\"}],\"name\":\"Store\",\"type\":\"event\"},{\"inputs\":[{\"internalType\":\"uint64\",\"name\":\"\",\"type\":\"uint64\"},{\"internalType\":\"string\",\"name\":\"\",\"type\":\"string\"},{\"internalType\":\"bool\",\"name\":\"\",\"type\":\"bool\"}],\"name\":\"Custom\",\"type\":\"error\"},{\"inputs\":[],\"name\":\"Unused\",\"type\":\"error\"}]";

    // Events
    public static readonly AbiEventDescription Basic;
    public static readonly AbiEventDescription Mixed;
    public static readonly AbiEventDescription Store;

    // Solidity errors
    public static readonly AbiErrorDescription Custom;
    public static readonly AbiErrorDescription Unused;

    static ArbDebug()
    {
        Dictionary<string, AbiEventDescription> allEvents = AbiMetadata.GetAllEventDescriptions(Abi)!;
        Basic = allEvents["Basic"];
        Mixed = allEvents["Mixed"];
        Store = allEvents["Store"];

        Dictionary<string, AbiErrorDescription> allErrors = AbiMetadata.GetAllErrorDescriptions(Abi)!;
        Custom = allErrors["Custom"];
        Unused = allErrors["Unused"];
    }

    public static void EmitBasicEvent(ArbitrumPrecompileExecutionContext context, bool flag, Hash256 value)
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(Basic, Address, flag, value);
        EventsEncoder.EmitEvent(context, eventLog);
    }

    public static void EmitMixedEvent(ArbitrumPrecompileExecutionContext context, bool flag, bool not, Hash256 value, Address conn, Address caller)
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(Mixed, Address, flag, not, value, conn, caller);
        EventsEncoder.EmitEvent(context, eventLog);
    }

    public static void EmitStoreEvent(ArbitrumPrecompileExecutionContext context, bool flag, Address field, uint number, Hash256 value, byte[] store)
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(Store, Address, flag, field, number, value, store);
        EventsEncoder.EmitEvent(context, eventLog);
    }

    public static ArbitrumPrecompileException CustomSolidityError(ulong number, string message, bool flag)
    {
        byte[] errorData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            new AbiSignature(Custom.Name, Custom.Inputs.Select(p => p.Type).ToArray()),
            [number, message, flag]
        );
        return ArbitrumPrecompileException.CreateSolidityException(errorData);
    }

    public static ArbitrumPrecompileException UnusedSolidityError()
    {
        byte[] errorData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            new AbiSignature(Unused.Name, Unused.Inputs.Select(p => p.Type).ToArray()),
            []
        );
        return ArbitrumPrecompileException.CreateSolidityException(errorData);
    }

    // Emits events with values based on the args provided
    public static (Address, UInt256) Events(ArbitrumPrecompileExecutionContext context, UInt256 paid, bool flag, Hash256 value)
    {
        // Emits 2 events that cover each case
        //   Basic tests an index'd value & a normal value
        //   Mixed interleaves index'd and normal values that may need to be padded

        EmitBasicEvent(context, !flag, value);

        EmitMixedEvent(context, flag, !flag, value, Address, context.Caller);

        return (context.Caller, paid);
    }

    // Tries (and fails) to emit logs in a view context
    public static void EventsView(ArbitrumPrecompileExecutionContext context)
    {
        Events(context, UInt256.Zero, true, Hash256.Zero);
    }

    // Throws a custom error
    public static void CustomRevert(ArbitrumPrecompileExecutionContext context, ulong number)
    {
        throw CustomSolidityError(number, "This spider family wards off bugs: /\\oo/\\ //\\(oo)//\\ /\\oo/\\", true);
    }

    // Caller becomes a chain owner
    public static void BecomeChainOwner(ArbitrumPrecompileExecutionContext context)
    {
        context.ArbosState.ChainOwners.Add(context.Caller);
    }

    // Halts the chain by panicking in the STF
    public static void Panic(ArbitrumPrecompileExecutionContext context)
    {
        // We can't really panic like nitro, as we already use exceptions for error handling :s
        // Nitro just crashes the whole tx rpc request here. Anyway, this function is only called in debug mode (not prod)
        throw ArbitrumPrecompileException.CreateFailureException("called ArbDebug's debug-only Panic method");
    }

    // Throws a hardcoded error
    public static void LegacyError(ArbitrumPrecompileExecutionContext context)
    {
        throw ArbitrumPrecompileException.CreateFailureException("example legacy error");
    }
}
