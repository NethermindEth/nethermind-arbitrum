// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Net;
using System.Security.Cryptography;
using Nethermind.Arbitrum.Config;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Snapshots;

/// <summary>
/// Handles downloading snapshot files, either as a single archive or in multiple parts.
/// </summary>
public class SnapshotDownloader(ILogger logger, HttpClient? httpClient = null) : IDisposable
{
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient();
    private bool _disposed;

    /// <summary>
    /// Ensures a download path exists and downloads the init file. Creates and cleans up a temporary directory if no path is provided.
    /// </summary>
    /// <returns>A tuple containing the final file path and a cleanup action to be executed by the caller.</returns>
    public async Task<(string FilePath, Action CleanupAction)> InitializeAndDownloadInitAsync(
        InitConfig config,
        CancellationToken cancellationToken)
    {
        Action cleanupAction = () => { };
        string downloadPath = config.DownloadPath;
        bool isTempPath = false;

        if (string.IsNullOrWhiteSpace(downloadPath))
        {
            isTempPath = true;
            downloadPath = Path.Combine(Path.GetTempPath(), $"snapshot-download-{Path.GetRandomFileName()}");
            Directory.CreateDirectory(downloadPath);

            cleanupAction = () =>
            {
                try
                {
                    if (Directory.Exists(downloadPath))
                    {
                        Directory.Delete(downloadPath, recursive: true);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error("Failed to clean up temporary download directory.", ex);
                }
            };
        }

        try
        {
            string filePath = await DownloadInitAsync(config, downloadPath, cancellationToken).ConfigureAwait(false);
            // If the path was temporary, we want the caller to own the cleanup, so we don't clean it up here.
            return (filePath, cleanupAction);
        }
        catch (Exception)
        {
            // If an error occurs and we created a temporary path, clean it up immediately.
            if (isTempPath) cleanupAction();
            throw;
        }
    }

    /// <summary>
    /// Downloads the initialization database, attempting a direct download first and falling back to a multi-part download if necessary.
    /// </summary>
    /// <param name="config">The initialization configuration.</param>
    /// <param name="downloadPath">The directory to download files to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The path to the downloaded and verified file.</returns>
    public async Task<string> DownloadInitAsync(InitConfig config, string downloadPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.Url))
        {
            logger.Warn("Download URL is not configured. Skipping download.");
            return string.Empty;
        }

        if (config.Url.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return config.Url.Substring(5);
        }

        logger.Info($"Starting database download from: {config.Url}");

        // Try to download the file directly with its checksum first.
        try
        {
            byte[]? checksum = null;
            if (config.ValidateChecksum)
            {
                try
                {
                    var checksumUrl = new Uri(config.Url + ".sha256");
                    checksum = await FetchChecksumAsync(checksumUrl, cancellationToken).ConfigureAwait(false);
                }
                catch (FileNotFoundException)
                {
                    // Checksum file isn't found, we'll proceed to multipart download.
                    logger.Info("Checksum file not found. Attempting multi-part download.");
                    return await DownloadInitInPartsAsync(config, downloadPath, cancellationToken).ConfigureAwait(false);
                }
            }
            return await DownloadFileAsync(config.Url, downloadPath, checksum, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            // The single file was not found, so attempt to download it in parts.
            return await DownloadInitInPartsAsync(config, downloadPath, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Downloads a database by fetching a manifest file and then downloading and joining all the listed parts.
    /// </summary>
    public async Task<string> DownloadInitInPartsAsync(InitConfig config, string downloadPath, CancellationToken cancellationToken)
    {
        logger.Info("Attempting to download database in parts using manifest.");
        var baseUri = new Uri(config.Url);
        var manifestUri = new Uri(baseUri + ".manifest.txt");

        string manifestContent = await HttpGetAsync(manifestUri, cancellationToken).ConfigureAwait(false);
        var (partNames, checksums) = ParseManifest(manifestContent);

        var partFiles = new List<string>();
        try
        {
            for (var i = 0; i < partNames.Count; i++)
            {
                var partName = partNames[i];
                logger.Info($"Downloading database part {i + 1}/{partNames.Count}: {partName}");

                var partUri = new Uri(baseUri, $"../{partName}");
                var expectedChecksum = config.ValidateChecksum ? checksums[i] : null;

                var filePath = await DownloadFileAsync(partUri.ToString(), downloadPath, expectedChecksum, cancellationToken).ConfigureAwait(false);
                partFiles.Add(filePath);
            }

            string archivePath = Path.Combine(downloadPath, Path.GetFileName(baseUri.LocalPath));
            return await JoinArchiveAsync(partFiles, archivePath, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Clean up temporary part files after joining them.
            foreach (var file in partFiles)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    logger.Error($"Failed to remove temporary part file: {file}", ex);
                }
            }
        }
    }

    private (List<string> PartNames, List<byte[]> Checksums) ParseManifest(string manifestContent)
    {
        var partNames = new List<string>();
        var checksums = new List<byte[]>();
        var lines = manifestContent.Trim().Split('\n');

        foreach (var line in lines)
        {
            var fields = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 2)
            {
                throw new FormatException("Manifest file has an incorrect format. Expected: <checksum> <filename>");
            }
            checksums.Add(Convert.FromHexString(fields[0]));
            partNames.Add(fields[1]);
        }
        return (partNames, checksums);
    }

    private async Task<string> DownloadFileAsync(string url, string downloadDirectory, byte[]? expectedChecksum, CancellationToken cancellationToken)
    {
        string fileName = Path.GetFileName(new Uri(url).LocalPath);
        string destinationPath = Path.Combine(downloadDirectory, fileName);

        logger.Info($"Downloading file from {url} to {destinationPath}");

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException("File not found at the specified URL.", url);
        }
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

        if (expectedChecksum != null)
        {
            using var sha256 = SHA256.Create();
            var buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
            }
            sha256.TransformFinalBlock([], 0, 0);

            if (!sha256.Hash!.SequenceEqual(expectedChecksum))
            {
                await fileStream.DisposeAsync().ConfigureAwait(false);
                File.Delete(destinationPath);
                throw new InvalidDataException($"Checksum mismatch for downloaded file: {fileName}");
            }
        }
        else
        {
            await contentStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
        }

        logger.Info($"Download complete: {destinationPath}");
        return destinationPath;
    }

    private async Task<string> JoinArchiveAsync(IReadOnlyList<string> partPaths, string outputPath, CancellationToken cancellationToken)
    {
        if (partPaths.Count == 0)
        {
            throw new InvalidOperationException("No database parts were provided to join.");
        }

        logger.Info($"Joining {partPaths.Count} parts into archive: {outputPath}");
        await using var outputStream = File.Create(outputPath);

        foreach (string partPath in partPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var partStream = File.OpenRead(partPath);
            await partStream.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
        }

        logger.Info($"Successfully created archive at: {outputPath}");
        return outputPath;
    }

    private async Task<byte[]> FetchChecksumAsync(Uri url, CancellationToken cancellationToken)
    {
        string content = await HttpGetAsync(url, cancellationToken).ConfigureAwait(false);
        string checksumStr = content.Trim().Split(' ')[0]; // Handle cases where filename is also on the line
        var checksum = Convert.FromHexString(checksumStr);

        if (checksum.Length != SHA256.Create().HashSize / 8)
        {
            throw new InvalidDataException($"Invalid SHA256 checksum length from URL: {url}");
        }
        return checksum;
    }

    private async Task<string> HttpGetAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new FileNotFoundException("File not found at the specified URL.", url.ToString());
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"HTTP GET failed for {url}: {ex.Message}", ex, ex.StatusCode);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _httpClient.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}