// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm.State;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles;

// All calls to this precompile are authorized by a debug-only wrapping function in the vm,
// which ensures these methods are not accessible in production.
public static class ArbDebug
{
    public static Address Address => ArbosAddresses.ArbDebugAddress;

    // Events
    public static readonly AbiEventDescription Basic;
    public static readonly AbiEventDescription Mixed;
    public static readonly AbiEventDescription Store;

    // Solidity errors
    public static readonly AbiErrorDescription Custom;
    public static readonly AbiErrorDescription Unused;

    static ArbDebug()
    {
        Basic = Solgen.ArbDebug.Events.Basic.ToAbiEventDescription();
        Mixed = Solgen.ArbDebug.Events.Mixed.ToAbiEventDescription();
        Store = Solgen.ArbDebug.Events.Store.ToAbiEventDescription();

        Custom = Solgen.ArbDebug.Errors.Custom.ToAbiErrorDescription();
        Unused = Solgen.ArbDebug.Errors.Unused.ToAbiErrorDescription();
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

    public static byte[] OverwriteContractCode(ArbitrumPrecompileExecutionContext context, Address addr, byte[] code)
    {
        byte[] oldCode = context.WorldState.GetCode(addr) ?? [];

        // In Go-Ethereum, SetCode → getOrNewStateObject → createObject (if needed)
        // In Nethermind, we explicitly call CreateAccountIfNotExists to match that behavior
        context.WorldState.CreateAccountIfNotExists(addr, UInt256.Zero);

        context.WorldState.InsertCode(addr, code, context.ReleaseSpec);

        return oldCode;
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
