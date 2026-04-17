// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Net;
using System.Net.Sockets;
using ConcurrentCollections;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public class TestHttpServer : IDisposable
{
    private readonly HttpListener _listener;

    private TestHttpServer(HttpListener listener, string uri)
    {
        _listener = listener;
        Uri = uri;
    }

    public string Uri { get; }

    public static TestHttpServer Start()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            TcpListener tcp = new(IPAddress.Loopback, 0);
            tcp.Start();
            int port = ((IPEndPoint)tcp.LocalEndpoint).Port;
            string uri = $"http://localhost:{port}/";

            HttpListener listener = new();
            listener.Prefixes.Add(uri);

            tcp.Stop(); // release the port…
            try
            {
                listener.Start(); // …and immediately rebind it
                return new TestHttpServer(listener, uri);
            }
            catch (HttpListenerException)
            {
                listener.Close();
            }
        }

        throw new InvalidOperationException($"Could not get free port for the {nameof(TestHttpServer)}");
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

    public void Dispose()
    {
        ((IDisposable)_listener).Dispose();
    }
}
