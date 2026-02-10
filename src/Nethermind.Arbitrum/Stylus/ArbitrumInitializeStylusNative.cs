// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Api.Steps;
using Nethermind.Arbitrum.Arbos.Stylus;

namespace Nethermind.Arbitrum.Stylus;

public class ArbitrumInitializeStylusNative(IStylusTargetConfig api) : IStep
{
    public Task Execute(CancellationToken cancellationToken)
    {
        IStylusTargetConfig config = api;

        StylusNative.SetWasmLruCacheCapacity(Math.Utils.SaturateMul(config.NativeLruCacheCapacityMb, 1024 * 1024ul));
        PopulateStylusTargetCache(config);

        return Task.CompletedTask;
    }

    private static void PopulateStylusTargetCache(IStylusTargetConfig config)
    {
        string localTarget = StylusTargets.GetLocalTargetName();
        IReadOnlyCollection<string> targets = config.GetWasmTargets();

        bool nativeSet = false;
        foreach (string target in targets)
        {
            if (target == StylusTargets.WavmTargetName) // WAVM is unknown target for WASM compiler (wasmer) and handled separately
                continue;

            string effectiveStylusTarget = target switch
            {
                StylusTargets.Amd64TargetName => config.Amd64,
                StylusTargets.Arm64TargetName => config.Arm64,
                StylusTargets.HostTargetName => config.Host,
                _ => throw new PlatformNotSupportedException($"Unsupported stylus target: {target}")
            };

            bool isNative = target == localTarget;
            StylusNativeResult<byte[]> nativeResult = StylusNative.SetTarget(target, effectiveStylusTarget, isNative);
            if (!nativeResult.IsSuccess)
                throw new InvalidOperationException($"Failed to set target {target} with descriptor {effectiveStylusTarget}: {nativeResult.Error}");

            nativeSet = nativeSet || isNative;
        }

        if (!nativeSet)
            throw new InvalidOperationException($"Local target {localTarget} missing in list of archs {string.Join(", ", targets)}");
    }
}
