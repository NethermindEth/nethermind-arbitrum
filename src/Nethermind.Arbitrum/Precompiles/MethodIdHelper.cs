using System.Text;
using Nethermind.Core.Crypto;

public static class MethodIdHelper
{
    public static UInt32 GetMethodId(string methodSignature)
    {
        byte[] signatureBytes = Encoding.UTF8.GetBytes(methodSignature);

        Hash256 hash = Keccak.Compute(signatureBytes);

        return BitConverter.ToUInt32((ReadOnlySpan<byte>)hash.Bytes);
    }
}