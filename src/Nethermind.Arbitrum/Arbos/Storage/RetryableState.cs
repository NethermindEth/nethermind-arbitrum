using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos;

public class RetryableState(ArbosStorage storage, ILogger logger)
{
    public static readonly byte[] TimeoutQueueKey = [0];
    public static readonly byte[] CalldataKey = [1];

    private readonly ArbosStorage _storage = storage;
    private readonly ILogger _logger = logger;
    private readonly StorageQueue _timeoutQueue = new(storage.OpenSubStorage(TimeoutQueueKey), logger);

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
        storage.SetUint64ByUint64(NextPutOffset, 2);
        storage.SetUint64ByUint64(NextGetOffset, 2);
    }
}
