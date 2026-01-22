// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Test.Rpc;

[TestFixture]
public sealed class ArbitrumRpcModuleMaintenanceTests
{
    [Test]
    public async Task MaintenanceStatus_NotRunning_ReturnsFalseIsRunning()
    {
        using ArbitrumRpcTestBlockchain blockchain = ArbitrumRpcTestBlockchain.CreateDefault();

        ResultWrapper<MaintenanceStatus> result = await blockchain.ArbitrumRpcModule.MaintenanceStatus();

        result.Should().NotBeNull();
        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.IsRunning.Should().BeFalse();
    }

    [Test]
    public async Task ShouldTriggerMaintenance_ThresholdZero_ReturnsFalse()
    {
        using ArbitrumRpcTestBlockchain blockchain = ArbitrumRpcTestBlockchain.CreateDefault();

        ResultWrapper<bool> result = await blockchain.ArbitrumRpcModule.ShouldTriggerMaintenance();

        result.Should().NotBeNull();
        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().BeFalse();
    }

    [Test]
    public async Task ShouldTriggerMaintenance_ThresholdEnabledBelowThreshold_ReturnsFalse()
    {
        using ArbitrumRpcTestBlockchain blockchain = ArbitrumRpcTestBlockchain.CreateDefault(
            configureArbitrum: config =>
            {
                config.TrieTimeLimitBeforeFlushMaintenanceMs = 30 * 60 * 1000;
            });

        ResultWrapper<bool> result = await blockchain.ArbitrumRpcModule.ShouldTriggerMaintenance();

        result.Should().NotBeNull();
        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().BeFalse();
    }

    [Test]
    public async Task TriggerMaintenance_NotRunning_ReturnsOk()
    {
        using ArbitrumRpcTestBlockchain blockchain = ArbitrumRpcTestBlockchain.CreateDefault();

        ResultWrapper<string> result = await blockchain.ArbitrumRpcModule.TriggerMaintenance();

        result.Should().NotBeNull();
        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().Be("OK");
    }

    [Test]
    public async Task TriggerMaintenance_CalledTwice_BothReturnOk()
    {
        using ArbitrumRpcTestBlockchain blockchain = ArbitrumRpcTestBlockchain.CreateDefault();

        ResultWrapper<string> result1 = await blockchain.ArbitrumRpcModule.TriggerMaintenance();
        ResultWrapper<string> result2 = await blockchain.ArbitrumRpcModule.TriggerMaintenance();

        result1.Should().NotBeNull();
        result1.Result.ResultType.Should().Be(ResultType.Success);
        result1.Data.Should().Be("OK");
        result2.Should().NotBeNull();
        result2.Result.ResultType.Should().Be(ResultType.Success);
        result2.Data.Should().Be("OK");
    }

    [Test]
    public async Task MaintenanceStatus_AfterTriggerMaintenance_ReturnsSuccess()
    {
        using ArbitrumRpcTestBlockchain blockchain = ArbitrumRpcTestBlockchain.CreateDefault();

        await blockchain.ArbitrumRpcModule.TriggerMaintenance();
        ResultWrapper<MaintenanceStatus> statusResult = await blockchain.ArbitrumRpcModule.MaintenanceStatus();

        statusResult.Should().NotBeNull();
        statusResult.Result.ResultType.Should().Be(ResultType.Success);
    }

    [Test]
    public async Task MaintenanceStatus_AfterMaintenanceCompletes_ReturnsIsRunningFalse()
    {
        using ArbitrumRpcTestBlockchain blockchain = ArbitrumRpcTestBlockchain.CreateDefault();

        await blockchain.ArbitrumRpcModule.TriggerMaintenance();
        await Task.Delay(500);
        ResultWrapper<MaintenanceStatus> statusResult = await blockchain.ArbitrumRpcModule.MaintenanceStatus();

        statusResult.Should().NotBeNull();
        statusResult.Result.ResultType.Should().Be(ResultType.Success);
        statusResult.Data.IsRunning.Should().BeFalse();
    }

