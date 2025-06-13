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

    public ulong GetNextGetOffset()
    {
        _nextGet ??= new ArbosStorageBackedULong(storage, NextGetOffset);
        return _nextGet.Get();
    }

    public void SetNextGetOffset(ulong newValue)
    {
        _nextGet ??= new ArbosStorageBackedULong(storage, NextGetOffset);
        _nextGet.Set(newValue);
    }

    public ulong GetNextPutOffset()
    {
        _nextPut ??= new ArbosStorageBackedULong(storage, NextPutOffset);
        return _nextPut.Get();
    }

    public bool IsEmpty()
    {
        return NextPutOffset == NextGetOffset;
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