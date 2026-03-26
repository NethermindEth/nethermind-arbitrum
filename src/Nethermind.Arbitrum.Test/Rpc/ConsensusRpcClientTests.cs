// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Rpc;
using Nethermind.Logging;
using Nethermind.Serialization.Json;

namespace Nethermind.Arbitrum.Test.Rpc;

[TestFixture]
public class ConsensusRpcClientTests
{
    [Test]
    public async Task GetBlockMetadataAsync_DisabledClient_ReturnsNull()
    {
        DisabledConsensusRpcClient client = new(LimboLogs.Instance);

        byte[]? result = await client.GetBlockMetadataAsync(100);

        result.Should().BeNull();
    }

    [Test]
    public async Task GetBlockMetadataAsync_BlockBeforeGenesis_ReturnsNull()
    {
        using ConsensusRpcClient client = CreateClient(genesisBlockNum: 100);

        byte[]? result = await client.GetBlockMetadataAsync(99);

        result.Should().BeNull();
    }

    [Test]
    public async Task GetBlockMetadataAsync_RpcCallFails_ReturnsNull()
    {
        using ConsensusRpcClient client = CreateClient(
            genesisBlockNum: 0,
            consensusUrl: "http://127.0.0.1:1");

        byte[]? result = await client.GetBlockMetadataAsync(10);

        result.Should().BeNull();
    }

    private static ConsensusRpcClient CreateClient(
        ulong genesisBlockNum = 0,
        string consensusUrl = "http://localhost:1")
    {
        ArbitrumConfig config = new() { ConsensusNodeRpcUrl = consensusUrl };
        ArbitrumSpecHelper specHelper = new(new ArbitrumChainSpecEngineParameters { GenesisBlockNum = genesisBlockNum });
        return new ConsensusRpcClient(config, specHelper, new EthereumJsonSerializer(), LimboLogs.Instance);
    }
}
