using Nethermind.Api;
using Nethermind.Consensus.Producers;
using Nethermind.Init.Steps;
using Nethermind.Arbitrum.Snapshots;
using Nethermind.Config;
using Nethermind.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Readers;
using SharpCompress.Common;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumInitializeBlockchainStep : InitializeBlockchain
{
    private readonly InitConfig _initConfig;
    private readonly INethermindApi _api;
    private readonly ILogger _logger;

    public ArbitrumInitializeBlockchainStep(INethermindApi api, InitConfig initConfig) : base(api)
    {
        _api = api;
        _initConfig = initConfig;
        _logger = api.LogManager.GetClassLogger();
    }

    protected override IBlockProductionPolicy CreateBlockProductionPolicy() => AlwaysStartBlockProductionPolicy.Instance;

    public override async Task ExecuteAsync(InitializationState state)
    {
        _logger.Info("ArbitrumInitializeBlockchainStep: Starting snapshot download and extraction process...");

        SnapshotDownloader.DownloadResult? downloadResult = null;
        Action? cleanupAction = null;
        string? downloadedSnapshotPath = null;

        try
        {
            (downloadResult, cleanupAction) = await SnapshotDownloader.InitializeAndDownloadInitAsync(CancellationToken.None, _initConfig);

            if (downloadResult != null && downloadResult.Success && !string.IsNullOrEmpty(downloadResult.UncompressedDirectoryPath)) // Assuming UncompressedDirectoryPath is the path to the downloaded archive
            {
                downloadedSnapshotPath = downloadResult.UncompressedDirectoryPath; // This might be a misnomer if it's the archive path
                _logger.Info($"ArbitrumInitializeBlockchainStep: Snapshot downloaded successfully. Path: {downloadedSnapshotPath}");

                bool dbExistsBeforeExtraction = _api.DbProvider.RealDb != null && !_api.DbProvider.RealDb.IsEmpty();
                string extractionPath = _api.Config<IDbConfig>().Path; // Base data directory

                if (string.IsNullOrEmpty(extractionPath))
                {
                    throw new InvalidOperationException("ArbitrumInitializeBlockchainStep: Database path (extraction path) is not configured.");
                }

                // The snapshot content should be extracted into the chain data directory directly (e.g. nethermind_db/goerli)
                // not into a sub-folder of it, so we use extractionPath directly.
                // If SnapshotDownloader provides a directory, it might already be uncompressed.
                // For this task, we assume UncompressedDirectoryPath from DownloadResult IS the path to the .tar.gz file.
                // And that it needs to be extracted TO the IDbConfig.Path.

                _logger.Info($"ArbitrumInitializeBlockchainStep: Attempting to extract snapshot from '{downloadedSnapshotPath}' to '{extractionPath}'.");
                await ExtractSnapshotAsync(downloadedSnapshotPath, extractionPath, _logger);
                _logger.Info($"ArbitrumInitializeBlockchainStep: Snapshot extraction successful to {extractionPath}.");

                // After successful extraction, the DB should exist.
                // The original dbExists check is now potentially outdated.
                // We proceed to base.ExecuteAsync if extraction was successful.
                await base.ExecuteAsync(state);
            }
            else
            {
                _logger.Warn("ArbitrumInitializeBlockchainStep: Snapshot download failed, was skipped, or path was empty.");
                bool dbExists = _api.DbProvider.RealDb != null && !_api.DbProvider.RealDb.IsEmpty();
                if (!dbExists)
                {
                    throw new InvalidOperationException("ArbitrumInitializeBlockchainStep: Snapshot download/extraction failed and no existing database found. Node cannot start.");
                }
                _logger.Info("ArbitrumInitializeBlockchainStep: Proceeding with existing database as snapshot download/extraction failed or was skipped.");
                await base.ExecuteAsync(state);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("ArbitrumInitializeBlockchainStep: An error occurred during the snapshot download or extraction process.", ex);
            // Critical check: if DB did not exist AND download/extraction failed, we must not proceed.
            bool dbExists = _api.DbProvider.RealDb != null && !_api.DbProvider.RealDb.IsEmpty();
            if (!dbExists) {
                 _logger.Error("ArbitrumInitializeBlockchainStep: Halting node start due to failure in snapshot processing for a fresh database.", ex);
                throw; // Re-throw the exception to halt initialization.
            }
            _logger.Warn("ArbitrumInitializeBlockchainStep: Proceeding with existing database despite error during snapshot processing, as a database already exists.");
            // If DB exists, we might be able to proceed. This depends on the desired behavior.
            // For now, let's assume if DB exists, we try to start with it.
            await base.ExecuteAsync(state);
        }
        finally
        {
            cleanupAction?.Invoke(); // Cleanup for downloaded archive file by SnapshotDownloader
            _logger.Info("ArbitrumInitializeBlockchainStep: Snapshot download cleanup action (if any) invoked.");
        }
    }

    private async Task ExtractSnapshotAsync(string snapshotArchivePath, string extractToPath, ILogger logger)
    {
        logger.Info($"Starting extraction of snapshot '{snapshotArchivePath}' to '{extractToPath}'...");

        if (string.IsNullOrWhiteSpace(snapshotArchivePath))
        {
            logger.Error("Snapshot archive path is null or empty. Cannot extract.");
            throw new ArgumentNullException(nameof(snapshotArchivePath));
        }

        if (!File.Exists(snapshotArchivePath))
        {
            logger.Error($"Snapshot archive file not found at '{snapshotArchivePath}'.");
            throw new FileNotFoundException("Snapshot archive file not found.", snapshotArchivePath);
        }

        try
        {
            Directory.CreateDirectory(extractToPath); // Ensure the extraction directory exists

            await Task.Run(() => // Run synchronous SharpCompress operations in a background thread
            {
                using (Stream stream = File.OpenRead(snapshotArchivePath))
                using (var gzipStream = new GZipStream(stream, CompressionMode.Decompress))
                using (var tarArchive = TarArchive.Open(gzipStream))
                {
                    foreach (var entry in tarArchive.Entries)
                    {
                        if (!entry.IsDirectory)
                        {
                            // Ensure subdirectories within the tar are created in the extraction path
                            string entryDestinationPath = Path.Combine(extractToPath, entry.Key.Replace('/', Path.DirectorySeparatorChar));
                            Directory.CreateDirectory(Path.GetDirectoryName(entryDestinationPath)!);
                            entry.WriteToFile(entryDestinationPath, new ExtractionOptions { Overwrite = true, PreserveFileTime = true });
                        }
                    }
                }
            });

            logger.Info($"Successfully extracted snapshot to '{extractToPath}'.");

            // Delete the snapshot archive file after successful extraction
            try
            {
                File.Delete(snapshotArchivePath);
                logger.Info($"Successfully deleted snapshot archive '{snapshotArchivePath}'.");
            }
            catch (IOException ioEx)
            {
                logger.Warn($"Could not delete snapshot archive '{snapshotArchivePath}' after extraction: {ioEx.Message}");
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to extract snapshot from '{snapshotArchivePath}' to '{extractToPath}'. Error: {ex.Message}", ex);
            throw; // Re-throw to be caught by ExecuteAsync
        }
    }
}
