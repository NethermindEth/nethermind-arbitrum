// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Stylus;
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

    [ConfigItem(Description = "Rebuild local WASM store mode: 'false' to disable, 'force' to force rebuild, or other value to continue from last position", DefaultValue = "auto")]
    WasmRebuildMode RebuildLocalWasm { get; set; }

    [ConfigItem(DefaultValue = "1000", Description = "Allowed message lag in milliseconds while still considered in sync")]
    int MessageLagMs { get; set; }

    [ConfigItem(Description = "Experimental: Expose multi-dimensional gas in transaction receipts", DefaultValue = "false")]
    bool ExposeMultiGas { get; set; }

    [ConfigItem(Description = "Duration threshold in milliseconds before flush maintenance is suggested. Set to 0 to disable.", DefaultValue = "0")]
    long TrieTimeLimitBeforeFlushMaintenanceMs { get; set; }

    [ConfigItem(Description = "Random offset range in milliseconds for flush timing to prevent coordinated flushes across nodes. Set to 0 to disable.", DefaultValue = "0")]
    long TrieTimeLimitRandomOffsetMs { get; set; }

    [ConfigItem(Description = "Maximum block processing time in milliseconds before trie is written to disk.", DefaultValue = "3600000")]
    long TrieTimeLimitMs { get; set; }
}
