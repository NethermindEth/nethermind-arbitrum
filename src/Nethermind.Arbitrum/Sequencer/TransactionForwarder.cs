// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text;
using System.Text.Json;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.JsonRpc;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Sequencer;

/// <summary>
/// HTTP client that forwards eth_sendRawTransaction to a backup sequencer URL.
/// </summary>
public class TransactionForwarder(string targetUrl, ILogManager logManager, TimeSpan? timeout = null) : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri(targetUrl),
        Timeout = timeout ?? TimeSpan.FromSeconds(120)
    };

    private readonly CancellationTokenSource _cts = new();
    private readonly ILogger _logger = logManager.GetClassLogger<TransactionForwarder>();

    public string PrimaryTarget { get; } = targetUrl;

    /// <summary>
    /// Forwards a transaction to the backup sequencer via eth_sendRawTransaction JSON-RPC.
    /// </summary>
    public async Task<ResultWrapper<Hash256>> ForwardTransactionAsync(byte[] rlpEncoded, Hash256 txHash, CancellationToken ct)
    {
        if (_cts.IsCancellationRequested)
            return ResultWrapper<Hash256>.Fail("Sequencer temporarily not available", ErrorCodes.TransactionRejected);

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);

        try
        {
            JsonRpcRequest request = new()
            {
                JsonRpc = "2.0",
                Method = "eth_sendRawTransaction",
                Params = [rlpEncoded.ToHexString(withZeroX: true)],
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

    /// <summary>
    /// Disables the forwarder, cancelling any in-flight forwards.
    /// </summary>
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

    private class JsonRpcRequest
    {
        public string JsonRpc { get; init; } = "2.0";
        public string Method { get; init; } = "";
        public object[] Params { get; init; } = [];
        public int Id { get; init; }
    }
}
