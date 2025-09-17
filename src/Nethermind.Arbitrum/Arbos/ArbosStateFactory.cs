// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only
using Nethermind.Logging;


using Nethermind.Arbitrum.Arbos.Storage;

namespace Nethermind.Arbitrum.Arbos;

public class ArbosStateFactory(ArbosStorageFactory storageFactory, ILogManager logManager)
{
    public ArbosState Build(IBurner burner)
    {
        return ArbosState.OpenArbosState(storageFactory.Build(burner), logManager.GetClassLogger<ArbosState>());
    }
}
