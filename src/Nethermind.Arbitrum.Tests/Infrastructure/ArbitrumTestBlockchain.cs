using Autofac;
using Nethermind.Core.Test.Blockchain;

namespace Nethermind.Arbitrum.Tests.Infrastructure;

public class ArbitrumTestBlockchain : TestBlockchain
{
    private ArbitrumTestBlockchain()
    {
    }

    public static async Task<ArbitrumTestBlockchain> Create(Action<ContainerBuilder>? configurer = null)
    {
        ArbitrumTestBlockchain blockchain = new();
        await blockchain.Build(configurer);
        return blockchain;
    }
}
