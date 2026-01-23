using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm.State;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public static class ArbOSInitialization
{
    public static Block Create(IWorldState worldState, ISpecProvider? specProvider = null)
    {
        ChainSpec chainSpec = FullChainSimulationChainSpecProvider.Create();
        ArbitrumChainSpecEngineParameters parameters = chainSpec.EngineChainSpecParametersProvider
            .GetChainSpecParameters<ArbitrumChainSpecEngineParameters>();
        IArbitrumSpecHelper specHelper = new ArbitrumSpecHelper(parameters);

        specProvider ??= FullChainSimulationChainSpecProvider.CreateDynamicSpecProvider(chainSpec);

        DigestInitMessage digestInitMessage = FullChainSimulationInitMessage.CreateDigestInitMessage(92);
        ParsedInitMessage parsedInitMessage = new(
            chainSpec.ChainId,
            digestInitMessage.InitialL1BaseFee,
            null,
            digestInitMessage.SerializedChainConfig);

        ArbitrumGenesisStateInitializer stateInitializer = new(
            chainSpec,
            specHelper,
            LimboLogs.Instance);

        ArbitrumGenesisLoader genesisLoader = new(
            chainSpec,
            specProvider,
            worldState,
            parsedInitMessage,
            stateInitializer,
            LimboLogs.Instance);

        return genesisLoader.Load();
    }
}
