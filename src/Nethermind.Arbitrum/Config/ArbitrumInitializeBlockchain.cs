using Nethermind.Consensus.Producers;
using Nethermind.Init.Steps;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumInitializeBlockchain(ArbitrumNethermindApi api) : InitializeBlockchain(api)
{
    protected override IBlockProductionPolicy CreateBlockProductionPolicy() => AlwaysStartBlockProductionPolicy.Instance;

}
