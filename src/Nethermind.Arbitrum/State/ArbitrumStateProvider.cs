// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Logging;
using Nethermind.State;

namespace Nethermind.Arbitrum.State;

internal class ArbitrumStateProvider(ILogManager logManager) : StateProvider(logManager)
{
    private readonly HashSet<AddressAsKey> _deletedThisBlock = new();
    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumStateProvider>();

    /// <summary>
    /// Creates a zombie account if the address was deleted earlier in this block and has no changes in the current transaction.
    /// Matches Nitro's CreateZombieIfDeleted: only fires when getStateObject(addr)==nil AND addr is in stateObjectsDestruct.
    /// </summary>
    public void CreateEmptyAccountIfDeleted(Address address)
    {
        // HasIntraTxChanges == true means the account has a live object in the current TX,
        // equivalent to Nitro's getStateObject(addr) != nil. In Nitro, zombie creation is strictly cross-TX.
        if (HasIntraTxChanges(address))
            return;

        if (_deletedThisBlock.Contains(address))
        {
            if (_logger.IsTrace)
                _logger.Trace($"Creating zombie account: {address}");

            // We do NOT remove from _deletedThisBlock.
            // Nitro's stateObjectsDestruct is never modified by zombie creation.
            // This allows re-creation after EVM revert.
            CreateEmptyAccount(address);
        }
    }

    public override void Reset(bool resetBlockChanges = true)
    {
        base.Reset(resetBlockChanges);

        if (resetBlockChanges)
            _deletedThisBlock.Clear();
    }

    protected override void OnAccountRemovedFromState(Address address)
    {
        _deletedThisBlock.Add(address);
    }
}
