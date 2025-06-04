using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class Blockhashes(ArbosStorage storage)
{
    private readonly ArbosStorageBackedULong _l1BlockNumberStorage = new(storage, 0);

    public static void Initialize(ArbosStorage storage, ILogger logger)
    {
    }

    public ulong GetL1BlockNumber()
    {
        return _l1BlockNumberStorage.Get();
    }

    public void RecordNewL1Block(ulong blockNumber, ValueHash256 blockHash, ulong arbOsVersion)
    {
        var nextNumber = GetL1BlockNumber();

        if (blockNumber < nextNumber)
        {
            // we already have a stored hash for the block, so just return
            return;
        }

        if (nextNumber + 256 < nextNumber)
        {
            nextNumber = blockNumber - 256; // no need to record hashes that we're just going to discard
        }

        Span<byte> buffer = stackalloc byte[Keccak.Size + sizeof(ulong)];

        while (nextNumber + 1 < blockNumber)
        {
            nextNumber++;
            blockHash.Bytes.CopyTo(buffer);
            if (arbOsVersion >= 8)
            {
                nextNumber.ToBigEndianByteArray().CopyTo(buffer[32..]);
            }

            //TODO calc and burn cost
            var newHash = Keccak.Compute(buffer);

            storage.Set(1 + (nextNumber % 256), newHash);
        }

        storage.Set(1 + (nextNumber % 256), blockHash);

        _l1BlockNumberStorage.Set(blockNumber + 1);
    }
}
