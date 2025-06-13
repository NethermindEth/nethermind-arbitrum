using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class RetryableState(ArbosStorage storage)
{
    public static readonly byte[] TimeoutQueueKey = [0];

    private readonly ArbosStorage _retryables = storage;
    private readonly StorageQueue _timeoutQueue = new(storage.OpenSubStorage(TimeoutQueueKey));

    public StorageQueue TimeoutQueue => _timeoutQueue;

    public static void Initialize(ArbosStorage storage)
    {
        var timeoutQueueStorage = storage.OpenSubStorage(TimeoutQueueKey);
        StorageQueue.Initialize(timeoutQueueStorage);
    }

    public Retryable GetRetryable(ValueHash256 id)
    {
        return new Retryable(id, storage.OpenSubStorage(id.ToByteArray()));
    }

    public Retryable? OpenRetryable(ValueHash256 id, ulong currentTimestamp)
    {
        ArbosStorage retryableStorage = _retryables.OpenSubStorage(id.ToByteArray());
        ArbosStorageBackedULong timeoutStorage = new(retryableStorage, Retryable.TimeoutOffset);
        ulong timeout = timeoutStorage.Get();
        if (timeout == 0 || timeout < currentTimestamp)
        {
            // Either no retryable here (real retryable never has a zero timeout),
            // Or the timeout has expired and the retryable will soon be reaped,
            // Or the user is out of gas
            return null;
        }

        return new(id, retryableStorage);
    }

    public ulong RetryableSizeBytes(ValueHash256 id, ulong currentTimestamp)
    {
        Retryable? retryable = OpenRetryable(id, currentTimestamp);
        if (retryable is null)
        {
            return 0;
        }

        //TODO: understand magic numbers
        // length + contents
        ulong calldata = 32 + 32 * (ulong)EvmPooledMemory.Div32Ceiling(retryable.Calldata.Size());
        return 6 * 32 + calldata;
    }

    public ulong KeepAlive(Hash256 ticketId, ulong currentTimestamp, ulong limitBeforeAdd)
    {
        Retryable retryable = OpenRetryable(ticketId, currentTimestamp) ?? throw new Exception("No retryable with ID " + ticketId);

        ulong timeout = retryable.CalculateTimeout();
        if (timeout > limitBeforeAdd)
        {
            throw new Exception("Timeout too far into the future");
        }

    	// Add a duplicate entry to the end of the queue (only the last one deletes the retryable)
        _timeoutQueue.Put(retryable.Id);

        retryable.TimeoutWindowsLeft.Increment();

	    // Pay in advance for the work needed to reap the duplicate from the timeout queue
        _retryables.Burner.Burn(Retryable.RetryableReapPrice);

        return timeout + Retryable.RetryableLifetimeSeconds;
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

    public void Put(ValueHash256 value)
    {
        ulong nextPutOffset = GetNextPutOffset();
        storage.Set(nextPutOffset - 1, value);
    }
}

public class Retryable (ValueHash256 id, ArbosStorage storage)
{
    public const ulong NumTriesOffset = 0;
    public const ulong FromOffset = 1;
    public const ulong ToOffset = 2;
    public const ulong CallValueOffset = 3;
    public const ulong BeneficiaryOffset = 4;
    public const ulong TimeoutOffset = 5;
    public const ulong TimeoutWindowsLeftOffset = 5;

    public const ulong RetryableLifetimeSeconds = 7 * 24 * 60 * 60; // one week in seconds
    public const ulong RetryableReapPrice = 58000;

    public static readonly byte[] CallDataKey = [1];

    public ValueHash256 Id { get; set; } = id;

    public ArbosStorageBackedULong NumTries { get; } = new(storage, NumTriesOffset);
    public ArbosStorageBackedAddress From { get; } = new(storage, FromOffset);
    public ArbosStorageBackedAddress? To { get; } = new(storage, ToOffset);
    public ArbosStorageBackedUInt256 CallValue { get; } = new(storage, CallValueOffset);
    public ArbosStorageBackedAddress Beneficiary { get; } = new(storage, BeneficiaryOffset);
    public ArbosStorageBackedBytes Calldata { get; } = new(storage.OpenSubStorage(CallDataKey));
    public ArbosStorageBackedULong Timeout { get; } = new(storage, TimeoutOffset);
    public ArbosStorageBackedULong TimeoutWindowsLeft { get; } = new(storage, TimeoutWindowsLeftOffset);

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

    public ulong IncrementNumTries()
    {
        return NumTries.Increment();
    }

    public ArbitrumRetryTx MakeTx(
        ulong chainId, ulong nonce, UInt256 gasFeeCap, ulong gas,
        Hash256 ticketId, Address refundTo, UInt256 maxRefund, UInt256 submissionFeeRefund
    )
    {
        return new ArbitrumRetryTx(
            chainId,
            nonce,
            From.Get(),
            gasFeeCap,
            gas,
            To?.Get(),
            CallValue.Get(),
            Calldata.Get(),
            ticketId,
            refundTo,
            maxRefund,
            submissionFeeRefund
        );
    }

    public ulong CalculateTimeout()
    {
        return Timeout.Get() + TimeoutWindowsLeft.Get() * RetryableLifetimeSeconds;
    }
}