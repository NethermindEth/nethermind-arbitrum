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

    [ConfigItem(Description = "Stylus target configuration for WASM compilation", DefaultValue = "")]
    string StylusHostTarget { get; set; }

    [ConfigItem(Description = "Stylus ARM64 target descriptor", DefaultValue = "arm64-linux-unknown+neon")]
    string StylusArm64Target { get; set; }

    [ConfigItem(Description = "Stylus AMD64 target descriptor", DefaultValue = "x86_64-linux-unknown+sse4.2+lzcnt+bmi")]
    string StylusAmd64Target { get; set; }

    [ConfigItem(Description = "Additional Stylus architecture targets", DefaultValue = "wavm")]
    string StylusExtraArchs { get; set; }
}
