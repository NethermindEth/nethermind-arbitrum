// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Metrics;
using Nethermind.Consensus.Processing;
using Nethermind.Logging;
using Nethermind.State;

namespace Nethermind.Arbitrum.Processing;

/// <summary>
/// Arbitrum-specific processing statistics that extends the base ProcessingStats
/// with Nethermind + Arbitrum branding and Stylus WASM execution metrics.
/// </summary>
public class ArbitrumProcessingStats : ProcessingStats
{
    private long _lastStylusCalls;
    private long _lastStylusTransactions;
    private long _lastStylusExecutionMicroseconds;

    // ANSI color codes
    private const string ResetColor = "\u001b[37m";
    private const string MagentaText = "\u001b[95m";

    public ArbitrumProcessingStats(IStateReader stateReader, ILogManager logManager)
        : base(stateReader, logManager)
    {
    }

    protected override void GenerateReport(BlockData data)
    {
        base.GenerateReport(data);
        LogStylusStats();
    }

    private void LogStylusStats()
    {
        long currentCalls = ArbitrumMetrics.ThreadLocalStylusCalls;
        long currentTxs = ArbitrumMetrics.ThreadLocalStylusTransactions;
        long currentMicros = ArbitrumMetrics.ThreadLocalStylusExecutionMicroseconds;

        long callsDelta = currentCalls - _lastStylusCalls;
        long txsDelta = currentTxs - _lastStylusTransactions;
        long microsDelta = currentMicros - _lastStylusExecutionMicroseconds;

        _lastStylusCalls = currentCalls;
        _lastStylusTransactions = currentTxs;
        _lastStylusExecutionMicroseconds = currentMicros;

        if (callsDelta > 0 && _logger.IsInfo)
        {
            double executionMs = microsDelta / 1000.0;
            _logger.Info($" {MagentaText}🦀 Stylus{ResetColor} txs: {txsDelta,5:N0} | calls: {callsDelta,6:N0} | {executionMs,8:F1} ms");
        }
    }
}