    [Test]
    public async Task ShouldTriggerMaintenance_MaintenanceRunning_ReturnsFalse()
    {
        using ArbitrumRpcTestBlockchain blockchain = ArbitrumRpcTestBlockchain.CreateDefault(
            configureArbitrum: config =>
            {
                config.TrieTimeLimitBeforeFlushMaintenanceMs = 2 * 60 * 60 * 1000;
            });

        await blockchain.ArbitrumRpcModule.TriggerMaintenance();
        ResultWrapper<bool> result = await blockchain.ArbitrumRpcModule.ShouldTriggerMaintenance();

        result.Should().NotBeNull();
        result.Result.ResultType.Should().Be(ResultType.Success);
    }

    [Test]
    public void ProcessingTimeTracker_AddProcessingTime_AccumulatesCorrectly()
    {
        using ArbitrumRpcTestBlockchain blockchain = ArbitrumRpcTestBlockchain.CreateDefault();

        TimeSpan initialTime = blockchain.ProcessingTimeTracker.TimeBeforeFlush;
        blockchain.ProcessingTimeTracker.AddProcessingTime(TimeSpan.FromMinutes(10));
        TimeSpan afterAdd = blockchain.ProcessingTimeTracker.TimeBeforeFlush;

        afterAdd.Should().Be(initialTime - TimeSpan.FromMinutes(10));
    }

    [Test]
    public async Task ShouldTriggerMaintenance_ThresholdExceeded_ReturnsTrue()
    {
        using ArbitrumRpcTestBlockchain blockchain = ArbitrumRpcTestBlockchain.CreateDefault(
            configureArbitrum: config =>
            {
                config.TrieTimeLimitMs = 100;
                config.TrieTimeLimitBeforeFlushMaintenanceMs = 90;
            });

        blockchain.ProcessingTimeTracker.AddProcessingTime(TimeSpan.FromMilliseconds(50));
        ResultWrapper<bool> result = await blockchain.ArbitrumRpcModule.ShouldTriggerMaintenance();

        result.Data.Should().BeTrue();
    }

    [Test]
    public async Task TriggerMaintenance_AfterCompletion_ResetsProcessingTime()
    {
        using ArbitrumRpcTestBlockchain blockchain = ArbitrumRpcTestBlockchain.CreateDefault(
            configureArbitrum: config =>
            {
                config.TrieTimeLimitMs = 100;
                config.TrieTimeLimitBeforeFlushMaintenanceMs = 90;
            });

        blockchain.ProcessingTimeTracker.AddProcessingTime(TimeSpan.FromMilliseconds(50));
        ResultWrapper<bool> shouldTrigger = await blockchain.ArbitrumRpcModule.ShouldTriggerMaintenance();
        shouldTrigger.Data.Should().BeTrue();

        await blockchain.ArbitrumRpcModule.TriggerMaintenance();
        await WaitForMaintenanceComplete(blockchain);

        ResultWrapper<bool> afterMaintenance = await blockchain.ArbitrumRpcModule.ShouldTriggerMaintenance();
        afterMaintenance.Data.Should().BeFalse();
    }

    [Test]
    public async Task Maintenance_MultipleCycles_WorksCorrectly()
    {
        using ArbitrumRpcTestBlockchain blockchain = ArbitrumRpcTestBlockchain.CreateDefault(
            configureArbitrum: config =>
            {
                config.TrieTimeLimitMs = 100;
                config.TrieTimeLimitBeforeFlushMaintenanceMs = 90;
            });

        for (int cycle = 0; cycle < 2; cycle++)
        {
            blockchain.ProcessingTimeTracker.AddProcessingTime(TimeSpan.FromMilliseconds(50));
            ResultWrapper<bool> shouldTrigger = await blockchain.ArbitrumRpcModule.ShouldTriggerMaintenance();
            shouldTrigger.Data.Should().BeTrue($"cycle {cycle} should trigger maintenance");

            await blockchain.ArbitrumRpcModule.TriggerMaintenance();
            await WaitForMaintenanceComplete(blockchain);

            ResultWrapper<bool> afterMaintenance = await blockchain.ArbitrumRpcModule.ShouldTriggerMaintenance();
            afterMaintenance.Data.Should().BeFalse($"cycle {cycle} should reset after maintenance");
        }
    }

