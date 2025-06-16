using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class StorageQueue(ArbosStorage storage)
{
    private const ulong NextPutOffset = 0;
    private const ulong NextGetOffset = 1;

    private ArbosStorageBackedULong? _nextPut;
    private ArbosStorageBackedULong? _nextGet;

    public static void Initialize(ArbosStorage storage)
    {
        storage.Set(NextPutOffset, 2);
        storage.Set(NextGetOffset, 2);
    }

    public bool IsEmpty()
    {
        return GetNextPutOffset() == GetNextGetOffset();
    }

    public ValueHash256 Peek()
    {
        return IsEmpty() ? Hash256.Zero : storage.Get(GetNextGetOffset());
    }

    public ValueHash256 Get()
    {
        if (IsEmpty())
            return Hash256.Zero;
        var currentGetOffset = GetNextGetOffset();

        var value = storage.Get(currentGetOffset);
        storage.Set(currentGetOffset++, Hash256.Zero);

        SetNextGetOffset(currentGetOffset);

        return value;
    }

    public void Put(ValueHash256 value)
    {
        ulong nextPutOffset = GetNextPutOffset();
        storage.Set(nextPutOffset, value);
        SetNextPutOffset(nextPutOffset + 1);
    }

    public ulong Size()
    {
        ulong nextPutOffset = GetNextPutOffset();
        ulong nextGetOffset = GetNextGetOffset();
        return nextPutOffset - nextGetOffset;
    }

    public void ForEach(Func<ulong, ValueHash256, bool> handle)
    {
        ulong size = Size();
        ulong offset = GetNextGetOffset();

        for (ulong i = 0; i < size; i++)
        {
            ulong valueIndex = offset + i;
            ValueHash256 value = storage.Get(valueIndex);
            bool done = handle(valueIndex, value);
            if (done)
            {
                break;
            }
        }
    }

    private ulong GetNextGetOffset()
    {
        _nextGet ??= new ArbosStorageBackedULong(storage, NextGetOffset);
        return _nextGet.Get();
    }

    private void SetNextGetOffset(ulong newValue)
    {
        _nextGet ??= new ArbosStorageBackedULong(storage, NextGetOffset);
        _nextGet.Set(newValue);
    }

    private ulong GetNextPutOffset()
    {
        _nextPut ??= new ArbosStorageBackedULong(storage, NextPutOffset);
        return _nextPut.Get();
    }

    private void SetNextPutOffset(ulong newValue)
    {
        _nextPut ??= new ArbosStorageBackedULong(storage, NextPutOffset);
        _nextPut.Set(newValue);
    }
}
