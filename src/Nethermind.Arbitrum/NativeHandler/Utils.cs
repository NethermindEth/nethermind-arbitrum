using System.Runtime.InteropServices;
using System.Text;

namespace Nethermind.Arbitrum.NativeHandler;

public static class Utils
{
    public const string TargetWavm = "wavm";
    public const string TargetArm64 = "arm64";
    public const string TargetAmd64 = "amd64";
    public const string TargetHost = "host";
    
    public const string Arm64TargetString = "arm64-linux-unknown+neon";
    public const string Amd64TargetString = "x86_64-linux-unknown+sse4.2+lzcnt+bmi";
    
    
    public static string LocalTarget()
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
    
    public static GoSliceData CreateSlice(string s)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(s);
        IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        return new GoSliceData { ptr = ptr, len = (UIntPtr)bytes.Length };
    }

    public static GoSliceData CreateSlice(byte[] bytes)
    {
        IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        return new GoSliceData { ptr = ptr, len = (UIntPtr)bytes.Length };
    }

    public static byte[] ReadBytes(RustBytes output)
    {
        byte[] buffer = new byte[(int)output.len];
        if (buffer.Length != 0) Marshal.Copy(output.ptr, buffer, 0, buffer.Length);
        return buffer;
    }
}