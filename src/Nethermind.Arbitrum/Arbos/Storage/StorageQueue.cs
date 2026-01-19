using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class StorageQueue(ArbosStorage storage)
{
    private const ulong NextPopOffset = 1;
    private const ulong NextPushOffset = 0;
    private ArbosStorageBackedULong? _nextPop;

    private ArbosStorageBackedULong? _nextPush;

    public static void Initialize(ArbosStorage storage)
    {
        // Inits offsets to 2 to be compatible with Nitro's implementation.
        storage.Set(NextPushOffset, 2);
        storage.Set(NextPopOffset, 2);
    }

    public void ForEach(Func<ulong, ValueHash256, bool> handle)
    {
        ulong size = Size();
        ulong offset = GetNextPopOffset();

        for (ulong i = 0; i < size; i++)
        {
            ulong valueIndex = offset + i;
            ValueHash256 value = storage.Get(valueIndex);
            bool done = handle(valueIndex, value);
            if (done)
            {
                return;
            }
        }
    }

    public bool IsEmpty()
    {
        return GetNextPushOffset() == GetNextPopOffset();
    }

    public ValueHash256 Peek()
    {
        return IsEmpty() ? Hash256.Zero : storage.Get(GetNextPopOffset());
    }

    public ValueHash256 Pop()
    {
        if (IsEmpty())
            return Hash256.Zero;
        ulong currentGetOffset = GetNextPopOffset();

        ValueHash256 value = storage.Get(currentGetOffset);
        storage.Set(currentGetOffset++, Hash256.Zero);

        SetNextPopOffset(currentGetOffset);

        return value;
    }

    public void Push(ValueHash256 value)
    {
        ulong nextPushOffset = GetNextPushOffset();
        storage.Set(nextPushOffset, value);
        SetNextPushOffset(nextPushOffset + 1);
    }

    public ulong Size()
    {
        ulong nextPushOffset = GetNextPushOffset();
        ulong nextPopOffset = GetNextPopOffset();
        return nextPushOffset - nextPopOffset;
    }

    private ulong GetNextPopOffset()
    {
        _nextPop ??= new ArbosStorageBackedULong(storage, NextPopOffset);
        return _nextPop.Get();
    }

    private ulong GetNextPushOffset()
    {
        _nextPush ??= new ArbosStorageBackedULong(storage, NextPushOffset);
        return _nextPush.Get();
    }

    private void SetNextPopOffset(ulong newValue)
    {
        _nextPop ??= new ArbosStorageBackedULong(storage, NextPopOffset);
        _nextPop.Set(newValue);
    }

    private void SetNextPushOffset(ulong newValue)
    {
        _nextPush ??= new ArbosStorageBackedULong(storage, NextPushOffset);
        _nextPush.Set(newValue);
    }
}
