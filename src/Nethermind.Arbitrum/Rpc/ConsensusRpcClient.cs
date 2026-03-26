// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Config;
using Nethermind.JsonRpc.Client;
using Nethermind.Logging;
using Nethermind.Serialization.Json;

namespace Nethermind.Arbitrum.Rpc;

public interface IConsensusRpcClient
{
    Task<byte[]?> GetBlockMetadataAsync(long blockNumber);
}

public sealed class DisabledConsensusRpcClient(ILogManager logManager) : IConsensusRpcClient
{
    private readonly ILogger _logger = logManager.GetClassLogger<DisabledConsensusRpcClient>();

    public Task<byte[]?> GetBlockMetadataAsync(long blockNumber)
    {
        if (_logger.IsWarn)
            _logger.Warn($"No BlockMetadata is available as {nameof(ArbitrumConfig.ConsensusNodeRpcUrl)} config parameter is not specified.");

        return Task.FromResult<byte[]?>(null);
    }
}

public sealed class ConsensusRpcClient(
    IArbitrumConfig arbitrumConfig,
    IArbitrumSpecHelper specHelper,
    IJsonSerializer jsonSerializer,
    ILogManager logManager) : IConsensusRpcClient, IDisposable
{
    private readonly BasicJsonRpcClient _rpcClient = new(new Uri(arbitrumConfig.ConsensusNodeRpcUrl), jsonSerializer, logManager);
    private readonly ulong _genesisBlockNum = specHelper.GenesisBlockNum;
    private readonly ILogger _logger = logManager.GetClassLogger<ConsensusRpcClient>();

    public async Task<byte[]?> GetBlockMetadataAsync(long blockNumber)
    {
        if (blockNumber < (long)_genesisBlockNum)
            return null;

        ulong msgIdx = (ulong)blockNumber - _genesisBlockNum;

        try
        {
            return await _rpcClient.Post<byte[]>("nitroconsensus_blockMetadataAtMessageIndex", msgIdx);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (_logger.IsWarn)
                _logger.Warn($"Failed to fetch block metadata for block {blockNumber} (msgIdx={msgIdx}): {ex.Message}");

            return null;
        }
    }

    public void Dispose() => _rpcClient.Dispose();
}
