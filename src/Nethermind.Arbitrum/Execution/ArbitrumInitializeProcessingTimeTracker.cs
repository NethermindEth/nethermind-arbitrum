// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Api.Steps;
using Nethermind.Consensus.Processing;

namespace Nethermind.Arbitrum.Execution;

/// <summary>
/// Subscribes to BlockchainProcessor.NewProcessingStatistics event to track accumulated
/// block processing time for maintenance scheduling.
/// </summary>
public class ArbitrumInitializeProcessingTimeTracker(
    IMainProcessingContext mainProcessingContext,
    IProcessingTimeTracker processingTimeTracker) : IStep
{
    public Task Execute(CancellationToken cancellationToken)
    {
        mainProcessingContext.BlockchainProcessor.NewProcessingStatistics += OnNewProcessingStatistics;
        return Task.CompletedTask;
    }

    private void OnNewProcessingStatistics(object? sender, BlockStatistics stats) =>
        processingTimeTracker.AddProcessingTime(TimeSpan.FromMilliseconds(stats.ProcessingMs));
}
