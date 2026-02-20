// SPDX-FileCopyrightText: 2026 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Consensus;
using Nethermind.Consensus.Processing;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Logging;
using Nethermind.State;
using System.Diagnostics;

namespace Nethermind.Arbitrum.Execution;

public class ArbitrumPrefetchManager : IPrefetchManager
{
    private readonly ArbitrumBlockCachePreWarmer _preWarmer;
    private readonly IPreBlockCachesWrapper? _cacheWrapper;
    private readonly ILogger _logger;
    private Task? _task;
    private CancellationTokenSource? _cancellationTokenSource;

    public ArbitrumPrefetchManager(ArbitrumBlockCachePreWarmer preWarmer, IPreBlockCachesWrapper caches, ILogManager logManager)
    {
        _preWarmer = preWarmer;
        _cacheWrapper = caches;
        _logger = logManager.GetClassLogger();
    }

    public void PrefetchBlock(Block preWarmBlock, BlockHeader parentHeader, IReleaseSpec releaseSpec)
    {
        _cancellationTokenSource = new();
        _cacheWrapper?.CreateNext();
        _task = _preWarmer.PreWarmCaches(preWarmBlock, parentHeader, releaseSpec, _cancellationTokenSource.Token);
    }

    public void CancelAndWait()
    {
        CancellationTokenExtensions.CancelDisposeAndClear(ref _cancellationTokenSource);

        bool addLog = _task is not null && (_task.Status != TaskStatus.RanToCompletion || _task.Status != TaskStatus.Canceled);
        if (addLog)
            _logger.Debug($"Cancel - awaiting prefetch task -> {_task?.Status}");

        Stopwatch stopwatch = Stopwatch.StartNew();

        _task?.GetAwaiter().GetResult();
        _task = null;

        stopwatch.Stop();
        long elapsed = stopwatch.ElapsedMicroseconds();
        Metrics.PrefetchWaitTime = elapsed;

        if (addLog)
            _logger.Debug($"Cancel - finished {elapsed} ms");
    }

    public void SwapCaches()
    {
        //_caches?.Swap();
        _logger.Debug($"Sealing and promoting cache");
        _cacheWrapper?.Promote();
    }
}
