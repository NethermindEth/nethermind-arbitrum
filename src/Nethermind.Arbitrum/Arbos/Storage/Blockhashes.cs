using Nethermind.Core.Crypto;
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

    public Hash256? GetL1BlockHash(ulong l1BlockNumber)
    {
        ulong currentL1BlockNumber = GetL1BlockNumber();

        if (l1BlockNumber >= currentL1BlockNumber || l1BlockNumber + 256 < currentL1BlockNumber)
            return null;

        ValueHash256 l1BlockHash = storage.Get(1 + l1BlockNumber % 256);

        return l1BlockHash.Bytes.SequenceEqual(default)
            ? null
            : new Hash256(l1BlockHash);
    }

    public void RecordNewL1Block(ulong blockNumber, ValueHash256 blockHash, ulong arbOsVersion)
    {
        ulong nextNumber = GetL1BlockNumber();

        if (blockNumber < nextNumber)
        {
            // we already have a stored hash for the block, so just return
            return;
        }

        if (nextNumber + 256 < blockNumber)
            nextNumber = blockNumber - 256; // no need to record hashes that we're just going to discard

        Span<byte> buffer = stackalloc byte[Keccak.Size + sizeof(ulong)];

        while (nextNumber + 1 < blockNumber)
        {
            nextNumber++;
            blockHash.Bytes.CopyTo(buffer);
            if (arbOsVersion >= ArbosVersion.Eight)
            {
                ToLittleEndianByteArray(nextNumber).CopyTo(buffer[32..]);
            }
            //burns cost
            ValueHash256 newHash = storage.ComputeKeccakHash(buffer);
            storage.Set(1 + (nextNumber % 256), newHash);
        }

        storage.Set(1 + (blockNumber % 256), blockHash);

        _l1BlockNumberStorage.Set(blockNumber + 1);
    }

    //Should move to Int64Extensions ?
    private byte[] ToLittleEndianByteArray(ulong @value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return bytes;
    }
}
