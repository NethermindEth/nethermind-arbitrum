using Nethermind.Core;
using Nethermind.Int256;
using System.Text.Json.Serialization;
using Nethermind.Core.Crypto;

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

    [property: JsonPropertyName("eip150Hash")]
    public string? Eip150Hash { get; set; }

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

    [JsonIgnore]
    public long? ArrowGlacierBlock { get; set; }

    [JsonIgnore]
    public long? GrayGlacierBlock { get; set; }

    [JsonIgnore]
    public long? MergeNetsplitBlock { get; set; }

    [JsonIgnore]
    public ulong? ShanghaiTime { get; set; }

    [JsonIgnore]
    public ulong? CancunTime { get; set; }

    [JsonIgnore]
    public ulong? PragueTime { get; set; }

    [JsonIgnore]
    public ulong? OsakaTime { get; set; }

    [JsonIgnore]
    public ulong? VerkleTime { get; set; }

    [JsonIgnore]
    public UInt256? TerminalTotalDifficulty { get; set; }

    [JsonIgnore]
    public bool TerminalTotalDifficultyPassed { get; set; }

    [property: JsonPropertyName("clique")]
    public CliqueConfigDTO? Clique { get; set; }


    [property: JsonPropertyName("arbitrum")]
    public required ArbitrumChainParams ArbitrumChainParams { get; set; }

    // CheckCompatible checks whether scheduled fork transitions have been imported
    // with a mismatching chain configuration.
    public void CheckCompatibilityWith(ChainConfig other, ulong headNumber, ulong headTimestamp)
    {
        ConfigIncompatibleException? lastError = null;
        // Iterate checkCompatible to find the lowest conflict
        while (true)
        {
            try
            {
                CheckInternalCompatibilityWith(other, headNumber, headTimestamp);
                break;
            }
            catch (ConfigIncompatibleException newError)
            {
                if (lastError is not null && newError.RewindToBlock == lastError.RewindToBlock && newError.RewindToTime == lastError.RewindToTime)
                {
                    break;
                }

                lastError = newError;

                if (newError.RewindToTime > 0)
                    headTimestamp = newError.RewindToTime;
                else
                    headNumber = newError.RewindToBlock;
            }
        }

        if (lastError is not null)
            throw lastError;
    }

    private void CheckInternalCompatibilityWith(ChainConfig other, ulong headNumber, ulong headTimestamp)
    {
        if (IsForkBlockIncompatible((ulong?)HomesteadBlock, (ulong?)other.HomesteadBlock, headNumber))
            throw ConfigIncompatibleException.CreateBlockCompatibleException("Homestead fork block", (ulong?)HomesteadBlock, (ulong?)other.HomesteadBlock);

        if (IsForkBlockIncompatible((ulong?)DaoForkBlock, (ulong?)other.DaoForkBlock, headNumber))
            throw ConfigIncompatibleException.CreateBlockCompatibleException("Dao fork block", (ulong?)DaoForkBlock, (ulong?)other.DaoForkBlock);

        // IsDaoFork
        if (IsBlockForked((ulong?)DaoForkBlock, headNumber) && DaoForkSupport != other.DaoForkSupport)
            throw ConfigIncompatibleException.CreateBlockCompatibleException("Dao fork support flag", (ulong?)DaoForkBlock, (ulong?)other.DaoForkBlock);

        if (IsForkBlockIncompatible((ulong?)Eip150Block, (ulong?)other.Eip150Block, headNumber))
            throw ConfigIncompatibleException.CreateBlockCompatibleException("Eip150 fork block", (ulong?)Eip150Block, (ulong?)other.Eip150Block);

        if (IsForkBlockIncompatible((ulong?)Eip155Block, (ulong?)other.Eip155Block, headNumber))
            throw ConfigIncompatibleException.CreateBlockCompatibleException("Eip155 fork block", (ulong?)Eip155Block, (ulong?)other.Eip155Block);

        if (IsForkBlockIncompatible((ulong?)Eip158Block, (ulong?)other.Eip158Block, headNumber))
            throw ConfigIncompatibleException.CreateBlockCompatibleException("Eip158 fork block", (ulong?)Eip158Block, (ulong?)other.Eip158Block);

        // IsEIP158
        if (IsBlockForked((ulong?)Eip158Block, headNumber) && !AreBlockEqual(ChainId, other.ChainId))
            throw ConfigIncompatibleException.CreateBlockCompatibleException("Eip158 chain id", (ulong?)Eip158Block, (ulong?)other.Eip158Block);

        CheckArbitrumCompatibility(other);

        if (IsForkBlockIncompatible((ulong?)ByzantiumBlock, (ulong?)other.ByzantiumBlock, headNumber))
            throw ConfigIncompatibleException.CreateBlockCompatibleException("Byzantium fork block", (ulong?)ByzantiumBlock, (ulong?)other.ByzantiumBlock);

        if (IsForkBlockIncompatible((ulong?)ConstantinopleBlock, (ulong?)other.ConstantinopleBlock, headNumber))
            throw ConfigIncompatibleException.CreateBlockCompatibleException("Constantinople fork block", (ulong?)ConstantinopleBlock, (ulong?)other.ConstantinopleBlock);

        if (IsForkBlockIncompatible((ulong?)PetersburgBlock, (ulong?)other.PetersburgBlock, headNumber))
        {
            // the only case where we allow Petersburg to be set in the past is if it is equal to Constantinople
            // mainly to satisfy fork ordering requirements which state that Petersburg fork be set if Constantinople fork is set
            if (IsForkBlockIncompatible((ulong?)ConstantinopleBlock, (ulong?)other.PetersburgBlock, headNumber))
                throw ConfigIncompatibleException.CreateBlockCompatibleException("Petersburg fork block", (ulong?)ConstantinopleBlock, (ulong?)other.PetersburgBlock);
        }

        if (IsForkBlockIncompatible((ulong?)IstanbulBlock, (ulong?)other.IstanbulBlock, headNumber))
            throw ConfigIncompatibleException.CreateBlockCompatibleException("Istanbul fork block", (ulong?)IstanbulBlock, (ulong?)other.IstanbulBlock);

        if (IsForkBlockIncompatible((ulong?)MuirGlacierBlock, (ulong?)other.MuirGlacierBlock, headNumber))
            throw ConfigIncompatibleException.CreateBlockCompatibleException("MuirGlacier fork block", (ulong?)MuirGlacierBlock, (ulong?)other.MuirGlacierBlock);

        if (IsForkBlockIncompatible((ulong?)BerlinBlock, (ulong?)other.BerlinBlock, headNumber))
            throw ConfigIncompatibleException.CreateBlockCompatibleException("Berlin fork block", (ulong?)BerlinBlock, (ulong?)other.BerlinBlock);

        if (IsForkBlockIncompatible((ulong?)LondonBlock, (ulong?)other.LondonBlock, headNumber))
            throw ConfigIncompatibleException.CreateBlockCompatibleException("London fork block", (ulong?)LondonBlock, (ulong?)other.LondonBlock);

        if (IsForkBlockIncompatible((ulong?)ArrowGlacierBlock, (ulong?)other.ArrowGlacierBlock, headNumber))
            throw ConfigIncompatibleException.CreateBlockCompatibleException("ArrowGlacier fork block", (ulong?)ArrowGlacierBlock, (ulong?)other.ArrowGlacierBlock);

        if (IsForkBlockIncompatible((ulong?)GrayGlacierBlock, (ulong?)other.GrayGlacierBlock, headNumber))
            throw ConfigIncompatibleException.CreateBlockCompatibleException("GrayGlacier fork block", (ulong?)GrayGlacierBlock, (ulong?)other.GrayGlacierBlock);

        if (IsForkBlockIncompatible((ulong?)MergeNetsplitBlock, (ulong?)other.MergeNetsplitBlock, headNumber))
            throw ConfigIncompatibleException.CreateBlockCompatibleException("MergeNetsplit fork block", (ulong?)MergeNetsplitBlock, (ulong?)other.MergeNetsplitBlock);

        // Timestamp compatibility from now

        if (IsForkBlockIncompatible(ShanghaiTime, other.ShanghaiTime, headTimestamp))
            throw ConfigIncompatibleException.CreateTimestampCompatibleException("ShanghaiTime fork block", ShanghaiTime, other.ShanghaiTime);

        if (IsForkBlockIncompatible(CancunTime, other.CancunTime, headTimestamp))
            throw ConfigIncompatibleException.CreateTimestampCompatibleException("CancunTime fork block", CancunTime, other.CancunTime);

        if (IsForkBlockIncompatible(PragueTime, other.PragueTime, headTimestamp))
            throw ConfigIncompatibleException.CreateTimestampCompatibleException("PragueTime fork block", PragueTime, other.PragueTime);

        if (IsForkBlockIncompatible(OsakaTime, other.OsakaTime, headTimestamp))
            throw ConfigIncompatibleException.CreateTimestampCompatibleException("OsakaTime fork block", OsakaTime, other.OsakaTime);

        if (IsForkBlockIncompatible(VerkleTime, other.VerkleTime, headTimestamp))
            throw ConfigIncompatibleException.CreateTimestampCompatibleException("VerkleTime fork block", VerkleTime, other.VerkleTime);
    }

    // isForkBlockIncompatible returns true if a fork scheduled at block s1 cannot be
    // rescheduled to block s2 because head is already past the fork.
    private static bool IsForkBlockIncompatible(ulong? blockNumberA, ulong? blockNumberB, ulong headNumber)
        => (IsBlockForked(blockNumberA, headNumber) || IsBlockForked(blockNumberB, headNumber)) &&
            !AreBlockEqual(blockNumberA, blockNumberB);

    // isBlockForked returns whether a fork scheduled at block s is active at the
    // given head block. Whilst this method is the same as isTimestampForked, they
    // are explicitly separate for clearer reading.
    private static bool IsBlockForked(ulong? blockNumber, ulong headNumber)
        => blockNumber is not null && blockNumber <= headNumber;

    private static bool AreBlockEqual(ulong? blockNumberA, ulong? blockNumberB)
        => blockNumberA is null && blockNumberB is null ||
            blockNumberA is not null && blockNumberB is not null && blockNumberA == blockNumberB;

    private void CheckArbitrumCompatibility(ChainConfig other)
    {
        if (ArbitrumChainParams.EnableArbOS != other.ArbitrumChainParams.EnableArbOS)
            // This difference applies to the entire chain, so report that the genesis block is where the difference appears.
            throw ConfigIncompatibleException.CreateBlockCompatibleException("isArbitrum", 0, 0);

        if (!ArbitrumChainParams.EnableArbOS)
            return;

        if (ArbitrumChainParams.GenesisBlockNum != other.ArbitrumChainParams.GenesisBlockNum)
            throw ConfigIncompatibleException.CreateBlockCompatibleException(
                "genesisBlockNum",
                ArbitrumChainParams.GenesisBlockNum,
                other.ArbitrumChainParams.GenesisBlockNum
            );
    }

    public class ConfigIncompatibleException : Exception
    {
        public string? What { get; private init; }

        // block numbers of the stored and new configurations if block based forking
        public ulong? StoredBlockNumber { get; private init; }
        public ulong? NewBlockNumber { get; private init; }
        // timestamps of the stored and new configurations if time based forking
        public ulong RewindToBlock { get; private init; }

        // timestamps of the stored and new configurations if time based forking
        public ulong? StoredTime { get; private init; }
        public ulong? NewTime { get; private init; }
        // the timestamp to which the local chain must be rewound to correct the error
        public ulong RewindToTime { get; private init; }

        public static ConfigIncompatibleException CreateBlockCompatibleException(string what, ulong? storedBlockNumber, ulong? newBlockNumber)
        {
            ulong? rew = newBlockNumber;
            if (newBlockNumber is null || (storedBlockNumber is not null && storedBlockNumber < newBlockNumber))
                rew = storedBlockNumber;

            ConfigIncompatibleException exception = new()
            {
                What = what,
                StoredBlockNumber = storedBlockNumber,
                NewBlockNumber = newBlockNumber,
                RewindToBlock = rew is not null && rew > 0 ? (ulong)rew - 1 : 0
            };

            return exception;
        }

        public static ConfigIncompatibleException CreateTimestampCompatibleException(string what, ulong? storedTime, ulong? newTime)
        {
            ulong? rew = newTime;
            if (newTime is null || (storedTime is not null && storedTime < newTime))
                rew = storedTime;

            ConfigIncompatibleException exception = new()
            {
                What = what,
                StoredTime = storedTime,
                NewTime = newTime,
                RewindToTime = rew is not null && rew != 0 ? (ulong)rew - 1 : 0
            };

            return exception;
        }
    }
}

public class ArbitrumChainParams
{
    [property: JsonPropertyName("EnableArbOS")]
    public bool EnableArbOS { get; set; } = true;

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

    // Maximum bytecode to permit for a contract.
    // 0 value implies DefaultMaxCodeSize
    [JsonIgnore]
    public ulong? MaxCodeSize { get; set; } = 0;

    // Maximum initcode to permit in a creation transaction and create instructions.
    // 0 value implies DefaultMaxInitCodeSize
    [JsonIgnore]
    public ulong? MaxInitCodeSize { get; set; } = 0;
}

public class CliqueConfigDTO
{
    [property: JsonPropertyName("period")]
    public ulong Period { get; set; }

    [property: JsonPropertyName("epoch")]
    public ulong Epoch { get; set; }
}
