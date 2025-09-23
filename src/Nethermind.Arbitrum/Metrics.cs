// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.ComponentModel;
using Nethermind.Core.Attributes;
using Nethermind.Core.Metric;

namespace Nethermind.Arbitrum;

public static class Metrics
{
    [CounterMetric]
    [Description("Total transactions processed")]
    public static long ArbTransactionsProcessed { get; set; }

    [CounterMetric]
    [Description("Total Arbitrum-specific transactions processed")]
    public static long ArbSpecificTransactionsProcessed { get; set; }

    [CounterMetric]
    [Description("Total Stylus contract executions")]
    public static long ArbStylusContractsExecuted { get; set; }

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
