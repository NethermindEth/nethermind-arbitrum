// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace Nethermind.Arbitrum.Arbos.Stylus;

public static partial class StylusNative
{
    private const string LibraryName = "stylus";

    static StylusNative()
    {
        SetLibraryFallbackResolver();
    }

    private static void SetLibraryFallbackResolver()
    {
        Assembly assembly = typeof(StylusNative).Assembly;

        AssemblyLoadContext.GetLoadContext(assembly)!.ResolvingUnmanagedDll += (context, name) =>
        {
            if (context != assembly || !LibraryName.Equals(name, StringComparison.Ordinal))
            {
                return nint.Zero;
            }

            string platform;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                name = $"lib{name}.so";
                platform = "linux";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                name = $"lib{name}.dylib";
                platform = "osx";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                name = $"{name}.dll";
                platform = "win";
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            string arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

            return NativeLibrary.Load($"Arbos/Stylus/runtimes/{platform}-{arch}/native/{name}", context, DllImportSearchPath.AssemblyDirectory);
        };
    }
}
