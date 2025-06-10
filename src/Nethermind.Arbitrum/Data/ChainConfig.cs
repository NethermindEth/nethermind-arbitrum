using Nethermind.Core;
using Nethermind.Int256;
using System.Text.Json.Serialization;

namespace Nethermind.Arbitrum.Data;

public class ChainConfig
{
    [property: JsonPropertyName("chainId")]
    public ulong ChainId { get; set; }


    [property: JsonPropertyName("homesteadBlock")]
    public long? HomesteadBlock { get; set; }


    [property: JsonPropertyName("daoForkBlock")]
    public long? DaoForkBlock { get; set; }

    [property: JsonPropertyName("daoForkSupport")]
    public bool DaoForkSupport { get; set; }


    [property: JsonPropertyName("eip150Block")]
    public long? Eip150Block { get; set; }

    [property: JsonPropertyName("eip155Block")]
    public long? Eip155Block { get; set; }

    [property: JsonPropertyName("eip158Block")]
    public long? Eip158Block { get; set; }


    [property: JsonPropertyName("byzantiumBlock")]
    public long? ByzantiumBlock { get; set; }

    [property: JsonPropertyName("constantinopleBlock")]
    public long? ConstantinopleBlock { get; set; }

    [property: JsonPropertyName("petersburgBlock")]
    public long? PetersburgBlock { get; set; }

    [property: JsonPropertyName("istanbulBlock")]
    public long? IstanbulBlock { get; set; }

    [property: JsonPropertyName("muirGlacierBlock")]
    public long? MuirGlacierBlock { get; set; }

    [property: JsonPropertyName("berlinBlock")]
    public long? BerlinBlock { get; set; }

    [property: JsonPropertyName("londonBlock")]
    public long? LondonBlock { get; set; }

    [property: JsonPropertyName("arrowGlacierBlock")]
    public long? ArrowGlacierBlock { get; set; }

    [property: JsonPropertyName("grayGlacierBlock")]
    public long? GrayGlacierBlock { get; set; }

    [property: JsonPropertyName("mergeNetsplitBlock")]
    public long? MergeNetsplitBlock { get; set; }


    [property: JsonPropertyName("shanghaiTime")]
    public ulong? ShanghaiTime { get; set; }

    [property: JsonPropertyName("cancunTime")]
    public ulong? CancunTime { get; set; }

    [property: JsonPropertyName("pragueTime")]
    public ulong? PragueTime { get; set; }

    [property: JsonPropertyName("verkleTime")]
    public ulong? VerkleTime { get; set; }


    [property: JsonPropertyName("terminalTotalDifficulty")]
    public UInt256? TerminalTotalDifficulty { get; set; }

    [property: JsonPropertyName("terminalTotalDifficultyPassed")]
    public bool TerminalTotalDifficultyPassed { get; set; }

    [property: JsonPropertyName("clique")]
    public CliqueConfigDTO? Clique { get; set; }


    [property: JsonPropertyName("arbitrum")]
    public required ArbitrumChainParams ArbitrumChainParams { get; set; }
}

public class ArbitrumChainParams
{
    [property: JsonPropertyName("EnableArbOS")]
    public bool Enabled { get; set; } = true;

    [property: JsonPropertyName("AllowDebugPrecompiles")]
    public bool AllowDebugPrecompiles { get; set; } = false;

    [property: JsonPropertyName("DataAvailabilityCommittee")]
    public bool DataAvailabilityCommittee { get; set; } = false;

    [property: JsonPropertyName("InitialArbOSVersion")]
    public ulong InitialArbOSVersion { get; set; } = 0;

    [property: JsonPropertyName("InitialChainOwner")]
    public Address InitialChainOwner { get; set; } = Address.Zero;

    [property: JsonPropertyName("GenesisBlockNum")]
    public ulong GenesisBlockNum { get; set; } = 0;

    [property: JsonPropertyName("MaxCodeSize")]
    public ulong? MaxCodeSize { get; set; } = 0;

    [property: JsonPropertyName("MaxInitCodeSize")]
    public ulong? MaxInitCodeSize { get; set; } = 0;
}

public class CliqueConfigDTO
{
    [property: JsonPropertyName("period")]
    public ulong Period { get; set; }

    [property: JsonPropertyName("epoch")]
    public ulong Epoch { get; set; }
}
