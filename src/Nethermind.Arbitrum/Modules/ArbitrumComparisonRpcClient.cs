// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Data;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Client;
using Nethermind.Logging;
using Nethermind.Serialization.Json;

namespace Nethermind.Arbitrum.Modules;

public sealed class ArbitrumComparisonRpcClient(string rpcUrl, IJsonSerializer jsonSerializer, ILogManager logManager, int maxRetries = 100)
    : IDisposable
{
    private readonly BasicJsonRpcClient _rpcClient = new(new Uri(rpcUrl), jsonSerializer, logManager, TimeSpan.FromSeconds(30));
    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumComparisonRpcClient>();

    public async Task<ResultWrapper<MessageResult>> GetBlockDataAsync(long blockNumber, CancellationToken cancellationToken = default)
    {
        for (int retryCount = 0; retryCount < maxRetries; retryCount++)
            try
            {
                return await FetchBlockDataAsync(blockNumber);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (_logger.IsDebug)
                    _logger.Debug($"Error fetching block {blockNumber} (attempt {retryCount + 1}/{maxRetries}): {ex.Message}");

                if (retryCount + 1 >= maxRetries)
                {
                    if (_logger.IsError)
                        _logger.Error($"Failed to fetch block {blockNumber} after {maxRetries} retries: {ex.Message}", ex);
                    return ResultWrapper<MessageResult>.Fail($"Block {blockNumber} not found after {maxRetries} retries", ErrorCodes.ResourceNotFound);
                }

                await Task.Delay(TimeSpan.FromSeconds(5 + retryCount), cancellationToken);
            }

        return ResultWrapper<MessageResult>.Fail($"Failed to get block data for block {blockNumber} after {maxRetries} retries", ErrorCodes.InternalError);
    }

    private async Task<ResultWrapper<MessageResult>> FetchBlockDataAsync(long blockNumber)
    {
        string blockNumberHex = $"0x{blockNumber:x}";

        if (_logger.IsTrace)
            _logger.Trace($"Fetching block {blockNumber} from external RPC using eth_getBlockByNumber");

        MessageResultForRpc rpcResponse = await _rpcClient.Post<MessageResultForRpc>("eth_getBlockByNumber", blockNumberHex, false);

        MessageResult result = rpcResponse.ToMessageResult();

        if (_logger.IsDebug)
            _logger.Debug($"External RPC block {blockNumber}: hash={result.BlockHash}, sendRoot={result.SendRoot}");

        return ResultWrapper<MessageResult>.Success(result);
    }

    public void Dispose() => _rpcClient.Dispose();
}
