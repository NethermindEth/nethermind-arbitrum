using Autofac;
using Nethermind.Api;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Modules;
using Nethermind.Config;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Init.Steps;
using Nethermind.State;

namespace Nethermind.Arbitrum.Genesis;

public class ArbitrumLoadGenesisBlockStep(INethermindApi api) : LoadGenesisBlock(api)
{
    protected override Task Load(IMainProcessingContext mainProcessingContext)
    {
        return Task.CompletedTask;
    }

    protected override void ValidateGenesisHash(Hash256? expectedGenesisHash, IWorldState worldState)
    {
    }
}
