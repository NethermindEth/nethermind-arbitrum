// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Config;
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
    public async Task ShouldTriggerMaintenance_ThresholdEnabled_BelowThreshold_ReturnsFalse()
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
}
