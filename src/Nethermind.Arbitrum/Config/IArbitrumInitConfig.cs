using Nethermind.Config;

namespace Nethermind.Arbitrum.Config;

public interface IArbitrumInitConfig: IConfig
{
    bool Force { get; set; }
    string DownloadPath { get; set; }
    string Url { get; set; }
    bool ValidateChecksum { get; set; }
    TimeSpan DownloadPoll { get; set; }
    string Latest { get; set; }
    string LatestBase { get; set; }
    bool ImportWasm { get; set; }
    string ImportFile { get; set; }
    bool Empty { get; set; }
    bool DevInit { get; set; }
    ulong DevInitBlockNum { get; set; }
    string DevInitAddress { get; set; }
    ulong DevMaxCodeSize { get; set; }
    string GenesisJsonFile { get; set; }
    bool ThenQuit { get; set; }
    ulong RecreateMissingStateFrom { get; set; }
    string RebuildLocalWasm { get; set; }
    int AccountsPerSync { get; set; }
}