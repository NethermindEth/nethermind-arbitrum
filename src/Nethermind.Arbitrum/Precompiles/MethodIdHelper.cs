using Nethermind.Core.Crypto;

public static class MethodIdHelper
{
    public static UInt32 GetMethodId(string methodSignature)
    {
        Hash256 hash = Keccak.Compute(methodSignature);

        return BitConverter.ToUInt32((ReadOnlySpan<byte>)hash.Bytes);
    }
}
