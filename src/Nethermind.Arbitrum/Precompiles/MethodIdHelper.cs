using System.Buffers.Binary;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Precompiles;

public static class MethodIdHelper
{
    public static uint GetMethodId(string methodSignature)
    {
        Hash256 hash = Keccak.Compute(methodSignature);
        ReadOnlySpan<byte> hashBytes = hash.Bytes;
        return BinaryPrimitives.ReadUInt32BigEndian(hashBytes[..4]);
    }
}
