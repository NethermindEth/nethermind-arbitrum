// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Runtime.InteropServices;
using Nethermind.Api.Steps;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Stylus;

public class ArbitrumInitializeStylusNative(IStylusTargetConfig api, ILogManager logManager) : IStep
{
    private const string StylusLibraryName = "stylus";
    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumInitializeStylusNative>();

    public Task Execute(CancellationToken cancellationToken)
    {
        // Verify native library is available for current architecture before any P/Invoke calls
        VerifyStylusNativeLibrary();

        IStylusTargetConfig config = api;

        StylusNative.SetWasmLruCacheCapacity(Math.Utils.SaturateMul(config.NativeLruCacheCapacityMb, 1024 * 1024ul));
        PopulateStylusTargetCache(config);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Stylus native library can be loaded for the current platform.
    /// Provides a clear, actionable error message if the library is missing, rather than
    /// failing deep in P/Invoke machinery with a generic DllNotFoundException.
    /// </summary>
    private void VerifyStylusNativeLibrary()
    {
        string rid = GetRuntimeIdentifier();

        if (!NativeLibrary.TryLoad(StylusLibraryName, typeof(StylusNative).Assembly,
                DllImportSearchPath.AssemblyDirectory, out nint handle))
        {
            string expectedFile = GetExpectedLibraryFileName();
            throw new InvalidOperationException(
                $"Failed to load Stylus native library for {rid}. " +
                $"Ensure the Nethermind.Arbitrum.Stylus NuGet package is correctly installed " +
                $"and the native library for your platform is present. " +
                $"Expected file: runtimes/{rid}/native/{expectedFile}");
        }

        // Free the handle - we only needed to verify the library loads successfully
        NativeLibrary.Free(handle);

        if (_logger.IsInfo)
            _logger.Info($"Stylus native library verified for {rid}");
    }

    private static string GetRuntimeIdentifier()
    {
        string arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()
        };

        string os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
                    RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" : "linux";

        return $"{os}-{arch}";
    }

    private static string GetExpectedLibraryFileName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "stylus.dll";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "libstylus.dylib";
        return "libstylus.so";
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
