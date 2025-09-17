// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class ArbosStorageFactory(IWorldState worldState, Address accountAddress, byte[]? storageKey = null)
{
    private readonly IWorldState _db = worldState ?? throw new ArgumentNullException(nameof(worldState));
    private readonly Address _account = accountAddress ?? throw new ArgumentNullException(nameof(accountAddress));
    // Never null, empty for root
    private readonly byte[] _storageKey = storageKey ?? []; // TODO: Fix to be ValueHash256

    public ArbosStorage Build(IBurner burner)
    {
        return new ArbosStorage(_db, burner, _account, _storageKey);
    }
}
