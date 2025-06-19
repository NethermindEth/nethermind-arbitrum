using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.State;

public static class ArbOSInitialization
{
    public static (IWorldState, Block) Create()
    {
        ChainSpec chainSpec = FullChainSimulationChainSpecProvider.Create();
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        ArbitrumChainSpecEngineParameters parameters = chainSpec.EngineChainSpecParametersProvider
            .GetChainSpecParameters<ArbitrumChainSpecEngineParameters>();
        IArbitrumSpecHelper specHelper = new ArbitrumSpecHelper(parameters);

        DigestInitMessage digestInitMessage = FullChainSimulationInitMessage.CreateDigestInitMessage(92);
        ParsedInitMessage parsedInitMessage = new(
            chainSpec.ChainId,
            digestInitMessage.InitialL1BaseFee,
            null,
            digestInitMessage.SerializedChainConfig);

        ArbitrumGenesisLoader genesisLoader = new(
            chainSpec,
            FullChainSimulationSpecProvider.Instance,
            specHelper,
            worldStateManager.GlobalWorldState,
            parsedInitMessage,
            LimboLogs.Instance);

        Block genesisBlock = genesisLoader.Load();

        return (worldStateManager.GlobalWorldState, genesisBlock);
    }
}
