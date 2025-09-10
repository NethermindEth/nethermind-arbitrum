using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Core;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.State;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public static class ArbOSInitialization
{
    public static Block Create(IWorldState worldState)
    {
        ChainSpec chainSpec = FullChainSimulationChainSpecProvider.Create();
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
            worldState,
            parsedInitMessage,
            LimboLogs.Instance);

        Block genesisBlock = genesisLoader.Load();

        return genesisBlock;
    }
}
