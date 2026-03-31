// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.ComponentModel;
using System.Threading;
using Nethermind.Core.Attributes;
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
}
