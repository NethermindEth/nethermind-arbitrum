using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class RetryableState
{
    public static readonly byte[] TimeoutQueueKey = [0];
    public static readonly byte[] CalldataKey = [1];

    private readonly StorageQueue _timeoutQueue;

    public RetryableState(ArbosStorage storage)
    {
        _timeoutQueue = new StorageQueue(storage.OpenSubStorage(TimeoutQueueKey));
    }

    public static void Initialize(ArbosStorage storage)
    {
        var timeoutQueueStorage = storage.OpenSubStorage(TimeoutQueueKey);
        StorageQueue.Initialize(timeoutQueueStorage);
    }
}

public class StorageQueue(ArbosStorage storage)
{
    private const ulong NextPutOffset = 0;
    private const ulong NextGetOffset = 1;

    public static void Initialize(ArbosStorage storage)
    {
        storage.SetULongByULong(NextPutOffset, 2);
        storage.SetULongByULong(NextGetOffset, 2);
    }
}
