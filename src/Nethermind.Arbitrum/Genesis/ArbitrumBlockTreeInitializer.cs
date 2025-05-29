using Nethermind.Api;
using Nethermind.Arbitrum.Data;
using Nethermind.Config;
using Nethermind.Core;
using Nethermind.Core.Events;

namespace Nethermind.Arbitrum.Genesis;

public class ArbitrumBlockTreeInitializer(INethermindApi api)
{
    private static readonly Lock _lock = new();
    private readonly TimeSpan _genesisProcessedTimeout = TimeSpan.FromMilliseconds(api.Config<IBlocksConfig>().GenesisTimeoutMs);
    private bool _isInitialized;

    public Block Initialize(ParsedInitMessage initMessage)
    {
        ArgumentNullException.ThrowIfNull(api.ChainSpec);
        ArgumentNullException.ThrowIfNull(api.SpecProvider);
        ArgumentNullException.ThrowIfNull(api.MainProcessingContext);
        ArgumentNullException.ThrowIfNull(api.BlockTree);

        lock (_lock)
        {
            if (_isInitialized)
            {
                throw new InvalidOperationException("Arbitrum block tree has already been initialized.");
            }

            ArbitrumGenesisLoader genesisLoader = new(
                api.ChainSpec,
                api.SpecProvider,
                api.MainProcessingContext.WorldState,
                initMessage,
                api.Config<IArbitrumConfig>(),
                api.LogManager);

            Block genesis = genesisLoader.Load();
            Task genesisProcessedTask = Wait.ForEventCondition<BlockEventArgs>(
                CancellationToken.None,
                e => api.BlockTree.NewHeadBlock += e,
                e => api.BlockTree.NewHeadBlock -= e,
                args => args.Block.Header.Hash == genesis.Header.Hash);

            api.BlockTree.SuggestBlock(genesis);

            genesisProcessedTask.Wait(_genesisProcessedTimeout);

            _isInitialized = true;

            return genesis;
        }
    }
}
