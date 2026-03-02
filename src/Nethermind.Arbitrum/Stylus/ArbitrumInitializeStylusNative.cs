// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Runtime.InteropServices;
using Nethermind.Api.Steps;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Stylus;

public class ArbitrumInitializeStylusNative(IStylusTargetConfig config, ILogManager? logManager = null) : IStep
{
    private const string StylusLibraryName = "stylus";
    private readonly ILogger _logger = (logManager ?? NullLogManager.Instance).GetClassLogger<ArbitrumInitializeStylusNative>();

    public Task Execute(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        VerifyStylusNativeLibrary();

        cancellationToken.ThrowIfCancellationRequested();

        StylusNative.SetWasmLruCacheCapacity(Math.Utils.SaturateMul(config.NativeLruCacheCapacityMb, 1024 * 1024ul));
        PopulateStylusTargetCache(config);

        return Task.CompletedTask;
    }

    private void VerifyStylusNativeLibrary()
    {
        string localTarget = StylusTargets.GetLocalTargetName();

        if (!NativeLibrary.TryLoad(StylusLibraryName, typeof(StylusNative).Assembly,
                DllImportSearchPath.AssemblyDirectory, out nint handle))
        {
            string expectedFile = GetExpectedLibraryFileName();
            throw new InvalidOperationException(
                $"Failed to load Stylus native library for target '{localTarget}'. " +
                $"Ensure the Nethermind.Arbitrum.Stylus NuGet package is correctly installed " +
                $"and the native library for your platform is present. " +
                $"Expected file: runtimes/{localTarget}/native/{expectedFile}");
        }

        NativeLibrary.Free(handle);

        if (_logger.IsInfo)
            _logger.Info($"Stylus native library verified for {localTarget}");
    }

    private static string GetExpectedLibraryFileName() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "stylus.dll" :
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "libstylus.dylib" : "libstylus.so";

    private static void PopulateStylusTargetCache(IStylusTargetConfig config)
    {
        string localTarget = StylusTargets.GetLocalTargetName();
        IReadOnlyCollection<string> targets = config.GetWasmTargets();

        bool nativeSet = false;
        foreach (string target in targets)
        {
            if (target == StylusTargets.WavmTargetName)
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
