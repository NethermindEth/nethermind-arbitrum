// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Arbitrum
{
    public class ArbitrumConfig : IArbitrumConfig
    {
        public bool Enabled { get; set; } = true;
        public ulong GenesisBlockNum { get; set; } = 0;
    }
}
