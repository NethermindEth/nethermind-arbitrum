// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Text;
using System.Text.Json;
using Nethermind.Arbitrum.Data;
using Nethermind.Core.Crypto;
using Nethermind.JsonRpc;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Modules;

/// <summary>
/// RPC client for comparing DigestMessage results with external Arbitrum RPC endpoints.
/// </summary>
public sealed class ArbitrumComparisonRpcClient(string rpcUrl, ILogger logger, int maxRetries = 100) : IDisposable
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false
    };

    public async Task<ResultWrapper<MessageResult>> GetBlockDataAsync(long blockNumber, CancellationToken cancellationToken = default)
    {
        for (int retryCount = 0; retryCount < maxRetries; retryCount++)
            try
            {
                return await FetchBlockDataAsync(blockNumber, cancellationToken);
            }
            catch (BlockNotFoundException)
            {
                if (!await HandleRetryAsync(blockNumber, retryCount, "Block not found", cancellationToken))
                    return null!;
            }
            catch (RpcErrorException ex)
            {
                if (!await HandleRetryAsync(blockNumber, retryCount, $"RPC error: {ex.Message}", cancellationToken))
                    throw new InvalidOperationException($"RPC error after {maxRetries} retries: {ex.Message}");
            }
            catch (HttpRequestException ex)
            {
                if (!await HandleRetryAsync(blockNumber, retryCount, $"HTTP error: {ex.Message}", cancellationToken))
                    throw new InvalidOperationException($"Failed to call external RPC after {maxRetries} retries", ex);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (logger.IsError)
                    logger.Error($"Unexpected error calling external RPC for block {blockNumber}: {ex.Message}", ex);

                if (!await HandleRetryAsync(blockNumber, retryCount, $"Unexpected error: {ex.Message}", cancellationToken))
                    throw;
            }

        throw new InvalidOperationException($"Failed to get block data for block {blockNumber} after {maxRetries} retries");
    }

    private async Task<ResultWrapper<MessageResult>> FetchBlockDataAsync(long blockNumber, CancellationToken cancellationToken)
    {
        string responseJson = await SendRpcRequestAsync(blockNumber, cancellationToken);
        return ParseBlockResponse(responseJson, blockNumber);
    }

    private async Task<string> SendRpcRequestAsync(long blockNumber, CancellationToken cancellationToken)
    {
        string requestJson = CreateRpcRequest(blockNumber);

        if (logger.IsTrace)
            logger.Trace($"Sending RPC request to {rpcUrl}: {requestJson}");

        using StringContent content = new(requestJson, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await _httpClient.PostAsync(rpcUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (logger.IsTrace)
            logger.Trace($"Received RPC response: {responseJson}");

        return responseJson;
    }

    private string CreateRpcRequest(long blockNumber)
    {
        string blockNumberHex = $"0x{blockNumber:x}";

        var request = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "eth_getBlockByNumber",
            @params = new object[] { blockNumberHex, false }
        };

        return JsonSerializer.Serialize(request, _jsonOptions);
    }

    private ResultWrapper<MessageResult> ParseBlockResponse(string responseJson, long blockNumber)
    {
        using JsonDocument doc = JsonDocument.Parse(responseJson);
        JsonElement root = doc.RootElement;

        if (root.TryGetProperty("error", out JsonElement error))
        {
            string errorMessage = error.GetProperty("message").GetString() ?? "Unknown error";
            if (logger.IsWarn)
                logger.Warn($"RPC error for block {blockNumber}: {errorMessage}");
            throw new RpcErrorException(errorMessage);
        }

        if (root.TryGetProperty("result", out JsonElement result) && result.ValueKind != JsonValueKind.Null)
            return ExtractBlockData(result, blockNumber);

        if (logger.IsWarn)
            logger.Warn($"Block {blockNumber} not found in external RPC");

        throw new BlockNotFoundException(blockNumber);
    }

    private ResultWrapper<MessageResult> ExtractBlockData(JsonElement result, long blockNumber)
    {
        string? hashStr = result.GetProperty("hash").GetString();
        string? sendRootStr = result.GetProperty("extraData").GetString();

        Hash256? blockHash = hashStr is not null ? new Hash256(hashStr) : null;
        Hash256? sendRoot = sendRootStr is not null ? new Hash256(sendRootStr) : null;

        if (logger.IsDebug)
            logger.Debug($"External RPC block {blockNumber}: hash={blockHash}, sendRoot={sendRoot}");

        return ResultWrapper<MessageResult>.Success(new MessageResult
        {
            BlockHash = blockHash!,
            SendRoot = sendRoot!,
        });
    }

    private async Task<bool> HandleRetryAsync(long blockNumber, int retryCount, string reason, CancellationToken cancellationToken)
    {
        if (retryCount + 1 >= maxRetries)
            return false;

        if (logger.IsDebug)
            logger.Debug($"Retrying RPC call for block {blockNumber} (attempt {retryCount + 2}/{maxRetries}) in 5 seconds... Reason: {reason}");

        await Task.Delay(TimeSpan.FromSeconds(5 + retryCount), cancellationToken);
        return true;
    }

    public void Dispose() => _httpClient.Dispose();

    private sealed class BlockNotFoundException(long blockNumber) : Exception($"Block {blockNumber} not found");
    private sealed class RpcErrorException(string message) : Exception(message);
}
