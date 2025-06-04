using Autofac;
using Nethermind.Api;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Modules;
using Nethermind.Config;
using Nethermind.Core;
using Nethermind.Init.Steps;

namespace Nethermind.Arbitrum.Genesis;

public class ArbitrumLoadGenesisBlockStep(INethermindApi api) : LoadGenesisBlock(api)
{
    private readonly TimeSpan _genesisProcessedTimeout = TimeSpan.FromMilliseconds(api.Config<IBlocksConfig>().GenesisTimeoutMs);

    protected override async Task Load(IMainProcessingContext mainProcessingContext)
    {
        if (api.ChainSpec is null) throw new StepDependencyException(nameof(api.ChainSpec));
        if (api.BlockTree is null) throw new StepDependencyException(nameof(api.BlockTree));
        if (api.SpecProvider is null) throw new StepDependencyException(nameof(api.SpecProvider));

        ArbitrumRpcBroker broker = api.Context.Resolve<ArbitrumRpcBroker>();
        using MessageContext context = await broker.WaitForMessageAsync();
        IArbitrumTransactionData abstractMessage = context.Request[0];
        if (abstractMessage is not ParsedInitMessage parsedInitMessage)
        {
            throw new InvalidOperationException($"Expected {typeof(ParsedInitMessage)} as the first message in the broker queue, but was {abstractMessage.GetType()}.");
        }

        ArbitrumGenesisLoader genesisLoader = new(
            api.ChainSpec,
            api.SpecProvider!,
            api.MainProcessingContext!.WorldState,
            parsedInitMessage,
            api.Config<IArbitrumConfig>(),
            api.LogManager);

        Block genesis = genesisLoader.Load();

        ManualResetEventSlim genesisProcessedEvent = new(false);

        void GenesisProcessed(object? sender, BlockEventArgs args)
        {
            api.BlockTree.NewHeadBlock -= GenesisProcessed;
            genesisProcessedEvent.Set();
        }

        api.BlockTree.NewHeadBlock += GenesisProcessed;
        api.BlockTree.SuggestBlock(genesis);
        bool genesisLoaded = genesisProcessedEvent.Wait(_genesisProcessedTimeout);

        if (!genesisLoaded)
        {
            throw new TimeoutException($"Genesis block was not processed after {_genesisProcessedTimeout.TotalSeconds} seconds. If you are running custom chain with very big genesis file consider increasing {nameof(BlocksConfig)}.{nameof(IBlocksConfig.GenesisTimeoutMs)}.");
        }
    }
}
