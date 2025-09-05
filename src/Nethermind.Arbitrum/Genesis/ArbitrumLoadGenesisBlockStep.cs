using Nethermind.Api;
using Nethermind.Core.Crypto;
using Nethermind.Evm.State;
using Nethermind.Init.Steps;

namespace Nethermind.Arbitrum.Genesis;

public class ArbitrumLoadGenesisBlockStep(ArbitrumNethermindApi api) : LoadGenesisBlock(api)
{
    protected override Task Load(IMainProcessingContext mainProcessingContext)
    {
        return Task.CompletedTask;
    }
}
