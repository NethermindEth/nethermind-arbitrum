// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Config;

namespace Nethermind.Arbitrum.Config;

[ConfigCategory(Description = "Configuration for block hash verification against external Arbitrum RPC")]
public interface IVerifyBlockHashConfig : IConfig
{
    [ConfigItem(Description = "External Arbitrum RPC URL for verification (e.g., https://sepolia-rollup.arbitrum.io/rpc or https://arb1.arbitrum.io/rpc)", DefaultValue = "")]
    public string? ArbNodeRpcUrl { get; set; }
    [ConfigItem(Description = "Enable block hash verification", DefaultValue = "false")]
    public bool Enabled { get; set; }

    [ConfigItem(Description = "Verify every N blocks (e.g., 10000 = verify every 10000th block)", DefaultValue = "10000")]
    public ulong VerifyEveryNBlocks { get; set; }
}
