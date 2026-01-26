// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Runtime.CompilerServices;
using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Execution;
using Nethermind.Blockchain.Tracing;
using Nethermind.Consensus.Processing;
using Nethermind.Core;
using Nethermind.Evm.State;
using Nethermind.Evm.Tracing;
using Nethermind.Evm.TransactionProcessing;

namespace Nethermind.Arbitrum.Test.Execution;

[TestFixture]
public sealed class ArbitrumInitializeProcessingTimeTrackerTests
{
    private static ProcessingTimeTracker CreateTracker() => new(new ArbitrumConfig());

    [Test]
    public async Task Execute_WhenCalled_SubscribesToNewProcessingStatistics()
    {
        StubBlockchainProcessor stubProcessor = new();
        StubMainProcessingContext stubContext = new(stubProcessor);
        ProcessingTimeTracker tracker = CreateTracker();

        ArbitrumInitializeProcessingTimeTracker step = new(stubContext, tracker);
        await step.Execute(CancellationToken.None);

        stubProcessor.HasSubscribers.Should().BeTrue();
    }

    [Test]
    public async Task OnNewProcessingStatistics_ValidStats_AddsProcessingTime()
    {
        StubBlockchainProcessor stubProcessor = new();
        StubMainProcessingContext stubContext = new(stubProcessor);
        ProcessingTimeTracker tracker = CreateTracker();
        TimeSpan initialTime = tracker.TimeBeforeFlush;

        ArbitrumInitializeProcessingTimeTracker step = new(stubContext, tracker);
        await step.Execute(CancellationToken.None);

        stubProcessor.RaiseNewProcessingStatistics(processingMs: 100.0);

        tracker.TimeBeforeFlush.Should().Be(initialTime - TimeSpan.FromMilliseconds(100));
    }

    [Test]
    public async Task OnNewProcessingStatistics_ZeroProcessingMs_AddsZeroTime()
    {
        StubBlockchainProcessor stubProcessor = new();
        StubMainProcessingContext stubContext = new(stubProcessor);
        ProcessingTimeTracker tracker = CreateTracker();
        TimeSpan initialTime = tracker.TimeBeforeFlush;

        ArbitrumInitializeProcessingTimeTracker step = new(stubContext, tracker);
        await step.Execute(CancellationToken.None);

        stubProcessor.RaiseNewProcessingStatistics(processingMs: 0.0);

        tracker.TimeBeforeFlush.Should().Be(initialTime);
    }

    [Test]
    public async Task OnNewProcessingStatistics_MultipleEvents_AccumulatesTime()
    {
        StubBlockchainProcessor stubProcessor = new();
        StubMainProcessingContext stubContext = new(stubProcessor);
        ProcessingTimeTracker tracker = CreateTracker();
        TimeSpan initialTime = tracker.TimeBeforeFlush;

        ArbitrumInitializeProcessingTimeTracker step = new(stubContext, tracker);
        await step.Execute(CancellationToken.None);

        stubProcessor.RaiseNewProcessingStatistics(processingMs: 50.0);
        stubProcessor.RaiseNewProcessingStatistics(processingMs: 30.0);
        stubProcessor.RaiseNewProcessingStatistics(processingMs: 20.0);

        tracker.TimeBeforeFlush.Should().Be(initialTime - TimeSpan.FromMilliseconds(100));
    }

    private sealed class StubBlockchainProcessor : IBlockchainProcessor
    {
        private EventHandler<BlockStatistics>? _newProcessingStatistics;

        public event EventHandler<IBlockchainProcessor.InvalidBlockEventArgs> InvalidBlock
        {
            add { }
            remove { }
        }

        public event EventHandler<BlockStatistics> NewProcessingStatistics
        {
            add => _newProcessingStatistics += value;
            remove => _newProcessingStatistics -= value;
        }

        public bool HasSubscribers => _newProcessingStatistics is not null;

        public void RaiseNewProcessingStatistics(double processingMs)
        {
            StubBlockStatistics stats = new(processingMs);
            _newProcessingStatistics?.Invoke(this, stats);
        }

        public ITracerBag Tracers => throw new NotImplementedException();
        public void Start() => throw new NotImplementedException();
        public Task StopAsync(bool processRemainingBlocks = false) => throw new NotImplementedException();
        public Block Process(Block block, ProcessingOptions options, IBlockTracer tracer, CancellationToken token = default) => throw new NotImplementedException();
        public bool IsProcessingBlocks(ulong? maxProcessingInterval) => throw new NotImplementedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class StubBlockStatistics : BlockStatistics
    {
        public StubBlockStatistics(double processingMs) => SetProcessingMs(this, processingMs);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_ProcessingMs")]
        private static extern void SetProcessingMs(BlockStatistics instance, double value);
    }

    private sealed class StubMainProcessingContext(IBlockchainProcessor blockchainProcessor) : IMainProcessingContext
    {
        public ITransactionProcessor TransactionProcessor => throw new NotImplementedException();
        public IBranchProcessor BranchProcessor => throw new NotImplementedException();
        public IBlockProcessor BlockProcessor => throw new NotImplementedException();
        public IBlockchainProcessor BlockchainProcessor => blockchainProcessor;
        public IBlockProcessingQueue BlockProcessingQueue => throw new NotImplementedException();
        public IWorldState WorldState => throw new NotImplementedException();
        public IGenesisLoader GenesisLoader => throw new NotImplementedException();
#pragma warning disable CS0067
        public event EventHandler<TxProcessedEventArgs>? TransactionProcessed;
#pragma warning restore CS0067
    }
}
