using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles;

/// <summary>
/// ArbNativeTokenManager precompile enables minting and burning native tokens.
/// Available from ArbOS version 41.
/// </summary>
public static class ArbNativeTokenManager
{
    public static Address Address => ArbosAddresses.ArbNativeTokenManagerAddress;

    public static readonly string Abi =
        "[{\"inputs\":[{\"internalType\":\"uint256\",\"name\":\"amount\",\"type\":\"uint256\"}],\"name\":\"burnNativeToken\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint256\",\"name\":\"amount\",\"type\":\"uint256\"}],\"name\":\"mintNativeToken\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"address\",\"name\":\"from\",\"type\":\"address\"},{\"indexed\":false,\"internalType\":\"uint256\",\"name\":\"amount\",\"type\":\"uint256\"}],\"name\":\"NativeTokenBurned\",\"type\":\"event\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"address\",\"name\":\"to\",\"type\":\"address\"},{\"indexed\":false,\"internalType\":\"uint256\",\"name\":\"amount\",\"type\":\"uint256\"}],\"name\":\"NativeTokenMinted\",\"type\":\"event\"}]";

    public static readonly AbiEventDescription NativeTokenMintedEvent;
    public static readonly AbiEventDescription NativeTokenBurnedEvent;

    static ArbNativeTokenManager()
    {
        Dictionary<string, AbiEventDescription> allEvents = AbiMetadata.GetAllEventDescriptions(Abi)!;
        NativeTokenMintedEvent = allEvents["NativeTokenMinted"];
        NativeTokenBurnedEvent = allEvents["NativeTokenBurned"];
    }

    /// <summary>
    /// Mints some amount of the native gas token for this chain to the caller
    /// </summary>
    public static void MintNativeToken(ArbitrumPrecompileExecutionContext context, UInt256 amount)
    {
        if (!HasAccess(context))
        {
            throw ArbitrumPrecompileException.CreateRevertException("only native token owners can mint native token");
        }

        Address caller = context.Caller;
        ArbitrumTransactionProcessor.MintBalance(caller, amount, context.ArbosState, context.WorldState,
            context.ReleaseSpec, context.TracingInfo);

        EmitNativeTokenMintedEvent(context, caller, amount);
    }

    /// <summary>
    /// Burns some amount of the native gas token for this chain from the caller
    /// </summary>
    public static void BurnNativeToken(ArbitrumPrecompileExecutionContext context, UInt256 amount)
    {
        if (!HasAccess(context))
        {
            throw ArbitrumPrecompileException.CreateRevertException("only native token owners can burn native token");
        }

        Address caller = context.Caller;
        UInt256 balance = context.WorldState.GetBalance(caller);

        if (balance < amount)
        {
            throw ArbitrumPrecompileException.CreateRevertException("burn amount exceeds balance");
        }

        ArbitrumTransactionProcessor.BurnBalance(caller, amount, context.ArbosState, context.WorldState,
            context.ReleaseSpec, context.TracingInfo!);

        EmitNativeTokenBurnedEvent(context, caller, amount);
    }

    private static bool HasAccess(ArbitrumPrecompileExecutionContext context)
    {
        return context.ArbosState.NativeTokenOwners.IsMember(context.Caller);
    }

    private static void EmitNativeTokenMintedEvent(ArbitrumPrecompileExecutionContext context, Address to, UInt256 amount)
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(NativeTokenMintedEvent, Address, to, amount);
        EventsEncoder.EmitEvent(context, eventLog);
    }

    private static void EmitNativeTokenBurnedEvent(ArbitrumPrecompileExecutionContext context, Address from, UInt256 amount)
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(NativeTokenBurnedEvent, Address, from, amount);
        EventsEncoder.EmitEvent(context, eventLog);
    }
}
