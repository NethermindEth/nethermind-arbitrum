using Nethermind.Core.Specs;
using Nethermind.Int256;
using Nethermind.Specs.Forks;

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
    public IReleaseSpec GetSpec(ForkActivation forkActivation)
    {
        return FullChainSimulationReleaseSpec.Instance;
    }
}

public class FullChainSimulationReleaseSpec : Cancun // Based on EVM Rules of Full Chain Simulation
{
    private static IReleaseSpec _instance = null!;

    private FullChainSimulationReleaseSpec()
    {
        Name = "FullChainSimulation";
    }

    public new static IReleaseSpec Instance => LazyInitializer.EnsureInitialized(ref _instance, static () => new FullChainSimulationReleaseSpec());
}
