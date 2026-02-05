// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Threading;

namespace Nethermind.Arbitrum.Metrics;

public static class ArbitrumMetrics
{
    private static long _stylusCalls;
    private static long _stylusTransactions;
    private static long _stylusExecutionMicroseconds;

    [ThreadStatic]
    private static bool _currentTxUsedStylus;

    public static long ThreadLocalStylusCalls => Interlocked.Read(ref _stylusCalls);
    public static long ThreadLocalStylusTransactions => Interlocked.Read(ref _stylusTransactions);
    public static long ThreadLocalStylusExecutionMicroseconds => Interlocked.Read(ref _stylusExecutionMicroseconds);

    public static void RecordStylusExecution(long executionMicroseconds)
    {
        Interlocked.Increment(ref _stylusCalls);
        Interlocked.Add(ref _stylusExecutionMicroseconds, executionMicroseconds);

        if (_currentTxUsedStylus)
            return;
        _currentTxUsedStylus = true;
        Interlocked.Increment(ref _stylusTransactions);
    }

    public static void ResetTransactionTracking()
    {
        _currentTxUsedStylus = false;
    }
}
