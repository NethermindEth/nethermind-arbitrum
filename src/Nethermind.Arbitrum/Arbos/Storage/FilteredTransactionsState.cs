// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Evm;
using Nethermind.Core.Crypto;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Arbos.Storage;

/// <summary>
/// Tracks filtered transactions that should be executed as no-ops.
/// Uses a dedicated storage account (FilteredTransactionsStateAddress) rather than a subspace.
/// </summary>
public class FilteredTransactionsState
{
    private static readonly ValueHash256 PresentHash = new(new byte[32] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 });

    private readonly ArbosStorage _storage;

    public FilteredTransactionsState(IWorldState worldState, IBurner burner) =>
        _storage = new ArbosStorage(worldState, burner, ArbosAddresses.FilteredTransactionsStateAddress);

    /// <summary>
    /// Adds a transaction hash to the filtered set.
    /// </summary>
    public void Add(Hash256 txHash) => _storage.Set(txHash, PresentHash);

    /// <summary>
    /// Removes a transaction hash from the filtered set.
    /// </summary>
    public void Delete(Hash256 txHash) => _storage.Clear(txHash);

    /// <summary>
    /// Checks if a transaction hash is in the filtered set.
    /// </summary>
    public bool IsFiltered(Hash256 txHash)
    {
        ValueHash256 value = _storage.Get(txHash);
        return value == PresentHash;
    }

    /// <summary>
    /// Checks if a transaction hash is in the filtered set without charging gas.
    /// </summary>
    public bool IsFilteredFree(Hash256 txHash)
    {
        ValueHash256 value = _storage.GetFree(txHash);
        return value == PresentHash;
    }

    /// <summary>
    /// Removes a transaction hash from the filtered set without charging gas.
    /// This is called after a filtered tx is executed as a no-op to clean up.
    /// </summary>
    public void DeleteFree(Hash256 txHash) => _storage.ClearFree(txHash);
}