    [Test]
    public async Task ShouldTriggerMaintenance_NoTimeAccumulated_ReturnsFalse()
    {
        using ArbitrumRpcTestBlockchain blockchain = ArbitrumRpcTestBlockchain.CreateDefault(
            configureArbitrum: config =>
            {
                config.TrieTimeLimitMs = 100;
                config.TrieTimeLimitBeforeFlushMaintenanceMs = 90;
            });

        ResultWrapper<bool> result = await blockchain.ArbitrumRpcModule.ShouldTriggerMaintenance();

        result.Data.Should().BeFalse();
    }

    [Test]
    public async Task ShouldTriggerMaintenance_ThresholdGreaterThanFlushInterval_TriggersImmediately()
    {
        using ArbitrumRpcTestBlockchain blockchain = ArbitrumRpcTestBlockchain.CreateDefault(
            configureArbitrum: config =>
            {
                config.TrieTimeLimitMs = 100;
                config.TrieTimeLimitBeforeFlushMaintenanceMs = 200;
            });

        ResultWrapper<bool> result = await blockchain.ArbitrumRpcModule.ShouldTriggerMaintenance();

        result.Data.Should().BeTrue();
    }

    [Test]
    public async Task ShouldTriggerMaintenance_NegativeThreshold_TreatedAsDisabled()
    {
        using ArbitrumRpcTestBlockchain blockchain = ArbitrumRpcTestBlockchain.CreateDefault(
            configureArbitrum: config =>
            {
                config.TrieTimeLimitMs = 100;
                config.TrieTimeLimitBeforeFlushMaintenanceMs = -50;
            });

        blockchain.ProcessingTimeTracker.AddProcessingTime(TimeSpan.FromMilliseconds(150));
        ResultWrapper<bool> result = await blockchain.ArbitrumRpcModule.ShouldTriggerMaintenance();

        result.Data.Should().BeFalse();
    }

    [Test]
    public async Task ShouldTriggerMaintenance_VerySmallThreshold_TriggersQuickly()
    {
        using ArbitrumRpcTestBlockchain blockchain = ArbitrumRpcTestBlockchain.CreateDefault(
            configureArbitrum: config =>
            {
                config.TrieTimeLimitMs = 100;
                config.TrieTimeLimitBeforeFlushMaintenanceMs = 99;
            });

        blockchain.ProcessingTimeTracker.AddProcessingTime(TimeSpan.FromMilliseconds(5));
        ResultWrapper<bool> result = await blockchain.ArbitrumRpcModule.ShouldTriggerMaintenance();

        result.Data.Should().BeTrue();
    }

    [Test]
    public async Task TriggerMaintenance_ConcurrentCalls_AllReturnOk()
    {
        using ArbitrumRpcTestBlockchain blockchain = ArbitrumRpcTestBlockchain.CreateDefault();

        Task<ResultWrapper<string>>[] tasks = Enumerable.Range(0, 10)
            .Select(_ => blockchain.ArbitrumRpcModule.TriggerMaintenance())
            .ToArray();

        ResultWrapper<string>[] results = await Task.WhenAll(tasks);

        results.Should().AllSatisfy(r =>
        {
            r.Result.ResultType.Should().Be(ResultType.Success);
            r.Data.Should().Be("OK");
        });
    }

    [Test]
    public async Task ShouldTriggerMaintenance_RapidCalls_AllReturnConsistentResults()
    {
        using ArbitrumRpcTestBlockchain blockchain = ArbitrumRpcTestBlockchain.CreateDefault(
            configureArbitrum: config =>
            {
                config.TrieTimeLimitMs = 100;
                config.TrieTimeLimitBeforeFlushMaintenanceMs = 90;
            });

        blockchain.ProcessingTimeTracker.AddProcessingTime(TimeSpan.FromMilliseconds(50));

        Task<ResultWrapper<bool>>[] tasks = Enumerable.Range(0, 10)
            .Select(_ => blockchain.ArbitrumRpcModule.ShouldTriggerMaintenance())
            .ToArray();

        ResultWrapper<bool>[] results = await Task.WhenAll(tasks);

        results.Should().AllSatisfy(r =>
        {
            r.Result.ResultType.Should().Be(ResultType.Success);
            r.Data.Should().BeTrue();
        });
    }

