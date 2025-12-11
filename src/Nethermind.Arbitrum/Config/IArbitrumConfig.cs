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
}
