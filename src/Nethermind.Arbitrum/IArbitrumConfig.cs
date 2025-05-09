// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Config;

namespace Nethermind.Arbitrum
{
    public interface IArbitrumConfig : IConfig
    {
        [ConfigItem(Description = "Whether to enable the Arbitrum endpoints.", DefaultValue = "false")]
        bool Enabled { get; set; }

        [ConfigItem(Description = "Genesis block number.", DefaultValue = "0")]
        public ulong GenesisBlockNum { get; set; }
    }
}
