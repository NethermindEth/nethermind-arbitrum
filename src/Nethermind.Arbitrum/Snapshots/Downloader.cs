using System.Net;
using System.Security.Cryptography;
using Nethermind.Arbitrum.Config;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Snapshots;

public class SnapshotDownloader
{
    private static readonly ILogger Logger;
    private static readonly HttpClient HttpClient = new();
    private static readonly Exception NotFoundErrorGlobal = new FileNotFoundException("File not found (global sentinel)");

    public static async Task<(string FilePath, Action CleanupAction)> InitializeAndDownloadInitAsync(
        CancellationToken cancellationToken,
        InitConfig config)
    {
        Action cleanup = () => { };

        if (string.IsNullOrWhiteSpace(config.DownloadPath))
        {
            var tmpPath = Path.Combine(Directory.GetCurrentDirectory(), "tmp");

            if (Directory.Exists(tmpPath))
                throw new IOException("Temporary directory for downloading init file already exists.");

            try
            {
                Directory.CreateDirectory(tmpPath);
                config.DownloadPath = tmpPath;

                cleanup = () =>
                {
                    try
                    {
                        if (Directory.Exists(tmpPath))
                            Directory.Delete(tmpPath, recursive: true);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Failed to clean up temporary directory.", ex);
                    }
                };
            }
            catch (Exception ex)
            {
                throw new IOException("Failed to create temporary download directory.", ex);
            }
        }

        string initFile = await DownloadInitAsync(cancellationToken, config);
        return (initFile, cleanup);
    }

    public static async Task<string> DownloadInitAsync(CancellationToken cancellationToken, InitConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Url))
            return string.Empty;

        if (config.Url.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            return config.Url.Substring(5);

        Logger.Info($"Downloading initial database: {config.Url}");

        if (!config.ValidateChecksum)
        {
            try
            {
                return await DownloadFileAsync(cancellationToken, config, config.Url, null);
            }
            catch (FileNotFoundException)
            {
                return await DownloadInitInPartsAsync(config, cancellationToken);
            }
        }

        byte[]? checksum;
        try
        {
            checksum = await FetchChecksumAsync(cancellationToken, new Uri(config.Url + ".sha256"));
        }
        catch (FileNotFoundException)
        {
            return await DownloadInitInPartsAsync(config, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new IOException("Error fetching checksum.", ex);
        }

        return await DownloadFileAsync(cancellationToken, config, config.Url, checksum);
    }

    public static async Task<string> DownloadInitInPartsAsync(InitConfig config, CancellationToken cancellationToken)
    {
        Logger.Info("File not found; attempting to download database in parts.");

        if (!Directory.Exists(config.DownloadPath))
            throw new IOException($"Download path must be a directory: {config.DownloadPath}");

        var baseUri = new Uri(config.Url);
        var manifestUri = new Uri(baseUri + ".manifest.txt");

        string manifestContent = await HttpGetAsync(cancellationToken, manifestUri);
        var lines = manifestContent.Trim().Split('\n');

        var partNames = new List<string>();
        var checksums = new List<byte[]>();

        foreach (var line in lines)
        {
            var fields = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 2) throw new FormatException("Manifest file format is incorrect.");

            checksums.Add(Convert.FromHexString(fields[0]));
            partNames.Add(fields[1]);
        }

        var partFiles = new List<string>();

        try
        {
            for (var i = 0; i < partNames.Count; i++)
            {
                var partName = partNames[i];
                Logger.Info($"Downloading database part: {partName}");

                var partUri = new Uri(baseUri, $"../{partName}");
                var checksum = config.ValidateChecksum ? checksums[i] : null;

                var filePath = await DownloadFileAsync(cancellationToken, config, partUri.ToString(), checksum);
                partFiles.Add(filePath);
            }

            string archivePath = Path.Combine(config.DownloadPath, Path.GetFileName(baseUri.LocalPath));
            return await JoinArchiveAsync(partFiles, archivePath);
        }
        finally
        {
            foreach (var file in partFiles)
            {
                try { File.Delete(file); }
                catch { Logger.Warn($"Failed to remove temporary file: {file}"); }
            }
        }
    }

    private static async Task<string> DownloadFileAsync(CancellationToken cancellationToken, InitConfig config, string url, byte[]? checksum)
    {
        Logger.Info($"Downloading file from {url} to {config.DownloadPath}");

        string fileName = Path.GetFileName(new Uri(url).LocalPath);
        string destinationPath = Path.Combine(config.DownloadPath, fileName);

        using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) throw NotFoundErrorGlobal;

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

        if (checksum != null)
        {
            using var hasher = SHA256.Create();
            var buffer = new byte[8192];
            int read;

            while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                hasher.TransformBlock(buffer, 0, read, null, 0);
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            hasher.TransformFinalBlock([], 0, 0);

            if (!hasher.Hash!.SequenceEqual(checksum))
            {
                fileStream.Close();
                File.Delete(destinationPath);
                throw new InvalidDataException($"Checksum mismatch for file: {fileName}");
            }
        }
        else
        {
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        Logger.Info($"Download complete: {destinationPath}");
        return destinationPath;
    }

    private static async Task<string> JoinArchiveAsync(List<string> parts, string outputPath)
    {
        if (parts.Count == 0)
            throw new InvalidOperationException("No database parts to join.");

        await using var output = File.Create(outputPath);

        foreach (string part in parts)
        {
            await using var partStream = File.OpenRead(part);
            await partStream.CopyToAsync(output);
            Logger.Info($"Appended part to archive: {part}");
        }

        Logger.Info($"Successfully created archive at: {outputPath}");
        return outputPath;
    }

    private static async Task<byte[]?> FetchChecksumAsync(CancellationToken cancellationToken, Uri url)
    {
        string body = await HttpGetAsync(cancellationToken, url);
        var checksumStr = body.Trim();
        var checksum = Convert.FromHexString(checksumStr);

        if (checksum.Length != SHA256.Create().HashSize / 8)
            throw new InvalidDataException("Checksum length is invalid.");

        return checksum;
    }

    private static async Task<string> HttpGetAsync(CancellationToken cancellationToken, Uri url)
    {
        try
        {
            var response = await HttpClient.GetAsync(url, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw NotFoundErrorGlobal;

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw NotFoundErrorGlobal;
        }
        catch (Exception ex)
        {
            throw new HttpRequestException($"HTTP GET failed for {url}: {ex.Message}", ex);
        }
    }
}
