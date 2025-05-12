using System.Runtime.InteropServices;

namespace Nethermind.Arbitrum.NativeHandler;

public static class Utils
{
    public const string TargetWavm = "wavm";
    public const string TargetArm64 = "arm64";
    public const string TargetAmd64 = "amd64";
    public const string TargetHost = "host";
    
    
    public static string GetLocalTarget()
    {
        if (OperatingSystem.IsLinux())
            switch (RuntimeInformation.OSArchitecture)
            {
                case Architecture.X64:
                    return TargetAmd64;
                case Architecture.Arm64:
                    return TargetArm64;
            }

        return TargetHost;
    }
}