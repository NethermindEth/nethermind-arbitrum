using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.Specs.Test.ChainSpecStyle;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public static class FullChainSimulationChainSpecProvider
{
    public static ChainSpec Create(ulong initialArbOsVersion = 32)
    {
        ChainSpec chainSpec = new()
        {
            Name = "Arbitrum Full Chain Simulation",
            DataDir = "arbitrum-local",
            NetworkId = 0x64aba,
            ChainId = 0x64aba,
            Bootnodes = [],
            GenesisStateUnavailable = false,
            SealEngineType = "Arbitrum",

            // Genesis block
            Genesis = new Block(new BlockHeader(
                parentHash: new Hash256("0x0000000000000000000000000000000000000000000000000000000000000000"),
                unclesHash: new Hash256("0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347"),
                beneficiary: Address.Zero,
                difficulty: 1,
                number: 0,
                gasLimit: 0x4000000000000,
                timestamp: 0,
                extraData: new byte[32],
                blobGasUsed: null,
                excessBlobGas: null,
                parentBeaconBlockRoot: null,
                requestsHash: null)
            {
                Nonce = 1,
                Author = Address.Zero,
                BaseFeePerGas = 0x5f5e100,
                StateRoot = Keccak.EmptyTreeHash,
                TxRoot = Keccak.EmptyTreeHash,
                ReceiptsRoot = Keccak.EmptyTreeHash,
                MixHash = new Hash256("0x0000000000000000000000000000000000000000000000200000000000000000"),
                Hash = Hash256.Zero,
                Bloom = Bloom.Empty
            }),

            // Block number transitions
            HomesteadBlockNumber = 0x0,
            TangerineWhistleBlockNumber = 0x0,
            SpuriousDragonBlockNumber = 0x0,
            ByzantiumBlockNumber = 0x0,
            ConstantinopleBlockNumber = 0x0,
            ConstantinopleFixBlockNumber = 0x0,
            IstanbulBlockNumber = 0x0,
            BerlinBlockNumber = 0x0,
            LondonBlockNumber = 0x0,
            MergeForkIdBlockNumber = 0x0,

            // Timestamp transitions
            ShanghaiTimestamp = 0x63fd7d60,

            TerminalTotalDifficulty = UInt256.Parse("0x3c6568f12e8000"),
            Parameters = CreateChainParameters(),
            EngineChainSpecParametersProvider = CreateEngineProvider(initialArbOsVersion),
            Allocations = CreateAllocations()
        };

        return chainSpec;
    }

    /// <summary>
    /// Creates a dynamic spec provider for testing with the specified chain spec.
    /// This matches production behavior where specs change based on ArbOS version.
    /// </summary>
    public static ISpecProvider CreateDynamicSpecProvider(ChainSpec chainSpec)
    {
        ArbitrumChainSpecEngineParameters parameters = chainSpec.EngineChainSpecParametersProvider
            .GetChainSpecParameters<ArbitrumChainSpecEngineParameters>();

        ArbitrumChainSpecBasedSpecProvider baseProvider = new(chainSpec, LimboLogs.Instance);
        ArbosStateVersionProvider versionProvider = new(parameters);
        return new ArbitrumDynamicSpecProvider(baseProvider, versionProvider);
    }

    /// <summary>
    /// Creates a dynamic spec provider with a specific ArbOS version.
    /// </summary>
    public static ISpecProvider CreateDynamicSpecProvider(ulong arbOsVersion = 32)
    {
        ChainSpec chainSpec = Create(arbOsVersion);
        return CreateDynamicSpecProvider(chainSpec);
    }

    private static ChainParameters CreateChainParameters()
    {
        return new ChainParameters
        {
            MaxCodeSize = 0x6000,
            MaxCodeSizeTransition = 0x0,
            GasLimitBoundDivisor = 0x400,
            MaximumExtraDataSize = 0x20,
            MinGasLimit = 0x1388,
            ForkBlock = 0x0,

            // EIP transitions
            Eip150Transition = 0x0,
            Eip152Transition = 0x0,
            Eip160Transition = 0x0,
            Eip161abcTransition = 0x0,
            Eip161dTransition = 0x0,
            Eip155Transition = 0x0,
            Eip140Transition = 0x0,
            Eip211Transition = 0x0,
            Eip214Transition = 0x0,
            Eip658Transition = 0x0,
            Eip145Transition = 0x0,
            Eip1014Transition = 0x0,
            Eip1052Transition = 0x0,
            Eip1108Transition = 0x0,
            Eip1283Transition = 0x0,
            Eip1283DisableTransition = 0x0,
            Eip1344Transition = 0x0,
            Eip1884Transition = 0x0,
            Eip2028Transition = 0x0,
            Eip2200Transition = 0x0,
            Eip1559Transition = 0x0,
            Eip2565Transition = 0x0,
            Eip2929Transition = 0x0,
            Eip2930Transition = 0x0,
            Eip3198Transition = 0x0,
            Eip3529Transition = 0x0,
            Eip3541Transition = 0x0,
            Eip3607Transition = 0x0,

            // EIP timestamp transitions
            Eip2537TransitionTimestamp = 0x67c7fd60,
            Eip3651TransitionTimestamp = 0x63fd7d60,
            Eip3855TransitionTimestamp = 0x63fd7d60,
            Eip3860TransitionTimestamp = 0x63fd7d60,
            Eip1153TransitionTimestamp = 0x65b97d60,
            Eip5656TransitionTimestamp = 0x65b97d60,
            Eip6780TransitionTimestamp = 0x65b97d60,
            Eip4788TransitionTimestamp = 0x65b97d60,
            Eip2935TransitionTimestamp = 0x67c7fd60,
            Eip7702TransitionTimestamp = 0x67c7fd60,

            // EIP 1559 parameters
            Eip1559BaseFeeInitialValue = 0x3b9aca00,
            Eip1559BaseFeeMaxChangeDenominator = 0x8,
            Eip1559ElasticityMultiplier = 0x2,

            // Merge parameters
            MergeForkIdTransition = 0x0,
            TerminalTotalDifficulty = UInt256.Parse("0x3c6568f12e8000"),
            BeaconChainGenesisTimestamp = 0x62b07d60,

            // Contract addresses
            Eip4788ContractAddress = new Address("0x000f3df6d732807ef1319fb7b8bb8522d0beac02"),
            Eip7002ContractAddress = new Address("0x00000961ef480eb55e80d19ad83579a64c007002"),
            Eip7251ContractAddress = new Address("0x0000bbddc7ce488642fb579f8b00f3a590007251"),
            Eip2935ContractAddress = new Address("0x0000f90827f1c53a10cb7a02335b175320002935"),

            Eip2935RingBufferSize = 393168
        };
    }

    private static IChainSpecParametersProvider CreateEngineProvider(ulong initialArbOsVersion = 32)
    {
        return new TestChainSpecParametersProvider(new ArbitrumChainSpecEngineParameters
        {
            Enabled = true,
            InitialArbOSVersion = initialArbOsVersion,
            InitialChainOwner = new Address("0x5E1497dD1f08C87b2d8FE23e9AAB6c1De833D927"),
            GenesisBlockNum = 0,
            EnableArbOS = true,
            AllowDebugPrecompiles = true,
            DataAvailabilityCommittee = false
        });
    }

    private static Dictionary<Address, ChainSpecAllocation> CreateAllocations()
    {
        return new Dictionary<Address, ChainSpecAllocation>
        {
            {
                new Address("0x0000000000000000000000000000000000000000"),
                new ChainSpecAllocation
                {
                    Balance = UInt256.Parse("0x1"),
                    Nonce = 0x0
                }
            }
        };
    }
}
