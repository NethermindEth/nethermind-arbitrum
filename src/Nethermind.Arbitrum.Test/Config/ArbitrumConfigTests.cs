// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos.Stylus;
using Nethermind.Arbitrum.Config;
using NUnit.Framework;

namespace Nethermind.Arbitrum.Test.Config;

[TestFixture]
public class ArbitrumConfigTests
{
    [Test]
    public void ToStylusTargetConfig_WithDefaultValues_ReturnsCorrectConfig()
    {
        ArbitrumConfig config = new();

        StylusTargetConfig stylusConfig = config.ToStylusTargetConfig();

        Assert.That(stylusConfig.Host, Is.EqualTo(StylusTargets.HostDescriptor));
        Assert.That(stylusConfig.Arm64, Is.EqualTo(StylusTargets.LinuxArm64Descriptor));
        Assert.That(stylusConfig.Amd64, Is.EqualTo(StylusTargets.LinuxX64Descriptor));
        Assert.That(stylusConfig.ExtraArchs, Is.EquivalentTo(new[] { StylusTargets.WavmTargetName }));
    }

    [Test]
    public void ToStylusTargetConfig_WithCustomValues_ReturnsCorrectConfig()
    {
        ArbitrumConfig config = new()
        {
            StylusHostTarget = "custom-host",
            StylusArm64Target = "custom-arm64",
            StylusAmd64Target = "custom-amd64",
            StylusExtraArchs = "wavm,custom1,custom2"
        };

        StylusTargetConfig stylusConfig = config.ToStylusTargetConfig();

        Assert.That(stylusConfig.Host, Is.EqualTo("custom-host"));
        Assert.That(stylusConfig.Arm64, Is.EqualTo("custom-arm64"));
        Assert.That(stylusConfig.Amd64, Is.EqualTo("custom-amd64"));
        Assert.That(stylusConfig.ExtraArchs, Is.EquivalentTo(new[] { "wavm", "custom1", "custom2" }));
    }

    [Test]
    public void ToStylusTargetConfig_WithEmptyExtraArchs_ReturnsDefaultWavm()
    {
        ArbitrumConfig config = new()
        {
            StylusExtraArchs = ""
        };

        StylusTargetConfig stylusConfig = config.ToStylusTargetConfig();

        Assert.That(stylusConfig.ExtraArchs, Is.EquivalentTo(new[] { StylusTargets.WavmTargetName }));
    }

    [Test]
    public void ToStylusTargetConfig_WithWhitespaceExtraArchs_ReturnsDefaultWavm()
    {
        ArbitrumConfig config = new()
        {
            StylusExtraArchs = "   "
        };

        StylusTargetConfig stylusConfig = config.ToStylusTargetConfig();

        Assert.That(stylusConfig.ExtraArchs, Is.EquivalentTo(new[] { StylusTargets.WavmTargetName }));
    }
}
