using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class RetryableState(ArbosStorage storage)
{
    public static readonly byte[] TimeoutQueueKey = [0];

    private readonly StorageQueue _timeoutQueue = new(storage.OpenSubStorage(TimeoutQueueKey));

    public StorageQueue TimeoutQueue => _timeoutQueue;

    public static void Initialize(ArbosStorage storage)
    {
        var timeoutQueueStorage = storage.OpenSubStorage(TimeoutQueueKey);
        StorageQueue.Initialize(timeoutQueueStorage);
    }
    public Retryable GetRetryable(ValueHash256 id)
    {
        return new Retryable(storage.OpenSubStorage(id.ToByteArray()), id.ToCommitment());
    }
}

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

public class Retryable(ArbosStorage storage, Hash256 id)
{
    private const ulong NumTriesOffset = 0;
    private const ulong FromOffset = 1;
    private const ulong ToOffset = 2;
    private const ulong CallValueOffset = 3;
    private const ulong BeneficiaryOffset = 4;
    private const ulong TimeoutOffset = 5;
    private const ulong TimeoutWindowsLeftOffset = 5;

    public static readonly byte[] CallDataKey = [1];

    public Hash256 Id = id;

    public ArbosStorageBackedULong NumTries { get; } = new(storage, NumTriesOffset);
    public ArbosStorageBackedAddress From { get; } = new(storage, FromOffset);
    public ArbosStorageBackedAddress? To { get; } = new(storage, ToOffset);
    public ArbosStorageBackedUInt256 CallValue { get; } = new(storage, CallValueOffset);
    public ArbosStorageBackedAddress Beneficiary { get; } = new(storage, BeneficiaryOffset);
    public ArbosStorageBackedULong Timeout { get; } = new(storage, TimeoutOffset);
    public ArbosStorageBackedULong TimeoutWindowsLeft { get; } = new(storage, TimeoutWindowsLeftOffset);

    public ArbosStorageBackedBytes CallData = new(storage.OpenSubStorage(CallDataKey));

    public void Clear()
    {
        storage.Clear(NumTriesOffset);
        storage.Clear(FromOffset);
        storage.Clear(ToOffset);
        storage.Clear(CallValueOffset);
        storage.Clear(BeneficiaryOffset);
        storage.Clear(TimeoutOffset);
        storage.Clear(TimeoutWindowsLeftOffset);
        storage.OpenSubStorage(CallDataKey).ClearBytes();
    }
}
