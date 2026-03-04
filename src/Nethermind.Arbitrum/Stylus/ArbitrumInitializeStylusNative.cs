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
        string rid = GetRuntimeIdentifier();
        string libraryFileName = GetExpectedLibraryFileName();

        // Try loading from assembly directory first (standard .NET resolution)
        if (NativeLibrary.TryLoad(StylusLibraryName, typeof(StylusNative).Assembly,
                DllImportSearchPath.AssemblyDirectory, out nint handle))
        {
            NativeLibrary.Free(handle);
            if (_logger.IsInfo)
                _logger.Info($"Stylus native library verified for {localTarget}");
            return;
        }

        // Try loading from runtimes/{RID}/native/ subdirectory (NuGet package layout)
        string? assemblyDir = Path.GetDirectoryName(typeof(StylusNative).Assembly.Location);
        if (assemblyDir is not null)
        {
            string runtimePath = Path.Combine(assemblyDir, "runtimes", rid, "native", libraryFileName);
            if (NativeLibrary.TryLoad(runtimePath, out handle))
            {
                NativeLibrary.Free(handle);
                if (_logger.IsInfo)
                    _logger.Info($"Stylus native library verified for {localTarget} from {runtimePath}");
                return;
            }
        }

        throw new InvalidOperationException(
            $"Failed to load Stylus native library for target '{localTarget}'. " +
            $"Ensure the Nethermind.Arbitrum.Stylus NuGet package is correctly installed " +
            $"and the native library for your platform is present. " +
            $"Expected file: runtimes/{rid}/native/{libraryFileName}");
    }

    private static string GetRuntimeIdentifier()
    {
        string arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException($"Unsupported architecture: {RuntimeInformation.OSArchitecture}")
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return $"win-{arch}";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return $"osx-{arch}";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return $"linux-{arch}";

        throw new PlatformNotSupportedException($"Unsupported operating system");
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
