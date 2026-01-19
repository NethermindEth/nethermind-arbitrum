// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Stylus;
using Nethermind.Config;

namespace Nethermind.Arbitrum.Config;

[ConfigCategory(HiddenFromDocs = true)]
public interface IArbitrumConfig : IConfig
{
    [ConfigItem(Description = "Timeout in seconds for block processing operations", DefaultValue = "1")]
    public int BlockProcessingTimeout { get; set; }

    [ConfigItem(Description = "Experimental: Expose multi-dimensional gas in transaction receipts", DefaultValue = "false")]
    public bool ExposeMultiGas { get; set; }

    [ConfigItem(Description = "Whether finalized blocks should wait for validator", DefaultValue = "false")]
    public bool FinalizedBlockWaitForValidator { get; set; }

    [ConfigItem(DefaultValue = "1000", Description = "Allowed message lag in milliseconds while still considered in sync")]
    public int MessageLagMs { get; set; }

    [ConfigItem(Description = "Rebuild local WASM store mode: 'false' to disable, 'force' to force rebuild, or other value to continue from last position", DefaultValue = "auto")]
    public WasmRebuildMode RebuildLocalWasm { get; set; }

    [ConfigItem(Description = "Whether safe blocks should wait for validator", DefaultValue = "false")]
    public bool SafeBlockWaitForValidator { get; set; }
}
