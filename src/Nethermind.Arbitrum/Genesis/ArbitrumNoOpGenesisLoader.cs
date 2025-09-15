// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Consensus.Processing;

namespace Nethermind.Arbitrum.Genesis;

public class ArbitrumNoOpGenesisLoader : IGenesisLoader
{
    public void Load()
    {
        // Do nothing - genesis will be loaded via DigestInitMessage
    }
}
