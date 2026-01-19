using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Config;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Arbos;

public interface IArbosVersionProvider
{
    public ulong Get();
}

public class ArbosStateVersionProvider(ArbitrumChainSpecEngineParameters parameters, IWorldState? state = null) : IArbosVersionProvider
{
    public ulong Get()
    {
        ulong defaultVersion = parameters.InitialArbOSVersion ?? 0;
        if (state is null)
            return defaultVersion;
        ArbosStorage backingStorage = new(state, new ZeroGasBurner(), ArbosAddresses.ArbosSystemAccount);
        ulong arbosVersion = backingStorage.GetULong(ArbosStateOffsets.VersionOffset);
        return arbosVersion > 0 ? arbosVersion : defaultVersion;
    }
}
