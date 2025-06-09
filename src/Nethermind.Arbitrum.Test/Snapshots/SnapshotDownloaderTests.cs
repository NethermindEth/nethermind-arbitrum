using System.Net;
using System.Security.Cryptography;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Snapshots;
using Nethermind.Logging;
using NUnit.Framework;

namespace Nethermind.Arbitrum.Test.Snapshots;

[TestFixture]
public class SnapshotDownloaderTests
{
    private HttpListener? _listener;
    private string? _serverRoot;
    private SnapshotDownloader _downloader = null!;
    private ILogger _logger;

    [SetUp]
    public void SetUp()
    {
        _logger = new ();
        // The tests use a real HttpListener, so we pass a real HttpClient.
        _downloader = new SnapshotDownloader(_logger, new HttpClient());
    }

    [TearDown]
    public void TearDown()
    {
        _downloader?.Dispose();
        _listener?.Stop();
        _listener?.Close();

        if (_serverRoot is { } dir && Directory.Exists(dir))
            Directory.Delete(dir, true);
    }

    [Test]
    public async Task DownloadInitAsync_WithoutChecksum_Succeeds()
    {
        var data = GenerateRandomBytes(1024 * 512);
        string archiveName = "init-plain.bin";

        SetupServerDirectory();
        await File.WriteAllBytesAsync(Path.Combine(_serverRoot!, archiveName), data);

        string serverUrl = StartFileServer(_serverRoot!);
        string downloadDir = CreateTempDir();

        var config = new ArbitrumInitConfig
        {
            Url = $"http://{serverUrl}/{archiveName}",
            DownloadPath = downloadDir,
            ValidateChecksum = false
        };

        var resultPath = await _downloader.DownloadInitAsync(config, downloadDir, CancellationToken.None);

        Assert.That(File.Exists(resultPath), Is.True);
        var downloaded = await File.ReadAllBytesAsync(resultPath);
        Assert.That(downloaded, Is.EqualTo(data));
    }

    [Test]
    public async Task DownloadInitAsync_WithChecksum_Succeeds()
    {
        var data = GenerateRandomBytes(1024 * 512);
        string archiveName = "init-checksum.bin";
        string checksumFile = archiveName + ".sha256";

        SetupServerDirectory();
        await File.WriteAllBytesAsync(Path.Combine(_serverRoot!, archiveName), data);
        var hash = SHA256.HashData(data);
        await File.WriteAllTextAsync(Path.Combine(_serverRoot!, checksumFile), Convert.ToHexString(hash).ToLower());

        string serverUrl = StartFileServer(_serverRoot!);
        string downloadDir = CreateTempDir();

        var config = new ArbitrumInitConfig
        {
            Url = $"http://{serverUrl}/{archiveName}",
            DownloadPath = downloadDir,
            ValidateChecksum = true
        };

        var resultPath = await _downloader.DownloadInitAsync(config, downloadDir, CancellationToken.None);

        Assert.That(File.Exists(resultPath), Is.True);
        var downloaded = await File.ReadAllBytesAsync(resultPath);
        Assert.That(downloaded, Is.EqualTo(data));
    }

    [Test]
    public async Task DownloadInitInPartsAsync_WithValidManifest_Succeeds()
    {
        var part1 = "hello "u8.ToArray();
        var part2 = "world"u8.ToArray();
        var allData = part1.Concat(part2).ToArray();

        string partName1 = "part1.bin";
        string partName2 = "part2.bin";
        string archiveName = "snapshot.tar";

        SetupServerDirectory();

        await Task.WhenAll(
            File.WriteAllBytesAsync(Path.Combine(_serverRoot!, partName1), part1),
            File.WriteAllBytesAsync(Path.Combine(_serverRoot!, partName2), part2)
        );

        var manifest = new[]
        {
            $"{Convert.ToHexString(SHA256.HashData(part1)).ToLower()} {partName1}",
            $"{Convert.ToHexString(SHA256.HashData(part2)).ToLower()} {partName2}"
        };

        await File.WriteAllLinesAsync(Path.Combine(_serverRoot!, $"{archiveName}.manifest.txt"), manifest);

        string serverUrl = StartFileServer(_serverRoot!);
        string downloadDir = CreateTempDir();

        var config = new ArbitrumInitConfig
        {
            Url = $"http://{serverUrl}/{archiveName}",
            DownloadPath = downloadDir,
            ValidateChecksum = true
        };

        var finalArchive = await _downloader.DownloadInitInPartsAsync(config, downloadDir, CancellationToken.None);

        Assert.That(File.Exists(finalArchive), Is.True);
        var resultBytes = await File.ReadAllBytesAsync(finalArchive);
        Assert.That(resultBytes, Is.EqualTo(allData));
    }
    
    private void SetupServerDirectory()
    {
        _serverRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_serverRoot!);
    }

    private string CreateTempDir()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    private static byte[] GenerateRandomBytes(int length)
    {
        var bytes = new byte[length];
        Random.Shared.NextBytes(bytes);
        return bytes;
    }

    private string StartFileServer(string directory)
    {
        var port = GetFreePort();
        string prefix = $"http://localhost:{port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(prefix);
        _listener.Start();

        _ = Task.Run(async () =>
        {
            while (_listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    var requestPath = context.Request.Url?.AbsolutePath?.TrimStart('/');
                    if (string.IsNullOrEmpty(requestPath))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        context.Response.Close();
                        continue;
                    }

                    var fullPath = Path.Combine(directory, requestPath);
                    if (!File.Exists(fullPath))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        context.Response.Close();
                        continue;
                    }

                    var content = await File.ReadAllBytesAsync(fullPath);
                    context.Response.ContentLength64 = content.Length;
                    await context.Response.OutputStream.WriteAsync(content);
                    context.Response.Close();
                }
                catch (HttpListenerException) { break; } // Thrown on listener stop
                catch (Exception ex)
                {
                    // Suppress exceptions during teardown
                    if (_listener.IsListening)
                    {
                         Console.WriteLine($"Server error: {ex.Message}");
                    }
                }
            }
        });

        return $"localhost:{port}";
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}