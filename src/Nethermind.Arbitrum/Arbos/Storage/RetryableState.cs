using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.Tracing.GethStyle.Custom.JavaScript;
using Nethermind.Int256;
using Nethermind.State;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class RetryableState(ArbosStorage retryables)
{
    public static readonly byte[] TimeoutQueueKey = [0];

    private readonly StorageQueue _timeoutQueue = new(retryables.OpenSubStorage(TimeoutQueueKey));

    public StorageQueue TimeoutQueue => _timeoutQueue;

    public static void Initialize(ArbosStorage storage)
    {
        var timeoutQueueStorage = storage.OpenSubStorage(TimeoutQueueKey);
        StorageQueue.Initialize(timeoutQueueStorage);
    }

    public Retryable CreateRetryable(
        ValueHash256 id, Address from, Address to, UInt256 callValue,
        Address beneficiary, ulong timeout, byte[] calldata
    )
    {
        Retryable retryable = new(retryables.OpenSubStorage(id.ToByteArray()), id.ToCommitment());

        retryable.NumTries.Set(0);
        retryable.From.Set(from);
        retryable.To!.Set(to);
        retryable.CallValue.Set(callValue);
        retryable.Beneficiary.Set(beneficiary);
        retryable.Calldata.Set(calldata);
        retryable.Timeout.Set(timeout);
        retryable.TimeoutWindowsLeft.Set(0);

        // insert the new retryable into the queue so it can be reaped later
        _timeoutQueue.Push(id);
        return retryable;
    }

    public Retryable GetRetryable(ValueHash256 id)
    {
        return new Retryable(retryables.OpenSubStorage(id.ToByteArray()), id.ToCommitment());
    }

    public Retryable? OpenRetryable(ValueHash256 id, ulong currentTimestamp)
    {
        ArbosStorage retryableStorage = retryables.OpenSubStorage(id.ToByteArray());
        ArbosStorageBackedULong timeoutStorage = new(retryableStorage, Retryable.TimeoutOffset);
        ulong timeout = timeoutStorage.Get();
        if (timeout == 0 || timeout < currentTimestamp)
        {
            // Either no retryable here (real retryable never has a zero timeout),
            // Or the timeout has expired and the retryable will soon be reaped,
            // Or the user is out of gas
            return null;
        }

        return new(retryableStorage, id.ToCommitment());
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
        ulong calldata = 32 + EvmPooledMemory.WordSize * (ulong)EvmPooledMemory.Div32Ceiling(retryable.Calldata.Size());
        return 6 * 32 + calldata;
    }

    public ulong KeepAlive(Hash256 ticketId, ulong currentTimestamp)
    {
        Retryable retryable = OpenRetryable(ticketId, currentTimestamp) ?? throw new Exception("TicketId not found");

        ulong timeout = retryable.CalculateTimeout();
        // Cannot extend life of a retryable that's already ending beyond 1 lifetime window from now
        if (timeout > currentTimestamp + Retryable.RetryableLifetimeSeconds)
        {
            throw new Exception("Timeout too far into the future");
        }

        // Add a duplicate entry to the end of the queue (only the last one deletes the retryable)
        _timeoutQueue.Push(retryable.Id);

        retryable.TimeoutWindowsLeft.Increment();

        // Pay in advance for the work needed to reap the duplicate from the timeout queue
        retryables.Burner.Burn(Retryable.RetryableReapPrice);

        return timeout + Retryable.RetryableLifetimeSeconds;
    }

    public bool DeleteRetryable(Hash256 id, IWorldState worldState)
    {
        ArbosStorage retryableStorage = retryables.OpenSubStorage(id.BytesToArray());
        ValueHash256 timeout = retryableStorage.Get(Retryable.TimeoutOffset);
        if (timeout == default)
        {
            return false;
        }

        // Move any funds in escrow to the beneficiary (should be none if the retry succeeded -- see EndTxHook)
        Address beneficiary = retryableStorage.Get(Retryable.BeneficiaryOffset).ToAddress();

        Address escrowAddress = ArbitrumTransactionProcessor.GetRetryableEscrowAddress(id);
        UInt256 escrowBalance = worldState.GetBalance(escrowAddress);

        //TODO: transfer balance here from escrow to beneficiary

        GetRetryable(id).Clear();

        return true;
    }
}

public class Retryable(ArbosStorage storage, Hash256 id)
{
    public const ulong NumTriesOffset = 0;
    public const ulong FromOffset = 1;
    public const ulong ToOffset = 2;
    public const ulong CallValueOffset = 3;
    public const ulong BeneficiaryOffset = 4;
    public const ulong TimeoutOffset = 5;
    public const ulong TimeoutWindowsLeftOffset = 6;

    public const ulong RetryableLifetimeSeconds = 7 * 24 * 60 * 60; // one week in seconds
    public const ulong RetryableReapPrice = 58000;

    public static readonly byte[] CallDataKey = [1];

    public Hash256 Id = id;

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

    public ulong CalculateTimeout()
    {
        return Timeout.Get() + TimeoutWindowsLeft.Get() * RetryableLifetimeSeconds;
    }
}
