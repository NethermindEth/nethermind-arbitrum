using System.Text;
using Autofac;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Modules;
using Nethermind.Arbitrum.Config;
using Nethermind.Core;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public class ArbitrumRpcTestBlockchain : ArbitrumTestBlockchainBase
{
    private ArbitrumRpcTestBlockchain(ChainSpec chainSpec) : base(chainSpec)
    {
    }

    public IArbitrumRpcModule ArbitrumRpcModule { get; private set; } = null!;
    public IArbitrumSpecHelper SpecHelper => Dependencies.SpecHelper;

    public static ArbitrumRpcTestBlockchain CreateDefault(Action<ContainerBuilder>? configurer = null, ChainSpec? chainSpec = null)
    {
        return CreateInternal(new ArbitrumRpcTestBlockchain(chainSpec ?? FullChainSimulationChainSpecProvider.Create()), configurer);
    }

    public void DumpBlocks()
    {
        List<Block> blocks = new();
        Block? current = BlockTree.Head;
        while (current != null)
        {
            blocks.Add(current);
            current = current.ParentHash is not null ? BlockTree.FindBlock(current.ParentHash) : null;
        }

        blocks.Reverse();

        StringBuilder sb = new();
        foreach (Block block in blocks)
            sb.Append(block.ToString(Block.Format.Full));

        Console.WriteLine("\n\n# Chain blocks:\n");
        Console.WriteLine(sb.ToString());
    }

    private static ArbitrumRpcTestBlockchain CreateInternal(ArbitrumRpcTestBlockchain chain, Action<ContainerBuilder>? configurer)
    {
        chain.Build(configurer);

        chain.ArbitrumRpcModule = new ArbitrumRpcModuleFactory(
                chain.Container.Resolve<ArbitrumBlockTreeInitializer>(),
                chain.BlockTree,
                chain.BlockProductionTrigger,
                chain.ArbitrumRpcTxSource,
                chain.ChainSpec,
                chain.Dependencies.SpecHelper,
                chain.LogManager,
                chain.Dependencies.CachedL1PriceData,
                chain.BlockProcessingQueue,
                chain.Container.Resolve<IArbitrumConfig>())
            .Create();

        return chain;
    }
}
