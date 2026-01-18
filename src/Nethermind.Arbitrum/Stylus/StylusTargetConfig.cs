// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos.Stylus;
using Nethermind.Config;

namespace Nethermind.Arbitrum.Stylus;

public interface IStylusTargetConfig : IConfig
{
    public string Amd64 { get; set; }
    public string Arm64 { get; set; }
    public string[] ExtraArchs { get; set; }
    public string Host { get; set; }
    public uint NativeLruCacheCapacityMb { get; set; }

    public IReadOnlyCollection<string> GetWasmTargets();
}

public class StylusTargetConfig : IStylusTargetConfig
{
    public string Amd64 { get; set; } = StylusTargets.LinuxX64Descriptor;
    public string Arm64 { get; set; } = StylusTargets.LinuxArm64Descriptor;
    public string[] ExtraArchs { get; set; } = [StylusTargets.WavmTargetName];
    public string Host { get; set; } = StylusTargets.HostDescriptor;
    public uint NativeLruCacheCapacityMb { get; set; } = 256;

    public IReadOnlyCollection<string> GetWasmTargets()
    {
        HashSet<string> targets = [StylusTargets.GetLocalTargetName()];
        foreach (string arch in ExtraArchs)
        {
            if (!WasmStoreSchema.IsSupportedWasmTarget(arch))
                throw new ArgumentException($"Unsupported WASM target {arch}. Supported targets are: {string.Join(", ", StylusTargets.GetAllSupportedWasmTargets())}");

            targets.Add(arch);
        }

        // Ensure targets are always have the same order... from Nitro
        return targets.Order().ToArray();
    }
}
