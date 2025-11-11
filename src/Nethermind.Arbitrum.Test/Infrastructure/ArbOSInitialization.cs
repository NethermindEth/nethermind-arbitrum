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

        specProvider ??= ArbitrumTestBlockchainBase.CreateDynamicSpecProvider(chainSpec);

        DigestInitMessage digestInitMessage = FullChainSimulationInitMessage.CreateDigestInitMessage(92);
        ParsedInitMessage parsedInitMessage = new(
            chainSpec.ChainId,
            digestInitMessage.InitialL1BaseFee,
            null,
            digestInitMessage.SerializedChainConfig);

        ArbitrumGenesisLoader genesisLoader = new(
            chainSpec,
            specProvider,
            specHelper,
            worldState,
            parsedInitMessage,
            LimboLogs.Instance);

        return genesisLoader.Load();
    }
}
