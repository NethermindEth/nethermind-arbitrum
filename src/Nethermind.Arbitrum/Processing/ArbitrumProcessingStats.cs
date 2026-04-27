// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Diagnostics;
using Microsoft.Extensions.ObjectPool;
using Nethermind.Consensus.Processing;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.State;
using BlockchainMetrics = Nethermind.Blockchain.Metrics;
using EvmMetrics = Nethermind.Evm.Metrics;

namespace Nethermind.Arbitrum.Processing;

/// <summary>
/// Arbitrum-specific processing statistics and Stylus WASM execution metrics.
/// </summary>
public class ArbitrumProcessingStats : IProcessingStats
{
    private static readonly DefaultObjectPool<BlockData> _dataPool = new(new BlockDataPolicy(), 16);
    private readonly Action<BlockData> _executeFromThreadPool;

    public event EventHandler<BlockStatistics>? NewProcessingStatistics;

    private readonly IStateReader _stateReader;
    private readonly ILogger _logger;
    private readonly Stopwatch _runStopwatch = new();
    private readonly Lock _reportLock = new();

    // Start values captured before processing
    private long _startOpCodes;
    private long _startSLoadOps;
    private long _startSStoreOps;
    private long _startCallOps;
    private long _startEmptyCalls;
    private long _startCachedContractsUsed;
    private long _startContractsAnalyzed;
    private long _startCreateOps;
    private long _startSelfDestructOps;

    // Chunk accumulators (reset after each report)
    private double _chunkMGas;
    private long _chunkProcessingMicroseconds;
    private long _chunkTx;
    private long _chunkBlocks;
    private long _opCodes;
    private long _callOps;
    private long _emptyCalls;
    private long _sLoadOps;
    private long _sStoreOps;
    private long _selfDestructOps;
    private long _createOps;
    private long _contractsAnalyzed;
    private long _cachedContractsUsed;

    // Timing
    private long _lastElapsedRunningMicroseconds;
    private long _lastReportMs;

    // Stylus tracking
    private long _lastStylusCalls;
    private long _lastStylusExecutionMicroseconds;

    // Reused buffer for formatter output to avoid per-report allocations.
    private readonly List<string> _formatterLines = new(3);

    public ArbitrumProcessingStats(IStateReader stateReader, ILogManager logManager)
    {
        _executeFromThreadPool = ExecuteFromThreadPool;
        _stateReader = stateReader;
        _logger = logManager.GetClassLogger<ArbitrumProcessingStats>();

#if DEBUG
        _logger.SetDebugMode();
#endif
    }

    public void Start()
    {
        if (!_runStopwatch.IsRunning)
        {
            _lastReportMs = Environment.TickCount64;
            _runStopwatch.Start();
        }
    }

    public void CaptureStartStats()
    {
        _startSLoadOps = EvmMetrics.MainThreadSLoadOpcode;
        _startSStoreOps = EvmMetrics.MainThreadSStoreOpcode;
        _startCallOps = EvmMetrics.MainThreadCalls;
        _startEmptyCalls = EvmMetrics.MainThreadEmptyCalls;
        _startContractsAnalyzed = EvmMetrics.MainThreadContractsAnalysed;
        _startCachedContractsUsed = EvmMetrics.MainThreadCodeDbCache;
        _startCreateOps = EvmMetrics.MainThreadCreates;
        _startSelfDestructOps = EvmMetrics.MainThreadSelfDestructs;
        _startOpCodes = EvmMetrics.MainThreadOpCodes;
    }

    public void UpdateStats(Block? block, BlockHeader? baseBlock, long blockProcessingTimeInMicros)
    {
        if (block is null)
            return;

        BlockData blockData = _dataPool.Get();
        blockData.Block = block;
        blockData.BaseBlock = baseBlock;
        blockData.RunningMicroseconds = _runStopwatch.ElapsedMicroseconds();
        blockData.RunMicroseconds = _runStopwatch.ElapsedMicroseconds() - _lastElapsedRunningMicroseconds;
        blockData.StartOpCodes = _startOpCodes;
        blockData.StartSLoadOps = _startSLoadOps;
        blockData.StartSStoreOps = _startSStoreOps;
        blockData.StartCallOps = _startCallOps;
        blockData.StartEmptyCalls = _startEmptyCalls;
        blockData.StartContractsAnalyzed = _startContractsAnalyzed;
        blockData.StartCachedContractsUsed = _startCachedContractsUsed;
        blockData.StartCreateOps = _startCreateOps;
        blockData.StartSelfDestructOps = _startSelfDestructOps;
        blockData.ProcessingMicroseconds = blockProcessingTimeInMicros;
        blockData.CurrentOpCodes = EvmMetrics.MainThreadOpCodes;
        blockData.CurrentSLoadOps = EvmMetrics.MainThreadSLoadOpcode;
        blockData.CurrentSStoreOps = EvmMetrics.MainThreadSStoreOpcode;
        blockData.CurrentCallOps = EvmMetrics.MainThreadCalls;
        blockData.CurrentEmptyCalls = EvmMetrics.MainThreadEmptyCalls;
        blockData.CurrentContractsAnalyzed = EvmMetrics.MainThreadContractsAnalysed;
        blockData.CurrentCachedContractsUsed = EvmMetrics.MainThreadCodeDbCache;
        blockData.CurrentCreatesOps = EvmMetrics.MainThreadCreates;
        blockData.CurrentSelfDestructOps = EvmMetrics.MainThreadSelfDestructs;

        ThreadPool.UnsafeQueueUserWorkItem(_executeFromThreadPool, blockData, preferLocal: false);
    }

