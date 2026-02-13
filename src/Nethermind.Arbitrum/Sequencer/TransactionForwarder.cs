// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Text;
using System.Text.Json;
using Nethermind.Core;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Sequencer;

/// <summary>
/// HTTP client that forwards eth_sendRawTransaction to a backup sequencer URL.
/// Mirrors Go TxForwarder concept from sequencer.go.
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
        Timeout = timeout ?? TimeSpan.FromSeconds(30)
    };

    private readonly CancellationTokenSource _cts = new();
    private readonly ILogger _logger = logManager.GetClassLogger<TransactionForwarder>();

    public string PrimaryTarget { get; } = targetUrl;

    /// <summary>
    /// Forwards a transaction to the backup sequencer via eth_sendRawTransaction JSON-RPC.
    /// Returns null on success, an exception on failure.
    /// </summary>
    public async Task<Exception?> ForwardTransactionAsync(Transaction tx, CancellationToken ct)
    {
        if (_cts.IsCancellationRequested)
            return new InvalidOperationException("Sequencer temporarily not available");

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);

        try
        {
            byte[] rlpEncoded = Rlp.Encode(tx).Bytes;
            string rawTxHex = "0x" + Convert.ToHexString(rlpEncoded).ToLowerInvariant();

            JsonRpcRequest request = new()
            {
                JsonRpc = "2.0",
                Method = "eth_sendRawTransaction",
                Params = [rawTxHex],
                Id = 1
            };

            string json = JsonSerializer.Serialize(request, JsonOptions);
            using StringContent content = new(json, Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await _httpClient.PostAsync("", content, linked.Token);

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(linked.Token);
                return new InvalidOperationException($"Forward failed with status {response.StatusCode}: {body}");
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
                    return new NoSequencerException(errorMsg);

                return new InvalidOperationException($"Forward RPC error: {errorMsg}");
            }

            return null;
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            return new InvalidOperationException("Forwarder has been disabled");
        }
        catch (OperationCanceledException)
        {
            return new OperationCanceledException("Forward cancelled");
        }
        catch (Exception ex)
        {
            if (_logger.IsWarn)
                _logger.Warn($"Error forwarding transaction to {PrimaryTarget}: {ex.Message}");
            return ex;
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

/// <summary>
/// Indicates the backup sequencer is also not sequencing (equivalent to Go's ErrNoSequencer).
/// </summary>
public class NoSequencerException(string message) : Exception(message);
