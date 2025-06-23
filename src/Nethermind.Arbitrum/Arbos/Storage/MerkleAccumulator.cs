using System.Numerics;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;

namespace Nethermind.Arbitrum.Arbos.Storage;

public record MerkleTreeNodeEvent(ulong Level, ulong NumLeaves, ValueHash256 Hash);
public record MerkleAccumulatorExportState(ulong Size, ValueHash256 Root, IReadOnlyList<ValueHash256> Partials);

public class MerkleAccumulator(ArbosStorage storage)
{
    private const int FirstPartialIndex = 2; // An offset for the first partial to be compatible with Nitro's layout.
    private readonly ArbosStorageBackedULong _sizeStorage = new(storage, 0);

    public ulong GetSize()
    {
        return _sizeStorage.Get();
    }

    public ValueHash256 CalculateRoot()
    {
        ulong size = _sizeStorage.Get();
        if (size == 0)
        {
            return default;
        }

        ValueHash256 soFar = default;
        ulong capacityInHash = 0;
        ulong capacity = 1;
        ulong partialsCount = CountPartials(size);
        ReadOnlySpan<byte> emptyBytes = stackalloc byte[32];

        for (ulong level = 0; level < partialsCount; level++)
        {
            ValueHash256 partial = GetPartial(level);
            if (partial != default)
            {
                if (soFar == default)
                {
                    soFar = partial;
                    capacityInHash = capacity;
                }
                else
                {
                    while (capacityInHash < capacity)
                    {
                        soFar = storage.CalculateHash(Bytes.Concat(soFar.Bytes, emptyBytes));
                        capacityInHash *= 2;
                    }

                    soFar = storage.CalculateHash(Bytes.Concat(partial.Bytes, soFar.Bytes));
                    capacityInHash = 2 * capacity;
                }
            }

            capacity *= 2;
        }

        return soFar;
    }

    public IReadOnlyCollection<MerkleTreeNodeEvent> Append(ValueHash256 item)
    {
        ulong size = _sizeStorage.Increment();
        ulong partialsCount = CountPartials(size - 1);
        List<MerkleTreeNodeEvent> events = new();

        ulong level = 0;
        ValueHash256 soFar = ValueKeccak.Compute(item.Bytes);
        while (true)
        {
            if (level == partialsCount) // Reached a new level
            {
                SetPartial(level, soFar);
                return events;
            }

            ValueHash256 thisLevel = GetPartial(level);
            if (thisLevel == default) // Found empty slot
            {
                SetPartial(level, soFar);
                return events;
            }

            // Combine and carry to next level
            soFar = ValueKeccak.Compute(Bytes.Concat(thisLevel.Bytes, soFar.Bytes));
            SetPartial(level, default); // Clear this level

            level += 1;
            events.Add(new MerkleTreeNodeEvent(level, size - 1, soFar));
        }
    }

    public MerkleAccumulatorExportState GetExportState()
    {
        ValueHash256 root = CalculateRoot();
        ulong size = _sizeStorage.Get();
        ulong partialsCount = CountPartials(size);
        List<ValueHash256> partials = new((int)partialsCount);

        for (ulong i = 0; i < partialsCount; i++)
        {
            partials.Add(GetPartial(i));
        }

        return new(size, root, partials);
    }

    private void SetPartial(ulong level, ValueHash256 hash)
    {
        storage.Set(level + FirstPartialIndex, hash);
    }

    private ValueHash256 GetPartial(ulong level)
    {
        return storage.Get(level + FirstPartialIndex);
    }

    private static ulong CountPartials(ulong size)
    {
        int log2Ceil = 64 - BitOperations.LeadingZeroCount(size);
        return (ulong)log2Ceil;
    }
}
