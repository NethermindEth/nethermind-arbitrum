// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Net;
using System.Net.Sockets;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Core;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public static class TestSequencer
{
    public static SequencedMsg ExpectedSequencedMessage(BlockHeader header, StartSequencingEnvironment env, byte[][] transactionRlps, byte[] timeboostBlockMetadata)
    {
        MessageWithMetadata messageWithMetadata =
            L2MessageAssembler.AssembleFromSignedTransactions(transactionRlps, env.L1BLockNumber, env.L2Timestamp, header.Nonce);

        ArbitrumBlockHeaderInfo headerInfo = ArbitrumBlockHeaderInfo.Deserialize(header, NullLogger.Instance);
        MessageResultForRpc messageResultForRpc = new() { Hash = header.Hash, SendRoot = headerInfo.SendRoot };

        return new SequencedMsg((ulong)header.Number, messageWithMetadata, messageResultForRpc, timeboostBlockMetadata);
    }

    public static SequencedMsg ExpectedSequencedMessage(BlockHeader header, L1IncomingMessage delayedMessage, ulong delayedMessagesRead, byte[] timeboostBlockMetadata)
    {
        MessageWithMetadata messageWithMetadata = new(delayedMessage, delayedMessagesRead);

        ArbitrumBlockHeaderInfo headerInfo = ArbitrumBlockHeaderInfo.Deserialize(header, NullLogger.Instance);
        MessageResultForRpc messageResultForRpc = new() { Hash = header.Hash, SendRoot = headerInfo.SendRoot };

        return new SequencedMsg((ulong)header.Number, messageWithMetadata, messageResultForRpc, timeboostBlockMetadata);
    }
}

public record StartSequencingEnvironment(ulong L1BLockNumber, ulong L1Timestamp, ulong L2Timestamp)
{
    public static StartSequencingEnvironment FromNowUtc(ulong l1BlockNumber = 1)
    {
        ulong now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return new(l1BlockNumber, now - 500, now);
    }
}

public class TestRemoteSequencer : IDisposable
{
    private readonly HttpListener _listener;

    private TestRemoteSequencer(HttpListener listener, string uri)
    {
        _listener = listener;
        Uri = uri;
    }

    public string Uri { get; }

    public static TestRemoteSequencer Start()
    {
        HttpListener listener = new();
        string uri = GetLocalhostUri();
        listener.Prefixes.Add(uri);
        listener.Start();

        return new TestRemoteSequencer(listener, uri);
    }

    public async Task Handle(Func<string, byte[]> handle, string contentType = "application/json")
    {
        HttpListenerContext ctx = await _listener.GetContextAsync();
        using StreamReader reader = new(ctx.Request.InputStream);
        string body = await reader.ReadToEndAsync();

        byte[] response = handle(body);

        ctx.Response.ContentType = contentType;
        ctx.Response.ContentLength64 = response.Length;
        await ctx.Response.OutputStream.WriteAsync(response);
        ctx.Response.Close();
    }

    private static string GetLocalhostUri()
    {
        using TcpListener tcp = new(IPAddress.Loopback, 0);
        tcp.Start();
        int port = ((IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();
        return $"http://localhost:{port}/";
    }

    public void Dispose()
    {
        ((IDisposable)_listener).Dispose();
    }
}
