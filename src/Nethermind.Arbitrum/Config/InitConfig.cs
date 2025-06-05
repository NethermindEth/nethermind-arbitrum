namespace Nethermind.Arbitrum.Config;

public class InitConfig
{
    public bool Force { get; set; }
    public string DownloadPath { get; set; }
    public string Url { get; set; }
    public bool ValidateChecksum { get; set; }
    public TimeSpan DownloadPoll { get; set; }
    public string Latest { get; set; }
    public string LatestBase { get; set; }
    public bool ImportWasm { get; set; }
    public string ImportFile { get; set; }
    public bool Empty { get; set; }
    public bool DevInit { get; set; }
    public ulong DevInitBlockNum { get; set; }
    public string DevInitAddress { get; set; }
    public ulong DevMaxCodeSize { get; set; }
    public string GenesisJsonFile { get; set; }
    public bool ThenQuit { get; set; }
    public ulong RecreateMissingStateFrom { get; set; }
    public string RebuildLocalWasm { get; set; } = "false";
    public int AccountsPerSync { get; set; }
}