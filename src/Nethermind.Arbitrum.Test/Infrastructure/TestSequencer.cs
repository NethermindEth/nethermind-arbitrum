// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Net;
using System.Net.Sockets;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Core;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public static class TestSequencer
{
    public static SequencedMsg ExpectedSequencedMessage(BlockHeader header, StartSequencingEnvironment env, byte[] timeboostBlockMetadata)
    {
        ArbitrumBlockHeaderInfo headerInfo = ArbitrumBlockHeaderInfo.Deserialize(header, NullLogger.Instance);

        L1IncomingMessageHeader l1MessageHeader = new(ArbitrumL1MessageKind.L2Message, ArbosAddresses.BatchPosterAddress, env.L1BLockNumber, env.L2Timestamp, null, null);
        L1IncomingMessage l1Message = new(l1MessageHeader, null, null, null);

        return new SequencedMsg(
            (ulong)header.Number,
            new MessageWithMetadata(l1Message, header.Nonce),
            new MessageResultForRpc { Hash = header.Hash, SendRoot = headerInfo.SendRoot },
            timeboostBlockMetadata);
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
