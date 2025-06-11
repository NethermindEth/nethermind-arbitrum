using Nethermind.Api;
using Nethermind.Core.Crypto;
using Nethermind.Init.Steps;
using Nethermind.State;

namespace Nethermind.Arbitrum.Genesis;

public class ArbitrumLoadGenesisBlockStep(ArbitrumNethermindApi api) : LoadGenesisBlock(api)
{
    protected override Task Load(IMainProcessingContext mainProcessingContext)
    {
        return Task.CompletedTask;
    }

    protected override void ValidateGenesisHash(Hash256? expectedGenesisHash, IWorldState worldState)
    {
    }
}
