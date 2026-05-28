// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Precompiles;

/// <summary>
/// ArbFilteredTransactionsManager precompile allows authorized transaction filterers
/// to mark transaction hashes as filtered (executed as no-ops).
/// Available from ArbOS version 60.
/// </summary>
public static class ArbFilteredTransactionsManager
{
    public static Address Address => ArbosAddresses.ArbFilteredTransactionsManagerAddress;

    private static readonly AbiEventDescription FilteredTransactionAddedEvent =
        Solgen.ArbFilteredTransactionsManager.Events.FilteredTransactionAdded.ToAbiEventDescription();
    private static readonly AbiEventDescription FilteredTransactionDeletedEvent =
        Solgen.ArbFilteredTransactionsManager.Events.FilteredTransactionDeleted.ToAbiEventDescription();

    /// <summary>
    /// Adds a transaction hash to the filtered set. Only callable by transaction filterers.
    /// </summary>
    public static void AddFilteredTransaction(ArbitrumPrecompileExecutionContext context, Hash256 txHash)
    {
        if (!HasAccess(context))
            context.BurnOut();

        new FilteredTransactionsState(context.WorldState, context).Add(txHash);
        EmitFilteredTransactionAddedEvent(context, txHash);
    }

    /// <summary>
    /// Removes a transaction hash from the filtered set. Only callable by transaction filterers.
    /// </summary>
    public static void DeleteFilteredTransaction(ArbitrumPrecompileExecutionContext context, Hash256 txHash)
    {
        if (!HasAccess(context))
            context.BurnOut();

        new FilteredTransactionsState(context.WorldState, context).Delete(txHash);
        EmitFilteredTransactionDeletedEvent(context, txHash);
    }

    /// <summary>
    /// Returns true if the given transaction hash is in the filtered set.
    /// </summary>
    public static bool IsTransactionFiltered(ArbitrumPrecompileExecutionContext context, Hash256 txHash)
    {
        return new FilteredTransactionsState(context.WorldState, context).IsFiltered(txHash);
    }

    private static bool HasAccess(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.TransactionFilterers.IsMember(context.Caller);

    private static void EmitFilteredTransactionAddedEvent(ArbitrumPrecompileExecutionContext context, Hash256 txHash)
    {
        LogEntry log = EventsEncoder.BuildLogEntryFromEvent(FilteredTransactionAddedEvent, Address, txHash.BytesToArray());
        EventsEncoder.EmitEvent(context, log);
    }

    private static void EmitFilteredTransactionDeletedEvent(ArbitrumPrecompileExecutionContext context, Hash256 txHash)
    {
        LogEntry log = EventsEncoder.BuildLogEntryFromEvent(FilteredTransactionDeletedEvent, Address, txHash.BytesToArray());
        EventsEncoder.EmitEvent(context, log);
    }
}
