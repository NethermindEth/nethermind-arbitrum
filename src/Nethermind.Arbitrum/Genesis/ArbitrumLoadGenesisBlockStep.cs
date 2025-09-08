using Nethermind.Api;
using Nethermind.Init.Steps;

namespace Nethermind.Arbitrum.Genesis;

public class ArbitrumLoadGenesisBlockStep(ArbitrumNethermindApi api) : LoadGenesisBlock(api)
{
    protected override void Load(IMainProcessingContext mainProcessingContext)
    {
    }
}
