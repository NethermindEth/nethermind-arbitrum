using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Config;
using Nethermind.State;

namespace Nethermind.Arbitrum.Arbos;

public interface IArbosVersionProvider
{
    ulong Get();
}

public class ArbosStateVersionProvider(IWorldState state) : IArbosVersionProvider
{
    public ulong Get()
    {
        ArbosStorage backingStorage = new(state, new ZeroGasBurner(), ArbosAddresses.ArbosSystemAccount);
        return backingStorage.GetULong(ArbosStateOffsets.VersionOffset);
    }
}

public class ChainSpecVersionProvider(ArbitrumChainSpecEngineParameters parameters) : IArbosVersionProvider
{
    public ulong Get() => parameters.InitialArbOSVersion ?? 0;
}
