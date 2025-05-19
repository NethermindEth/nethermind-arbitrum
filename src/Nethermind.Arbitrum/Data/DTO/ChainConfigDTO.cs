
using Nethermind.Int256;
using Newtonsoft.Json;

namespace Nethermind.Arbitrum.Data.DTO;

public class ChainConfigDTO
{
    [JsonProperty("chainId")]
    public ulong ChainId { get; set; }


    [JsonProperty("homesteadBlock")]
    public long? HomesteadBlock { get; set; }


    [JsonProperty("daoForkBlock")]
    public long? DaoForkBlock { get; set; }

    [JsonProperty("daoForkSupport")]
    public bool DaoForkSupport { get; set; }


    [JsonProperty("eip150Block")]
    public long? Eip150Block { get; set; }

    [JsonProperty("eip155Block")]
    public long? Eip155Block { get; set; }

    [JsonProperty("eip158Block")]
    public long? Eip158Block { get; set; }


    [JsonProperty("byzantiumBlock")]
    public long? ByzantiumBlock { get; set; }

    [JsonProperty("constantinopleBlock")]
    public long? ConstantinopleBlock { get; set; }

    [JsonProperty("petersburgBlock")]
    public long? PetersburgBlock { get; set; }

    [JsonProperty("istanbulBlock")]
    public long? IstanbulBlock { get; set; }

    [JsonProperty("muirGlacierBlock")]
    public long? MuirGlacierBlock { get; set; }

    [JsonProperty("berlinBlock")]
    public long? BerlinBlock { get; set; }

    [JsonProperty("londonBlock")]
    public long? LondonBlock { get; set; }

    [JsonProperty("arrowGlacierBlock")]
    public long? ArrowGlacierBlock { get; set; }

    [JsonProperty("grayGlacierBlock")]
    public long? GrayGlacierBlock { get; set; }

    [JsonProperty("mergeNetsplitBlock")]
    public long? MergeNetsplitBlock { get; set; }


    [JsonProperty("shanghaiTime")]
    public ulong? ShanghaiTime { get; set; }

    [JsonProperty("cancunTime")]
    public ulong? CancunTime { get; set; }

    [JsonProperty("pragueTime")]
    public ulong? PragueTime { get; set; }

    [JsonProperty("verkleTime")]
    public ulong? VerkleTime { get; set; }


    [JsonProperty("terminalTotalDifficulty")]
    public UInt256? TerminalTotalDifficulty { get; set; }

    [JsonProperty("terminalTotalDifficultyPassed")]
    public bool TerminalTotalDifficultyPassed { get; set; }

    [JsonProperty("clique")]
    public CliqueConfigDTO? Clique { get; set; }


    [JsonProperty("arbitrum")]
    public required ArbitrumConfig ArbitrumChainParams { get; set; }
}

public class CliqueConfigDTO
{
    [JsonProperty("period")]
    public ulong Period { get; set; }

    [JsonProperty("epoch")]
    public ulong Epoch { get; set; }
}
