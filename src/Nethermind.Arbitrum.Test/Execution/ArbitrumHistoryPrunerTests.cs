// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Execution;
using Nethermind.Blockchain;
using Nethermind.Blockchain.BlockAccessLists;
using Nethermind.Blockchain.Receipts;
using Nethermind.Blockchain.Synchronization;
using Nethermind.Config;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Scheduler;
using Nethermind.Core.Specs;
using Nethermind.Db;
using Nethermind.History;
using Nethermind.Logging;
using Nethermind.State.Repositories;
using NSubstitute;

namespace Nethermind.Arbitrum.Test.Execution;

[TestFixture]
public sealed class ArbitrumHistoryPrunerTests
{
    private CapturingScheduler _scheduler = null!;
    private HistoryPruner _inner = null!;

    [SetUp]
    public void SetUp()
    {
        _scheduler = new CapturingScheduler();
        _inner = BuildHistoryPruner(_scheduler);
    }

    [Test]
    public async Task SchedulePruneHistory_WhenBuildBlocksOnMainStateEnabled_CallsInnerPruner()
    {
        ArbitrumHistoryPruner pruner = new(_inner, new BlocksConfig { BuildBlocksOnMainState = true });

        pruner.SchedulePruneHistory();

        await _scheduler.Invoked.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _scheduler.WasCalled.Should().BeTrue();
    }

    [Test]
    public async Task SchedulePruneHistory_WhenBuildBlocksOnMainStateDisabled_DoesNotCallInnerPruner()
    {
        ArbitrumHistoryPruner pruner = new(_inner, new BlocksConfig { BuildBlocksOnMainState = false });

        pruner.SchedulePruneHistory();

        // Allow time for any Task.Run that might have been queued to execute
        await Task.Delay(100);
        _scheduler.WasCalled.Should().BeFalse();
    }

    private static HistoryPruner BuildHistoryPruner(IBackgroundTaskScheduler scheduler)
    {
        ISpecProvider specProvider = Substitute.For<ISpecProvider>();
        IReleaseSpec genesisSpec = Substitute.For<IReleaseSpec>();
        specProvider.GenesisSpec.Returns(genesisSpec);

        IDbProvider dbProvider = Substitute.For<IDbProvider>();
        dbProvider.MetadataDb.Returns(Substitute.For<IDb>());

        IProcessExitSource processExitSource = Substitute.For<IProcessExitSource>();
        processExitSource.Token.Returns(CancellationToken.None);

        return new HistoryPruner(
            blockTree: Substitute.For<IBlockTree>(),
            receiptStorage: Substitute.For<IReceiptStorage>(),
            blockAccessListStore: Substitute.For<IBlockAccessListStore>(),
            specProvider: specProvider,
            chainLevelInfoRepository: Substitute.For<IChainLevelInfoRepository>(),
            dbProvider: dbProvider,
            historyConfig: new HistoryConfig { Pruning = PruningModes.Disabled },
            blocksConfig: new BlocksConfig(),
            syncConfig: Substitute.For<ISyncConfig>(),
            processExitSource: processExitSource,
            backgroundTaskScheduler: scheduler,
            blockProcessingQueue: Substitute.For<IBlockProcessingQueue>(),
            logManager: LimboLogs.Instance);
    }

    private sealed class CapturingScheduler : IBackgroundTaskScheduler
    {
        public bool WasCalled { get; private set; }
        public TaskCompletionSource Invoked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool TryScheduleTask<TReq>(TReq request, Func<TReq, CancellationToken, Task> fulfillFunc, TimeSpan? timeout = null, string? source = null)
        {
            WasCalled = true;
            Invoked.TrySetResult();
            return true;
        }
    }
}
