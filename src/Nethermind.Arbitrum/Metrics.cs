// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.ComponentModel;
using System.Threading;
using Nethermind.Core.Attributes;
using Nethermind.Core.Metric;
using Nethermind.Core.Threading;

namespace Nethermind.Arbitrum;

public class Metrics
{
    private static bool IsBlockProcessingThread => ProcessingThread.IsBlockProcessingThread;

    [CounterMetric]
    [Description("Number of Stylus WASM calls executed.")]
    public static long StylusCalls => _mainStylusCalls + _otherStylusCalls;
    private static long _mainStylusCalls;
    private static long _otherStylusCalls;

    [CounterMetric]
    [Description("Number of transactions that executed Stylus WASM code.")]
    public static long StylusTransactions => _mainStylusTransactions + _otherStylusTransactions;
    private static long _mainStylusTransactions;
    private static long _otherStylusTransactions;

    [CounterMetric]
    [Description("Total Stylus WASM execution time in microseconds.")]
    public static long StylusExecutionMicroseconds => _mainStylusExecutionMicroseconds + _otherStylusExecutionMicroseconds;
    private static long _mainStylusExecutionMicroseconds;
    private static long _otherStylusExecutionMicroseconds;

    [ThreadStatic]
    private static bool _currentTxUsedStylus;

    /// <summary>
    /// Records a Stylus WASM execution. Called after each native call completes.
    /// </summary>
    public static void RecordStylusExecution(long executionMicroseconds)
    {
        Interlocked.Increment(ref IsBlockProcessingThread ? ref _mainStylusCalls : ref _otherStylusCalls);
        Interlocked.Add(ref IsBlockProcessingThread ? ref _mainStylusExecutionMicroseconds : ref _otherStylusExecutionMicroseconds, executionMicroseconds);

        if (_currentTxUsedStylus)
            return;

        _currentTxUsedStylus = true;
        Interlocked.Increment(ref IsBlockProcessingThread ? ref _mainStylusTransactions : ref _otherStylusTransactions);
    }

    /// <summary>
    /// Resets per-transaction tracking. Call at the start of each transaction.
    /// </summary>
    public static void ResetTransactionTracking()
    {
        _currentTxUsedStylus = false;
    }

    [CounterMetric]
    [Description("Total transactions processed")]
    public static long ArbTransactionsProcessed { get; set; }

    [CounterMetric]
    [Description("Total Arbitrum-specific transactions processed")]
    public static long ArbSpecificTransactionsProcessed { get; set; }

    [CounterMetric]
    [Description("Total Stylus contract executions")]
    public static long ArbStylusContractsExecuted { get; set; }

    [CounterMetric]
    [Description("Total Stylus API calls processed")]
    [KeyIsLabel("api")]
    public static NonBlocking.ConcurrentDictionary<int, long> ArbStylusApiCallsProcessed { get; } = new();

    [SummaryMetric(LabelNames = ["type"], ObjectiveQuantile = [0.5, 0.75, 0.9, 0.95, 0.99], ObjectiveEpsilon = [0.05, 0.05, 0.05, 0.01, 0.005])]
    [Description("Time to execute Arbitrum transactions by type.")]
    public static IMetricObserver ArbTransactionDurationMicros = NoopMetricObserver.Instance;

    [SummaryMetric(LabelNames = ["method"], ObjectiveQuantile = [0.5, 0.75, 0.9, 0.95, 0.99], ObjectiveEpsilon = [0.05, 0.05, 0.05, 0.01, 0.005])]
    [Description("Time to process Arbitrum JSON-RPC calls by method.")]
    public static IMetricObserver ArbRpcCallDurationMicros = NoopMetricObserver.Instance;

    [SummaryMetric(LabelNames = ["op"], ObjectiveQuantile = [0.5, 0.75, 0.9, 0.95, 0.99], ObjectiveEpsilon = [0.05, 0.05, 0.05, 0.01, 0.005])]
    [Description("Time to execute an operation during Arbitrum block processing.")]
    public static IMetricObserver ArbProcessingOpDurationMicros = NoopMetricObserver.Instance;
}
