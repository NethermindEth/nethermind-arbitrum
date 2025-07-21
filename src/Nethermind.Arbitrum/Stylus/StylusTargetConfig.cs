// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos.Stylus;
using Nethermind.Config;

namespace Nethermind.Arbitrum.Stylus;

public interface IStylusTargetConfig : IConfig
{
    string Host { get; set; }
    string Arm64 { get; set; }
    string Amd64 { get; set; }
    string[] ExtraArchs { get; set; }
    uint NativeLruCacheCapacityMb { get; set; }

    IReadOnlyCollection<string> GetWasmTargets();
}

public class StylusTargetConfig : IStylusTargetConfig
{
    public string Host { get; set; } = StylusTargets.HostDescriptor;
    public string Arm64 { get; set; } = StylusTargets.LinuxArm64Descriptor;
    public string Amd64 { get; set; } = StylusTargets.LinuxX64Descriptor;
    public string[] ExtraArchs { get; set; } = [StylusTargets.WavmTargetName];
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
        return targets.OrderBy(t => t).ToArray();
    }
}
