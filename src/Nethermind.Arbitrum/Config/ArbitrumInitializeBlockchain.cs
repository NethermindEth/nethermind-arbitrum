using Nethermind.Consensus.Producers;
using Nethermind.Init.Steps;
using Nethermind.TxPool;

public class ArbitrumInitializeBlockchain(ArbitrumNethermindApi api, IChainHeadInfoProvider chainHeadInfoProvider)
    : InitializeBlockchain(api, chainHeadInfoProvider)
{
    protected override IBlockProductionPolicy CreateBlockProductionPolicy()
        => AlwaysStartBlockProductionPolicy.Instance;
}