    private void ExecuteFromThreadPool(BlockData data)
    {
        try
        {
            lock (_reportLock)
                GenerateReport(data);
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error("Error when generating processing statistics", ex);
        }
        finally
        {
            _dataPool.Return(data);
        }
    }

    private void GenerateReport(BlockData data)
    {
        Block? block = data.Block;
        if (block is null)
            return;

        long blockNumber = block.Number;
        double chunkMGas = (_chunkMGas += block.GasUsed / 1_000_000.0);

        // Update Prometheus metrics
        double mgas = block.GasUsed / 1_000_000.0;
        double timeSec = data.ProcessingMicroseconds / 1_000_000.0;
        BlockchainMetrics.BlockMGasPerSec.Observe(mgas / timeSec);
        BlockchainMetrics.BlockProcessingTimeMicros.Observe(data.ProcessingMicroseconds);

        BlockchainMetrics.Mgas += block.GasUsed / 1_000_000.0;
        Transaction[] txs = block.Transactions;
        double chunkMicroseconds = (_chunkProcessingMicroseconds += data.ProcessingMicroseconds);
        double chunkTx = (_chunkTx += txs.Length);

        long chunkBlocks = ++_chunkBlocks;

        BlockchainMetrics.Blocks = blockNumber;
        BlockchainMetrics.BlockchainHeight = blockNumber;
        BlockchainMetrics.Transactions += txs.Length;
        BlockchainMetrics.TotalDifficulty = block.TotalDifficulty ?? UInt256.Zero;
        BlockchainMetrics.LastDifficulty = block.Difficulty;
        BlockchainMetrics.GasUsed = block.GasUsed;
        BlockchainMetrics.GasLimit = block.GasLimit;

        // Accumulate EVM operation counts
        long chunkOpCodes = (_opCodes += data.CurrentOpCodes - data.StartOpCodes);
        long chunkCalls = (_callOps += data.CurrentCallOps - data.StartCallOps);
        long chunkEmptyCalls = (_emptyCalls += data.CurrentEmptyCalls - data.StartEmptyCalls);
        long chunkSload = (_sLoadOps += data.CurrentSLoadOps - data.StartSLoadOps);
        long chunkSstore = (_sStoreOps += data.CurrentSStoreOps - data.StartSStoreOps);
        long chunkSelfDestructs = (_selfDestructOps += data.CurrentSelfDestructOps - data.StartSelfDestructOps);
        long chunkCreates = (_createOps += data.CurrentCreatesOps - data.StartCreateOps);
        long contractsAnalysed = (_contractsAnalyzed += data.CurrentContractsAnalyzed - data.StartContractsAnalyzed);
        long cachedContractsUsed = (_cachedContractsUsed += data.CurrentCachedContractsUsed - data.StartCachedContractsUsed);

        // Skip logging during init/genesis when state isn't fully available
        if (data.BaseBlock is null || !_stateReader.HasStateForBlock(data.BaseBlock) ||
            block.StateRoot is null || !_stateReader.HasStateForBlock(block.Header))
            return;

        // Throttle logging to once per second (or always in debug mode)
        long reportMs = Environment.TickCount64;
        if (reportMs - _lastReportMs <= 1000 && !_logger.IsDebug)
            return;
        _lastReportMs = reportMs;

        // Capture Stylus metrics before resetting
        long currentStylusCalls = Metrics.StylusCalls;
        long currentStylusMicros = Metrics.StylusExecutionMicroseconds;

        long stylusCallsDelta = currentStylusCalls - _lastStylusCalls;
        long stylusMicrosDelta = currentStylusMicros - _lastStylusExecutionMicroseconds;

        _lastStylusCalls = currentStylusCalls;
        _lastStylusExecutionMicroseconds = currentStylusMicros;

        // Reset chunk accumulators
        _chunkBlocks = 0;
        _chunkMGas = 0;
        _chunkTx = 0;
        _chunkProcessingMicroseconds = 0;
        _opCodes = 0;
        _callOps = 0;
        _emptyCalls = 0;
        _sLoadOps = 0;
        _sStoreOps = 0;
        _selfDestructOps = 0;
        _createOps = 0;
        _contractsAnalyzed = 0;
        _cachedContractsUsed = 0;

        // Calculate throughput metrics
        double mgasPerSecond = chunkMicroseconds == 0 ? -1 : chunkMGas / chunkMicroseconds * 1_000_000.0;
        if (chunkMicroseconds != 0 && chunkMGas != 0)
            BlockchainMetrics.MgasPerSec = mgasPerSecond;

        double txps = chunkMicroseconds == 0 ? -1 : chunkTx / chunkMicroseconds * 1_000_000.0;
        double bps = chunkMicroseconds == 0 ? -1 : chunkBlocks / chunkMicroseconds * 1_000_000.0;
        double chunkMs = chunkMicroseconds == 0 ? -1 : chunkMicroseconds / 1000.0;
        double runMs = data.RunMicroseconds == 0 ? -1 : data.RunMicroseconds / 1000.0;

        // Get gas prices via public method
        var gasPrices = EvmMetrics.GetBlockGasPrices();

        // Fire statistics event for monitoring consumers
        NewProcessingStatistics?.Invoke(this, new BlockStatistics
        {
            BlockCount = chunkBlocks,
            BlockFrom = block.Number - chunkBlocks + 1,
            BlockTo = block.Number,
            ProcessingMs = chunkMs,
            SlotMs = runMs,
            MGasPerSecond = mgasPerSecond,
            MinGas = gasPrices?.Min ?? 0,
            MedianGas = gasPrices?.EstMedian ?? 0,
            AveGas = gasPrices?.Ave ?? 0,
            MaxGas = gasPrices?.Max ?? 0,
            GasLimit = block.GasLimit
        });

        _lastElapsedRunningMicroseconds = data.RunningMicroseconds;

        if (!_logger.IsInfo)
            return;

        ProcessingStatsReport report = new(
            BlockNumber: blockNumber,
            ChunkBlocks: chunkBlocks,
            ChunkMGas: chunkMGas,
            ChunkTx: (long)chunkTx,
            ChunkMs: chunkMs,
            RunMs: runMs,
            GasPrices: gasPrices,
            ChunkCalls: chunkCalls,
            ChunkEmptyCalls: chunkEmptyCalls,
            ChunkSload: chunkSload,
            ChunkSstore: chunkSstore,
            ChunkCreates: chunkCreates,
            ChunkSelfDestructs: chunkSelfDestructs,
            ChunkOpCodes: chunkOpCodes,
            MGasPerSecond: mgasPerSecond,
            Txps: txps,
            Bps: bps,
            GasLimit: block.GasLimit,
            StylusCallsDelta: stylusCallsDelta,
            StylusMs: stylusCallsDelta > 0 ? stylusMicrosDelta / 1000.0 : 0,
            CachedContractsUsed: cachedContractsUsed,
            ContractsAnalysed: contractsAnalysed,
            RecoveryQueueSize: BlockchainMetrics.RecoveryQueueSize,
            ProcessingQueueSize: BlockchainMetrics.ProcessingQueueSize);

        _formatterLines.Clear();

        ProcessingStatsFormatter.Format(in report, _formatterLines);

        foreach (string line in _formatterLines)
            _logger.Info(line);
    }

    private class BlockDataPolicy : IPooledObjectPolicy<BlockData>
    {
        public BlockData Create() => new();
        public bool Return(BlockData data)
        {
            data.Block = null;
            data.BaseBlock = null;
            return true;
        }
    }

    private class BlockData
    {
        public Block? Block;
        public BlockHeader? BaseBlock;
        public long CurrentOpCodes;
        public long CurrentSLoadOps;
        public long CurrentSStoreOps;
        public long CurrentCallOps;
        public long CurrentEmptyCalls;
        public long CurrentContractsAnalyzed;
        public long CurrentCachedContractsUsed;
        public long CurrentCreatesOps;
        public long CurrentSelfDestructOps;
        public long ProcessingMicroseconds;
        public long RunningMicroseconds;
        public long RunMicroseconds;
        public long StartOpCodes;
        public long StartSelfDestructOps;
        public long StartCreateOps;
        public long StartContractsAnalyzed;
        public long StartCachedContractsUsed;
        public long StartEmptyCalls;
        public long StartCallOps;
        public long StartSStoreOps;
        public long StartSLoadOps;
    }
}