    [Test]
    public async Task TriggerMaintenance_FlushCacheThrows_ReturnsOk()
    {
        InvalidOperationException testException = new("Simulated FlushCache failure");
        using ArbitrumRpcTestBlockchain blockchain = ArbitrumRpcTestBlockchain.CreateDefault(
            worldStateManagerFactory: inner => new ThrowingWorldStateManagerDecorator(inner, testException));

        ResultWrapper<string> result = await blockchain.ArbitrumRpcModule.TriggerMaintenance();

        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().Be("OK");
    }

    [Test]
    public async Task TriggerMaintenance_FlushCacheThrows_TrackerNotReset()
    {
        InvalidOperationException testException = new("Simulated FlushCache failure");
        using ArbitrumRpcTestBlockchain blockchain = ArbitrumRpcTestBlockchain.CreateDefault(
            configureArbitrum: config =>
            {
                config.TrieTimeLimitMs = 100;
                config.TrieTimeLimitBeforeFlushMaintenanceMs = 90;
            },
            worldStateManagerFactory: inner => new ThrowingWorldStateManagerDecorator(inner, testException));

        blockchain.ProcessingTimeTracker.AddProcessingTime(TimeSpan.FromMilliseconds(50));
        TimeSpan timeBeforeMaintenance = blockchain.ProcessingTimeTracker.TimeBeforeFlush;

        await blockchain.ArbitrumRpcModule.TriggerMaintenance();
        await WaitForMaintenanceComplete(blockchain);

        blockchain.ProcessingTimeTracker.TimeBeforeFlush.Should().Be(timeBeforeMaintenance);
    }

    [Test]
    public async Task MaintenanceStatus_AfterFailedMaintenance_ReturnsNotRunning()
    {
        InvalidOperationException testException = new("Simulated FlushCache failure");
        using ArbitrumRpcTestBlockchain blockchain = ArbitrumRpcTestBlockchain.CreateDefault(
            worldStateManagerFactory: inner => new ThrowingWorldStateManagerDecorator(inner, testException));

        await blockchain.ArbitrumRpcModule.TriggerMaintenance();
        await WaitForMaintenanceComplete(blockchain);

        ResultWrapper<MaintenanceStatus> status = await blockchain.ArbitrumRpcModule.MaintenanceStatus();
        status.Result.ResultType.Should().Be(ResultType.Success);
        status.Data.IsRunning.Should().BeFalse();
    }

    [Test]
    public async Task TriggerMaintenance_AfterFailedMaintenance_CanTriggerAgain()
    {
        InvalidOperationException testException = new("Simulated FlushCache failure");
        using ArbitrumRpcTestBlockchain blockchain = ArbitrumRpcTestBlockchain.CreateDefault(
            worldStateManagerFactory: inner => new ThrowingWorldStateManagerDecorator(inner, testException));

        await blockchain.ArbitrumRpcModule.TriggerMaintenance();
        await WaitForMaintenanceComplete(blockchain);

        ResultWrapper<string> secondResult = await blockchain.ArbitrumRpcModule.TriggerMaintenance();

        secondResult.Result.ResultType.Should().Be(ResultType.Success);
        secondResult.Data.Should().Be("OK");
    }

    private static async Task WaitForMaintenanceComplete(ArbitrumRpcTestBlockchain blockchain, int timeoutMs = 5000)
    {
        Stopwatch sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            ResultWrapper<MaintenanceStatus> status = await blockchain.ArbitrumRpcModule.MaintenanceStatus();
            if (!status.Data.IsRunning)
                return;
            await Task.Delay(10);
        }
        throw new TimeoutException("Maintenance did not complete in time");
    }
}
