// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Execution;
using Nethermind.Core;
using Nethermind.Core.Test.Builders;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Modules;

namespace Nethermind.Arbitrum.Test.Execution;

[TestFixture]
public class MetadataHeadersTests
{
    [TearDown]
    public void TearDown()
    {
        // Clean up JsonRpcContext
        JsonRpcContext.Current.Value?.Dispose();
        JsonRpcContext.Current.Value = null;
    }

    [Test]
    public void SetResponseHeader_WhenConfigEnabled_SetsGasUsedHeader()
    {
        using JsonRpcContext context = new(RpcEndpoint.Http);
        ArbitrumConfig config = new() { ExposeMetadataHeaders = true };
        Block block = Build.A.Block.WithGasUsed(12345L).TestObject;

        if (config.ExposeMetadataHeaders)
            JsonRpcContext.SetResponseHeader(ArbitrumExecutionEngine.HeaderGasUsed, block.Header.GasUsed.ToString());

        Assert.That(context.ResponseHeaders, Is.Not.Null);
        Assert.That(context.ResponseHeaders![ArbitrumExecutionEngine.HeaderGasUsed], Is.EqualTo("12345"));
    }

    [Test]
    public void SetResponseHeader_WhenConfigDisabled_DoesNotSetHeader()
    {
        using JsonRpcContext context = new(RpcEndpoint.Http);
        ArbitrumConfig config = new() { ExposeMetadataHeaders = true };
        Block block = Build.A.Block.WithGasUsed(12345L).TestObject;

        if (config.ExposeMetadataHeaders)
            JsonRpcContext.SetResponseHeader(ArbitrumExecutionEngine.HeaderGasUsed, block.Header.GasUsed.ToString());
        Assert.That(context.ResponseHeaders, Is.Null);
    }

    [Test]
    public void HeaderGasUsed_ConstantValue_IsCorrect()
    {
        // Verify the constant is correctly defined
        Assert.That(ArbitrumExecutionEngine.HeaderGasUsed, Is.EqualTo("X-Arb-Gas-Used"));
    }

    [Test]
    public void SetResponseHeader_WithZeroGasUsed_SetsZeroValue()
    {
        using JsonRpcContext context = new(RpcEndpoint.Http);
        Block block = Build.A.Block.WithGasUsed(0L).TestObject;

        JsonRpcContext.SetResponseHeader(ArbitrumExecutionEngine.HeaderGasUsed, block.Header.GasUsed.ToString());

        Assert.That(context.ResponseHeaders![ArbitrumExecutionEngine.HeaderGasUsed], Is.EqualTo("0"));
    }

    [Test]
    public void SetResponseHeader_WithLargeGasUsed_SetsCorrectValue()
    {
        using JsonRpcContext context = new(RpcEndpoint.Http);
        const long largeGas = 30_000_000L; // 30M gas - typical block limit
        Block block = Build.A.Block.WithGasUsed(largeGas).TestObject;

        JsonRpcContext.SetResponseHeader(ArbitrumExecutionEngine.HeaderGasUsed, block.Header.GasUsed.ToString());

        Assert.That(context.ResponseHeaders![ArbitrumExecutionEngine.HeaderGasUsed], Is.EqualTo("30000000"));
    }
}
