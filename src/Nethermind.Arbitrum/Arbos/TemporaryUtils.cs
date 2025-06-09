using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Arbos;

public static class TemporaryUtils
{
    public static ValueHash256 ToHash2(this Address address)
    {
        Span<byte> addressBytes = stackalloc byte[Hash256.Size];
        address.Bytes.CopyTo(addressBytes[(Hash256.Size - Address.Size)..]);
        return new ValueHash256(addressBytes);
    }
}
