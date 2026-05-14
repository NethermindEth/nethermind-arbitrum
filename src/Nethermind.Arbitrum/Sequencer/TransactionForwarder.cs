// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text;
using System.Text.Json;
using Nethermind.Arbitrum.Data;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.JsonRpc;
using Nethermind.Logging;
using Nethermind.Serialization.Json;

namespace Nethermind.Arbitrum.Sequencer;

public class TransactionForwarder(string targetUrl, ILogManager logManager, TimeSpan? timeout = null) : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = EthereumJsonSerializer.JsonOptions;

    private readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri(targetUrl),
        Timeout = timeout ?? TimeSpan.FromSeconds(120)
    };

    private readonly CancellationTokenSource _cts = new();
    private readonly ILogger _logger = logManager.GetClassLogger<TransactionForwarder>();

    public string PrimaryTarget { get; } = targetUrl;

    public Task<ResultWrapper<Hash256>> ForwardTransactionAsync(byte[] rlpEncoded, Hash256 txHash, CancellationToken ct)
        => ForwardRpcCallAsync("eth_sendRawTransaction", [rlpEncoded.ToHexString(withZeroX: true)], txHash, ct);

    public Task<ResultWrapper<Hash256>> ForwardTransactionAsync(byte[] rlpEncoded, ConditionalOptions? options, Hash256 txHash, CancellationToken ct)
        => options is not null
            ? ForwardRpcCallAsync("eth_sendRawTransactionConditional", [rlpEncoded.ToHexString(withZeroX: true), options], txHash, ct)
            : ForwardRpcCallAsync("eth_sendRawTransaction", [rlpEncoded.ToHexString(withZeroX: true)], txHash, ct);

    public void Disable()
    {
        _cts.Cancel();
    }

    public void Dispose()
    {
        Disable();
        _httpClient.Dispose();
        _cts.Dispose();
    }

    private async Task<ResultWrapper<Hash256>> ForwardRpcCallAsync(string method, object[] rpcParams, Hash256 txHash, CancellationToken ct)
    {
        if (_cts.IsCancellationRequested)
            return ResultWrapper<Hash256>.Fail("Sequencer temporarily not available", ErrorCodes.TransactionRejected);

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);

        try
        {
            JsonRpcRequest request = new()
            {
                JsonRpc = "2.0",
                Method = method,
                Params = rpcParams,
                Id = 1
            };

            string json = JsonSerializer.Serialize(request, JsonOptions);
            using StringContent content = new(json, Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await _httpClient.PostAsync("", content, linked.Token);

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(linked.Token);
                return ResultWrapper<Hash256>.Fail($"Forward failed with status {response.StatusCode}: {body}", ErrorCodes.TransactionRejected);
            }

            string responseBody = await response.Content.ReadAsStringAsync(linked.Token);
            using JsonDocument doc = JsonDocument.Parse(responseBody);

            if (doc.RootElement.TryGetProperty("error", out JsonElement errorElement))
            {
                string errorMsg = errorElement.TryGetProperty("message", out JsonElement msgElement)
                    ? msgElement.GetString() ?? "Unknown error"
                    : "Unknown error";

                if (errorMsg.Contains("sequencer temporarily not available", StringComparison.OrdinalIgnoreCase)
                    || errorMsg.Contains("no sequencer", StringComparison.OrdinalIgnoreCase))
                    return ResultWrapper<Hash256>.Fail(errorMsg, ArbitrumSequencerErrors.NoSequencer);

                return ResultWrapper<Hash256>.Fail($"Forward RPC error: {errorMsg}", ErrorCodes.TransactionRejected);
            }

            return ResultWrapper<Hash256>.Success(txHash);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            return ResultWrapper<Hash256>.Fail("Forwarder has been disabled", ErrorCodes.TransactionRejected);
        }
        catch (OperationCanceledException)
        {
            return ResultWrapper<Hash256>.Fail("Forward cancelled", ErrorCodes.TransactionRejected);
        }
        catch (Exception ex)
        {
            if (_logger.IsWarn)
                _logger.Warn($"Error forwarding transaction to {PrimaryTarget}: {ex.Message}");
            return ResultWrapper<Hash256>.Fail(ex.Message, ErrorCodes.TransactionRejected);
        }
    }

    private class JsonRpcRequest
    {
        public string JsonRpc { get; init; } = "2.0";
        public string Method { get; init; } = "";
        public object[] Params { get; init; } = [];
        public int Id { get; init; }
    }
}
