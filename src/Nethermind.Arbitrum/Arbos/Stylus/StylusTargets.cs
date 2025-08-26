// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;

namespace Nethermind.Arbitrum.Arbos.Stylus;

public static class StylusTargets
{
    public const string HostTargetName = "host";
    public const string WavmTargetName = "wavm";
    public const string Amd64TargetName = "amd64";
    public const string Arm64TargetName = "arm64";

    public const string HostDescriptor = "";
    public const string LinuxX64Descriptor = "x86_64-linux-unknown+sse4.2+lzcnt+bmi";
    public const string LinuxArm64Descriptor = "arm64-linux-unknown+neon";

    public const string MacOsX64Descriptor = "x86_64-apple-darwin-unknown+sse4.2+lzcnt+bmi";
    public const string MacOsArm64Descriptor = "aarch64-apple-darwin-unknown+neon";

    public const string WindowsGnuX64Descriptor = "x86_64-pc-windows-gnu-unknown+sse4.2+lzcnt+bmi";

    public static string GetLocalTargetName()
    {
        string arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        return arch switch
        {
            "x64" when OperatingSystem.IsLinux() => Amd64TargetName,
            "arm64" when OperatingSystem.IsLinux() => Arm64TargetName,
            _ => HostTargetName
        };
    }

    public static string GetLocalDescriptor()
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

    public static void PopulateStylusTargetCache(StylusTargetConfig config)
    {
        string localTarget = GetLocalTargetName();
        IReadOnlyCollection<string> targets = config.GetWasmTargets();

        bool nativeSet = false;
        foreach (string target in targets)
        {
            if (target == WavmTargetName) // WAVM is unknown target for WASM compiler (wasmer) and handled separately
                continue;

            string effectiveStylusTarget = target switch
            {
                Amd64TargetName => config.Amd64,
                Arm64TargetName => config.Arm64,
                HostTargetName => config.Host,
                _ => throw new PlatformNotSupportedException($"Unsupported stylus target: {target}")
            };

            bool isNative = target == localTarget;
            StylusResult<byte[]> result = StylusNative.SetTarget(target, effectiveStylusTarget, isNative);
            if (!result.IsSuccess)
                throw new InvalidOperationException($"Failed to set target {target} with descriptor {effectiveStylusTarget}: {result.Error}");

            nativeSet = nativeSet || isNative;
        }

        if (!nativeSet)
            throw new InvalidOperationException($"Local target {localTarget} missing in list of archs {string.Join(", ", targets)}");
    }
}

public class StylusTargetConfig
{
    public string Host { get; set; } = StylusTargets.HostDescriptor;
    public string Arm64 { get; set; } = StylusTargets.LinuxArm64Descriptor;
    public string Amd64 { get; set; } = StylusTargets.LinuxX64Descriptor;
    public string[] ExtraArchs { get; set; } = [StylusTargets.WavmTargetName];

    public IReadOnlyCollection<string> GetWasmTargets()
    {
        HashSet<string> targets = [.. ExtraArchs, StylusTargets.GetLocalTargetName()];
        return targets;
    }
}
