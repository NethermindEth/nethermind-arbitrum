// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos.Stylus;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumConfig : IArbitrumConfig
{
    public bool SafeBlockWaitForValidator { get; set; } = false;
    public bool FinalizedBlockWaitForValidator { get; set; } = false;
    public int BlockProcessingTimeout { get; set; } = 1;

    public string StylusHostTarget { get; set; } = StylusTargets.HostDescriptor;
    public string StylusArm64Target { get; set; } = StylusTargets.LinuxArm64Descriptor;
    public string StylusAmd64Target { get; set; } = StylusTargets.LinuxX64Descriptor;
    public string StylusExtraArchs { get; set; } = StylusTargets.WavmTargetName;

    public StylusTargetConfig ToStylusTargetConfig()
    {
        return new StylusTargetConfig
        {
            Host = StylusHostTarget,
            Arm64 = StylusArm64Target,
            Amd64 = StylusAmd64Target,
            ExtraArchs = string.IsNullOrWhiteSpace(StylusExtraArchs)
                ? [StylusTargets.WavmTargetName]
                : StylusExtraArchs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        };
    }
}
