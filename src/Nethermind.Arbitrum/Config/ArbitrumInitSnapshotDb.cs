using System.IO.Compression;
using System.Net;
using Nethermind.Api;
using Nethermind.Arbitrum.Snapshots;
using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Init;
using Nethermind.Init.Snapshot;
using Nethermind.Init.Steps;
using Nethermind.Logging;
using Nethermind.State;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumInitSnapshotDb: InitDatabaseSnapshot
{
    private const int BufferSize = 8192;

    private SnapshotDownloader _downloader;
    
    private readonly INethermindApi _api;
    private readonly ILogger _logger;
    
    public ArbitrumInitSnapshotDb(INethermindApi api) : base(api)
    {
        _api = api;
        _logger = _api.LogManager.GetClassLogger();
        _downloader = new SnapshotDownloader(_logger, new HttpClient());
    }
    
    
    public override async Task Execute(CancellationToken cancellationToken)
    {
        switch (_api.Config<IInitConfig>().DiagnosticMode)
        {
            case DiagnosticMode.RpcDb:
            case DiagnosticMode.ReadOnlyDb:
            case DiagnosticMode.MemDb:
                break;
            default:
                await InitDbFromSnapshot(cancellationToken);
                break;
        }

        await base.Execute(cancellationToken);
    }
    
    private async Task InitDbFromSnapshot(CancellationToken cancellationToken)
    {

        IArbitrumInitConfig arbitrumInitConfig = _api.Config<IArbitrumInitConfig>();
        string dbPath = _api.Config<IInitConfig>().BaseDbPath;
        string snapshotUrl = arbitrumInitConfig.Url ??
                             throw new InvalidOperationException("Snapshot download URL is not configured");
        string snapshotFileName = arbitrumInitConfig.DownloadPath;

        if (Path.Exists(dbPath))
        {
            if (GetCheckpoint(arbitrumInitConfig) < Stage.Extracted)
            {
                if (_logger.IsInfo)
                    _logger.Info($"Extracting wasn't finished last time, restarting it. To interrupt press Ctrl^C");
                // Wait few seconds if user wants to stop reinitialization
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                Directory.Delete(dbPath, true);
            }
            else
            {
                if (_logger.IsInfo)
                    _logger.Info($"Database already exists at {dbPath}. Interrupting");

                return;
            }
        }
        
        if (GetCheckpoint(arbitrumInitConfig) < Stage.Downloaded)
        {
            while (true)
            {
                try
                {
                    await _downloader.DownloadInitAsync(arbitrumInitConfig, snapshotFileName, cancellationToken);
                    break;
                }
                catch (IOException e)
                {
                    if (_logger.IsError)
                        _logger.Error($"Snapshot download failed. Retrying in 5 seconds. Error: {e}");
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
            SetCheckpoint(arbitrumInitConfig, Stage.Downloaded);
        }

        await ExtractSnapshotTo(snapshotFileName, dbPath, cancellationToken);
        SetCheckpoint(arbitrumInitConfig, Stage.Extracted);

        if (_logger.IsInfo)
        {
            _logger.Info("Database successfully initialized from snapshot.");
            _logger.Info($"Deleting snapshot file {snapshotFileName}.");
        }

        File.Delete(snapshotFileName);

        SetCheckpoint(arbitrumInitConfig, Stage.End);
    }

    private Task ExtractSnapshotTo(string snapshotPath, string dbPath, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            if (_logger.IsInfo)
                _logger.Info($"Extracting snapshot to {dbPath}. Do not interrupt!");

            ZipFile.ExtractToDirectory(snapshotPath, dbPath);
        }, cancellationToken);

    private enum Stage
    {
        Start,
        Downloaded,
        Verified,
        Extracted,
        End,
    }

    private static void SetCheckpoint(IArbitrumInitConfig snapshotConfig, Stage stage)
    {
        string checkpointPath = Path.Combine(snapshotConfig.DownloadPath, "_checkpoint");
        File.WriteAllText(checkpointPath, stage.ToString());
    }

    private static Stage GetCheckpoint(IArbitrumInitConfig snapshotConfig)
    {
        string checkpointPath = Path.Combine(snapshotConfig.DownloadPath, "_checkpoint");
        if (File.Exists(checkpointPath))
        {
            string stringStage = File.ReadAllText(checkpointPath);
            return Enum.Parse<Stage>(stringStage);
        }
        else
        {
            return Stage.Start;
        }
    }
}