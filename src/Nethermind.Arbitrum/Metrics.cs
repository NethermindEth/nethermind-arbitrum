// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.ComponentModel;
using Nethermind.Core.Attributes;
using Nethermind.Core.Metric;
using Nethermind.Core.Threading;

namespace Nethermind.Arbitrum;

public class Metrics
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
