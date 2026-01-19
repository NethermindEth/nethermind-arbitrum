// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Modules;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Test.Rpc;

[TestFixture]
public sealed class ArbitrumRpcModuleSetConsensusSyncDataTests
{
    private ArbitrumRpcTestBlockchain? _blockchain;
    private IArbitrumRpcModule _rpcModule = null!;

    [Test]
    public void SetConsensusSyncData_WithNullParameters_ReturnsFailure()
    {
        ResultWrapper<string> result = _rpcModule.SetConsensusSyncData(null);

        result.Should().NotBeNull();
        result.Result.ResultType.Should().Be(ResultType.Failure);
        result.Result.Error.Should().Contain("Parameters cannot be null");
        result.ErrorCode.Should().Be(ErrorCodes.InvalidParams);
    }

    [Test]
    public void SetConsensusSyncData_WithValidParameters_ReturnsSuccess()
    {
        SetConsensusSyncDataParams parameters = new()
        {
            Synced = true,
            MaxMessageCount = 100,
            UpdatedAt = DateTime.UtcNow
        };

        ResultWrapper<string> result = _rpcModule.SetConsensusSyncData(parameters);

        result.Should().NotBeNull();
        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().Be("OK");
    }

    [SetUp]
    public void SetUp()
    {
        _blockchain = ArbitrumRpcTestBlockchain.CreateDefault();
        _rpcModule = _blockchain.ArbitrumRpcModule;
    }

    [TearDown]
    public void TearDown()
    {
        _blockchain?.Dispose();
    }
}
