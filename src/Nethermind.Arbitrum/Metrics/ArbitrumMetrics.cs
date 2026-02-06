// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.ComponentModel;
using Nethermind.Core.Attributes;
using Nethermind.Core.Threading;

namespace Nethermind.Arbitrum.Metrics;

public static class ArbitrumMetrics
{
    [CounterMetric]
    [Description("Number of Stylus WASM calls executed.")]
    public static long StylusCalls => _stylusCalls.GetTotalValue();
    private static readonly ZeroContentionCounter _stylusCalls = new();

    [CounterMetric]
    [Description("Number of transactions that executed Stylus WASM code.")]
    public static long StylusTransactions => _stylusTransactions.GetTotalValue();
    private static readonly ZeroContentionCounter _stylusTransactions = new();

    [CounterMetric]
    [Description("Total Stylus WASM execution time in microseconds.")]
    public static long StylusExecutionMicroseconds => _stylusExecutionMicroseconds.GetTotalValue();
    private static readonly ZeroContentionCounter _stylusExecutionMicroseconds = new();

    [ThreadStatic]
    private static bool _currentTxUsedStylus;

    /// <summary>
    /// Records a Stylus WASM execution. Called after each native call completes.
    /// </summary>
    public static void RecordStylusExecution(long executionMicroseconds)
    {
        _stylusCalls.Increment();
        _stylusExecutionMicroseconds.Increment((int)executionMicroseconds);

        if (_currentTxUsedStylus)
            return;

        _currentTxUsedStylus = true;
        _stylusTransactions.Increment();
    }

    /// <summary>
    /// Resets per-transaction tracking. Call at the start of each transaction.
    /// </summary>
    public static void ResetTransactionTracking()
    {
        _currentTxUsedStylus = false;
    }
}
