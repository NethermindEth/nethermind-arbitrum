using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Int256;
using Nethermind.Specs.Forks;
using System.Collections.Frozen;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public class FullChainSimulationSpecProvider : ISpecProvider
{
    public static FullChainSimulationSpecProvider Instance { get; } = new();

    public void UpdateMergeTransitionInfo(long? blockNumber, UInt256? terminalTotalDifficulty = null)
    {
    }

    public ForkActivation? MergeBlockNumber => null;
    public UInt256? TerminalTotalDifficulty => null;
    public ulong TimestampFork => ISpecProvider.TimestampForkNever;
    public IReleaseSpec GenesisSpec => FullChainSimulationReleaseSpec.Instance;
    public long? DaoBlockNumber => null;
    public ulong? BeaconChainGenesisTimestamp => null;
    public ulong NetworkId => 412346;
    public ulong ChainId => NetworkId;
    public ForkActivation[] TransitionActivations => [];

    public IReleaseSpec GetSpecInternal(ForkActivation forkActivation)
    {
        return FullChainSimulationReleaseSpec.Instance;
    }
}

public class FullChainSimulationReleaseSpec : Cancun // Based on Cancun fork with Arbitrum precompiles for testing
{
    private static IReleaseSpec _instance = null!;

    public FullChainSimulationReleaseSpec()
    {
        Name = "FullChainSimulation";

        IsEip4844Enabled = false; // Disable blobs gas calculation
        IsEip4895Enabled = false; // Disable withdrawals
        IsEip3541Enabled = false; // Disable contract code validation

        Eip2935RingBufferSize = 393168;
    }

    /// <summary>
    /// Override to include Arbitrum precompiles in addition to Ethereum precompiles.
    /// NOTE: This duplicates logic from ArbitrumReleaseSpec.BuildPrecompilesCache() because
    /// ArbitrumReleaseSpec extends ReleaseSpec (empty) for proper chain spec transition support.
    /// </summary>
    public override FrozenSet<AddressAsKey> BuildPrecompilesCache()
    {
        // Get Ethereum precompiles from base Cancun fork
        FrozenSet<AddressAsKey> ethereumPrecompiles = base.BuildPrecompilesCache();

        // Add Arbitrum precompiles (same list as ArbitrumReleaseSpec.BuildPrecompilesCache)
        HashSet<AddressAsKey> allPrecompiles =
        [
            ..ethereumPrecompiles,
            ArbosAddresses.ArbSysAddress, // 0x64
            ArbosAddresses.ArbInfoAddress, // 0x65
            ArbosAddresses.ArbAddressTableAddress, // 0x66
            ArbosAddresses.ArbBLSAddress, // 0x67
            ArbosAddresses.ArbFunctionTableAddress, // 0x68
            ArbosAddresses.ArbosTestAddress, // 0x69
            ArbosAddresses.ArbOwnerPublicAddress, // 0x6b
            ArbosAddresses.ArbGasInfoAddress, // 0x6c
            ArbosAddresses.ArbAggregatorAddress, // 0x6d
            ArbosAddresses.ArbRetryableTxAddress, // 0x6e
            ArbosAddresses.ArbStatisticsAddress, // 0x6f
            ArbosAddresses.ArbOwnerAddress, // 0x70
            ArbosAddresses.ArbDebugAddress, // 0xff
            ArbosAddresses.ArbosAddress // 0xa4b05
        ];

        return allPrecompiles.ToFrozenSet();
    }

    public new static IReleaseSpec Instance => LazyInitializer.EnsureInitialized(ref _instance, static () => new FullChainSimulationReleaseSpec());
}
