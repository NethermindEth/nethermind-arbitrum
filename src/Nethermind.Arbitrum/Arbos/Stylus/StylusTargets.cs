// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;

namespace Nethermind.Arbitrum.Arbos.Stylus;

public static class StylusTargets
{
    public const string HostTargetName = "";

    public const string LinuxX64Descriptor = "x86_64-linux-unknown+sse4.2+lzcnt+bmi";
    public const string LinuxArm64Descriptor = "arm64-linux-unknown+neon";

    public const string MacOsX64Descriptor = "x86_64-apple-darwin-unknown+sse4.2+lzcnt+bmi";
    public const string MacOsArm64Descriptor = "aarch64-apple-darwin-unknown+neon";

    public const string WindowsGnuX64Descriptor = "x86_64-pc-windows-gnu-unknown+sse4.2+lzcnt+bmi";

    public static string GetCurrentTarget()
    {
        string arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        return arch switch
        {
            "x64" when OperatingSystem.IsLinux() => LinuxX64Descriptor,
            "arm64" when OperatingSystem.IsLinux() => LinuxArm64Descriptor,
            "x64" when OperatingSystem.IsMacOS() => MacOsX64Descriptor,
            "arm64" when OperatingSystem.IsMacOS() => MacOsArm64Descriptor,
            "x64" when OperatingSystem.IsWindows() => WindowsGnuX64Descriptor,
            _ => throw new PlatformNotSupportedException($"Unsupported OS or architecture: {RuntimeInformation.OSDescription} {arch}")
        };
    }
}
