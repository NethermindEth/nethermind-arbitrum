// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.ComponentModel;
using Nethermind.Core.Attributes;

namespace Nethermind.Arbitrum.Sequencer;

/// <summary>
/// Metrics for the Arbitrum sequencer transaction queue
/// </summary>
public static class Metrics
{
    [GaugeMetric]
    [Description("Number of transactions currently in the sequencer queue.")]
    public static long TransactionQueueLength { get; set; }

    [GaugeMetric]
    [Description("Number of transactions currently in the sequencer retry queue.")]
    public static long RetryQueueLength { get; set; }

    [CounterMetric]
    [Description("Total number of transactions enqueued by the sequencer.")]
    public static long TransactionsEnqueued { get; set; }

    [CounterMetric]
    [Description("Total number of transactions dequeued by the sequencer.")]
    public static long TransactionsDequeued { get; set; }

    [CounterMetric]
    [Description("Total number of transactions that timed out in the queue.")]
    public static long TransactionsTimedOut { get; set; }

    [CounterMetric]
    [Description("Total number of transactions rejected due to full queue.")]
    public static long TransactionsRejectedQueueFull { get; set; }

    [CounterMetric]
    [Description("Total number of transactions moved to retry queue.")]
    public static long TransactionsMovedToRetry { get; set; }

    [CounterMetric]
    [Description("Total number of transactions retried from retry queue.")]
    public static long TransactionsRetried { get; set; }

    [CounterMetric]
    [Description("Total number of Timeboost transactions processed.")]
    public static long TimeboostTransactions { get; set; }

    [GaugeMetric]
    [Description("Average time (ms) transactions spend in the queue before processing.")]
    public static double AverageQueueTimeMs { get; set; }
}
