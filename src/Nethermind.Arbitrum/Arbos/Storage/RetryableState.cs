using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class RetryableState
{
    public static readonly byte[] TimeoutQueueKey = [0];
    public static readonly byte[] CalldataKey = [1];

    private readonly ArbosStorage _storage;
    private readonly ILogger _logger;
    private readonly StorageQueue _timeoutQueue;

    public RetryableState(ArbosStorage storage, ILogger logger)
    {
        _storage = storage;
        _logger = logger;

        _timeoutQueue = new StorageQueue(storage.OpenSubStorage(TimeoutQueueKey), logger);
    }

    public static void Initialize(ArbosStorage storage, ILogger logger)
    {
        logger.Info("RetryableState: Initializing...");

        var timeoutQueueStorage = storage.OpenSubStorage(TimeoutQueueKey);
        StorageQueue.Initialize(timeoutQueueStorage, logger);

        logger.Info("RetryableState (TimeoutQueue) initialized.");
    }
}

public class StorageQueue(ArbosStorage storage, ILogger logger)
{
    private const ulong NextPutOffset = 0;
    private const ulong NextGetOffset = 1;

    public static void Initialize(ArbosStorage storage, ILogger logger)
    {
        storage.SetULongByULong(NextPutOffset, 2);
        storage.SetULongByULong(NextGetOffset, 2);
    }
}
