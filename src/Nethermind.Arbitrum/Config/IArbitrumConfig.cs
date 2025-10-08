// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Config;

namespace Nethermind.Arbitrum.Config;

[ConfigCategory(HiddenFromDocs = true)]
public interface IArbitrumConfig : IConfig
{
    [ConfigItem(Description = "Whether safe blocks should wait for validator", DefaultValue = "false")]
    bool SafeBlockWaitForValidator { get; set; }

    [ConfigItem(Description = "Whether finalized blocks should wait for validator", DefaultValue = "false")]
    bool FinalizedBlockWaitForValidator { get; set; }

    [ConfigItem(Description = "Timeout in seconds for block processing operations", DefaultValue = "1")]
    int BlockProcessingTimeout { get; set; }

    [ConfigItem(Description = "Comparison mode interval - compare DigestMessage results with external RPC every N blocks (0 = disabled)", DefaultValue = "0")]
    ulong ComparisonModeInterval { get; set; }

    [ConfigItem(Description = "External RPC URL for comparison mode (e.g., https://sepolia-rollup.arbitrum.io/rpc or https://arb1.arbitrum.io/rpc)", DefaultValue = "")]
    string? ComparisonModeRpcUrl { get; set; }
}
