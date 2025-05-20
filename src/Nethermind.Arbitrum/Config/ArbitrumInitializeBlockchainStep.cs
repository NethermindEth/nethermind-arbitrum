using Nethermind.Api;
using Nethermind.Consensus.Producers;
using Nethermind.Init.Steps;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumInitializeBlockchainStep(INethermindApi api) : InitializeBlockchain(api)
{
    protected override IBlockProductionPolicy CreateBlockProductionPolicy() => AlwaysStartBlockProductionPolicy.Instance;
}
