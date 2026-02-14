// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.ObjectPool;
using Nethermind.Arbitrum.Metrics;
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

    // ANSI color codes
    private const string ResetColor = "\u001b[37m";
    private const string WhiteText = "\u001b[97m";
    private const string YellowText = "\u001b[93m";
    private const string OrangeText = "\u001b[38;5;208m";
    private const string RedText = "\u001b[38;5;196m";
    private const string GreenText = "\u001b[92m";
    private const string DarkGreenText = "\u001b[32m";
    private const string DarkCyanText = "\u001b[36m";
    private const string BlueText = "\u001b[94m";
    private const string MagentaText = "\u001b[95m";

    // Threshold for showing splash emoji (stylus calls > 20% of total EVM calls)
    private const double StylusSignificanceThreshold = 0.2;

    public ArbitrumProcessingStats(IStateReader stateReader, ILogManager logManager)
    {
        _executeFromThreadPool = ExecuteFromThreadPool;
        _stateReader = stateReader;
        _logger = logManager.GetClassLogger();

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
        _startSLoadOps = EvmMetrics.ThreadLocalSLoadOpcode;
        _startSStoreOps = EvmMetrics.ThreadLocalSStoreOpcode;
        _startCallOps = EvmMetrics.ThreadLocalCalls;
        _startEmptyCalls = EvmMetrics.ThreadLocalEmptyCalls;
        _startContractsAnalyzed = EvmMetrics.ThreadLocalContractsAnalysed;
        _startCachedContractsUsed = EvmMetrics.GetThreadLocalCodeDbCache();
        _startCreateOps = EvmMetrics.ThreadLocalCreates;
        _startSelfDestructOps = EvmMetrics.ThreadLocalSelfDestructs;
        _startOpCodes = EvmMetrics.ThreadLocalOpCodes;
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
        blockData.CurrentOpCodes = EvmMetrics.ThreadLocalOpCodes;
        blockData.CurrentSLoadOps = EvmMetrics.ThreadLocalSLoadOpcode;
        blockData.CurrentSStoreOps = EvmMetrics.ThreadLocalSStoreOpcode;
        blockData.CurrentCallOps = EvmMetrics.ThreadLocalCalls;
        blockData.CurrentEmptyCalls = EvmMetrics.ThreadLocalEmptyCalls;
        blockData.CurrentContractsAnalyzed = EvmMetrics.ThreadLocalContractsAnalysed;
        blockData.CurrentCachedContractsUsed = EvmMetrics.GetThreadLocalCodeDbCache();
        blockData.CurrentCreatesOps = EvmMetrics.ThreadLocalCreates;
        blockData.CurrentSelfDestructOps = EvmMetrics.ThreadLocalSelfDestructs;

        ThreadPool.UnsafeQueueUserWorkItem(_executeFromThreadPool, blockData, preferLocal: false);
    }

    private void ExecuteFromThreadPool(BlockData data)
    {
        try
        {
            lock (_reportLock)
            {
                GenerateReport(data);
            }
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
        {
            return;
        }

        // Throttle logging to once per second (or always in debug mode)
        long reportMs = Environment.TickCount64;
        if (reportMs - _lastReportMs <= 1000 && !_logger.IsDebug)
        {
            return;
        }
        _lastReportMs = reportMs;

        // Capture Stylus metrics before resetting
        long currentStylusCalls = Metrics.Metrics.StylusCalls;
        long currentStylusMicros = Metrics.Metrics.StylusExecutionMicroseconds;

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
        {
            BlockchainMetrics.MgasPerSec = mgasPerSecond;
        }

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

        string gasPrice = gasPrices is { } g
            ? $"⛽ Gas gwei: {g.Min:N3} .. {WhiteText}{System.Math.Max(g.Min, g.EstMedian):N3}{ResetColor} ({g.Ave:N3}) .. {g.Max:N3}"
            : "";

        if (chunkBlocks > 1)
        {
            _logger.Info($"Processed    {block.Number - chunkBlocks + 1,10}...{block.Number,9}   | {chunkMs,10:N1} ms  | elapsed {runMs,15:N0} ms | {gasPrice}");
        }
        else
        {
            string chunkColor = chunkMs switch
            {
                < 200 => GreenText,
                < 300 => DarkGreenText,
                < 500 => WhiteText,
                < 1000 => YellowText,
                < 2000 => OrangeText,
                _ => RedText
            };
            _logger.Info($"Processed          {block.Number,10}         | {chunkColor}{chunkMs,10:N1}{ResetColor} ms  | elapsed {runMs,15:N0} ms | {gasPrice}");
        }

        // Log block details
        string mgasPerSecondColor = (mgasPerSecond / (block.GasLimit / 1_000_000.0)) switch
        {
            > 3 => GreenText,
            > 2.5f => DarkGreenText,
            > 2 => WhiteText,
            > 1.5f => ResetColor,
            > 1 => YellowText,
            > 0.5f => OrangeText,
            _ => RedText
        };
        string sstoreColor = chunkBlocks > 1 ? "" : chunkSstore switch
        {
            > 3500 => RedText,
            > 2500 => OrangeText,
            > 2000 => YellowText,
            > 1500 => WhiteText,
            > 900 when chunkCalls > 900 => WhiteText,
            _ => ""
        };
        string callsColor = chunkBlocks > 1 ? "" : chunkCalls switch
        {
            > 3500 => RedText,
            > 2500 => OrangeText,
            > 2000 => YellowText,
            > 1500 => WhiteText,
            > 900 when chunkSstore > 900 => WhiteText,
            _ => ""
        };
        string createsColor = chunkBlocks > 1 ? "" : chunkCreates switch
        {
            > 300 => RedText,
            > 200 => OrangeText,
            > 150 => YellowText,
            > 75 => WhiteText,
            _ => ""
        };

        // Build Stylus section for Block line
        double stylusMs = stylusCallsDelta > 0 ? stylusMicrosDelta / 1000.0 : 0;
        bool isSignificant = stylusCallsDelta > 0 && (chunkCalls == 0 || stylusCallsDelta > chunkCalls * StylusSignificanceThreshold);
        string splash = isSignificant ? " 🌊" : "   "; // space+emoji OR 3 spaces for alignment
        string stylusSection = $" | {MagentaText}🦀 stylus {stylusCallsDelta,3:N0}{ResetColor} ({stylusMs,5:F1}ms){splash}";

        _logger.Info($" Block{(chunkBlocks > 1 ? $"s  x{chunkBlocks,-9:N0} " : "              ")}{(chunkBlocks == 1 ? (chunkMGas / (block.GasLimit / 16_000_000.0)) switch { > 15 => RedText, > 14 => OrangeText, > 13 => YellowText, > 10 => DarkGreenText, > 7 => GreenText, > 6 => DarkGreenText, > 5 => WhiteText, > 4 => ResetColor, > 3 => DarkCyanText, _ => BlueText } : "")} {chunkMGas,8:F2}{ResetColor} MGas    | {chunkTx,8:N0}   txs{stylusSection} | calls {callsColor}{chunkCalls,10:N0}{ResetColor} ({chunkEmptyCalls,3:N0}) | sload {chunkSload,7:N0} | sstore {sstoreColor}{chunkSstore,6:N0}{ResetColor} | create {createsColor}{chunkCreates,3:N0}{ResetColor}{(chunkSelfDestructs > 0 ? $"({-chunkSelfDestructs,3:N0})" : "")}");

        // Log throughput
        long recoveryQueue = BlockchainMetrics.RecoveryQueueSize;
        long processingQueue = BlockchainMetrics.ProcessingQueueSize;
        string blocksPerSec = $"       {bps,14:F2} Blk/s ";

        if (recoveryQueue > 0 || processingQueue > 0)
        {
            _logger.Info($" Block throughput {mgasPerSecondColor}{mgasPerSecond,11:F2}{ResetColor} MGas/s{(mgasPerSecond > 1000 ? "🔥" : "  ")}| {txps,10:N1} tps |{blocksPerSec}| recover {recoveryQueue,5:N0} | process {processingQueue,5:N0} | ops {chunkOpCodes,9:N0}");
        }
        else
        {
            _logger.Info($" Block throughput {mgasPerSecondColor}{mgasPerSecond,11:F2}{ResetColor} MGas/s{(mgasPerSecond > 1000 ? "🔥" : "  ")}| {txps,10:N1} tps |{blocksPerSec}| exec code{ResetColor} cache {cachedContractsUsed,6:N0} |{ResetColor} new {contractsAnalysed,9:N0} | ops {chunkOpCodes,9:N0}");
        }
    }

    private class BlockDataPolicy : IPooledObjectPolicy<BlockData>
    {
        public BlockData Create() => new BlockData();
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
