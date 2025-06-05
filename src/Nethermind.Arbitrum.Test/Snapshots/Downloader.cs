namespace Nethermind.Arbitrum.Test.Snapshots;

using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Snapshots;

[TestFixture]
public class SnapshotDownloaderTests
{
    private const int DataSize = 1024 * 1024; // 1 MB
    private const string ArchiveName = "test-archive.bin";
    private HttpListener? _listener;
    private string? _baseDirectory;

    [TearDown]
    public void Cleanup()
    {
        _listener?.Stop();
        _listener?.Close();
        if (_baseDirectory != null && Directory.Exists(_baseDirectory))
        {
            Directory.Delete(_baseDirectory, true);
        }
    }

    [Test]
    public async Task DownloadInitWithoutChecksum_ShouldDownloadAndMatchOriginal()
    {
        // Arrange
        _baseDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_baseDirectory);

        var data = GenerateRandomBytes(DataSize);
        var archivePath = Path.Combine(_baseDirectory, ArchiveName);
        await File.WriteAllBytesAsync(archivePath, data);

        string address = StartFileServer(_baseDirectory);
        var downloadPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(downloadPath);

        var config = new InitConfig
        {
            Url = $"http://{address}/{ArchiveName}",
            DownloadPath = downloadPath,
            ValidateChecksum = false
        };

        var cancellationToken = CancellationToken.None;

        // Act
        string receivedFilePath = await SnapshotDownloader.DownloadInitAsync(cancellationToken, config);

        // Assert
        Assert.That(receivedFilePath, Is.Not.Null.And.Not.Empty, "DownloadInit should return file path");
        Assert.That(File.Exists(receivedFilePath), Is.True, "Downloaded file should exist");

        var receivedData = await File.ReadAllBytesAsync(receivedFilePath);
        Assert.That(receivedData, Is.EqualTo(data), "Downloaded data should match original");
    }

    private static byte[] GenerateRandomBytes(int size)
    {
        var bytes = new byte[size];
        new Random().NextBytes(bytes);
        return bytes;
    }

    private string StartFileServer(string directory)
    {
        var listener = new HttpListener();
        var port = GetFreePort();
        var prefix = $"http://localhost:{port}/";
        listener.Prefixes.Add(prefix);
        listener.Start();
        _listener = listener;

        _ = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    var urlPath = context.Request.Url?.AbsolutePath?.TrimStart('/');
                    if (string.IsNullOrEmpty(urlPath)) continue;

                    var filePath = Path.Combine(directory, urlPath);
                    if (!File.Exists(filePath))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        context.Response.Close();
                        continue;
                    }

                    var fileBytes = await File.ReadAllBytesAsync(filePath);
                    context.Response.ContentLength64 = fileBytes.Length;
                    await context.Response.OutputStream.WriteAsync(fileBytes, 0, fileBytes.Length);
                    context.Response.Close();
                }
                catch (HttpListenerException)
                {
                    break; // Listener was stopped
                }
                catch
                {
                    // Ignore other exceptions during shutdown
                }
            }
        });

        return $"localhost:{port}";
    }

    private int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
