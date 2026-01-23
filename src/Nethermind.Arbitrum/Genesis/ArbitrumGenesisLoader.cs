using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Crypto;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Genesis;

public class ArbitrumGenesisLoader
{
    private readonly ChainSpec _chainSpec;
    private readonly ISpecProvider _specProvider;
    private readonly IWorldState _worldState;
    private readonly ParsedInitMessage _initMessage;
    private readonly ArbitrumGenesisStateInitializer _stateInitializer;
    private readonly ILogger _logger;

    public ArbitrumGenesisLoader(
        ChainSpec chainSpec,
        ISpecProvider specProvider,
        IWorldState worldState,
        ParsedInitMessage initMessage,
        ArbitrumGenesisStateInitializer stateInitializer,
        ILogManager logManager)
    {
        _chainSpec = chainSpec;
        _specProvider = specProvider;
        _worldState = worldState;
        _initMessage = initMessage;
        _stateInitializer = stateInitializer;
        _logger = logManager.GetClassLogger<ArbitrumGenesisLoader>();
    }

    public Block Load()
    {
        _logger.Info("Loading genesis from DigestInitMessage");
        _stateInitializer.ValidateInitMessage(_initMessage);

        _worldState.CreateAccountIfNotExists(ArbosAddresses.ArbosSystemAccount, UInt256.Zero, UInt256.One);
        _logger.Info($"Preallocated ArbOS system account: {ArbosAddresses.ArbosSystemAccount}");

        _stateInitializer.InitializeArbosState(_initMessage, _worldState, _specProvider);
        _stateInitializer.Preallocate(_worldState, _specProvider);

        _worldState.Commit(_specProvider.GenesisSpec, true);
        _worldState.CommitTree(0);

        Block genesis = _chainSpec.Genesis;
        genesis.Header.StateRoot = _worldState.StateRoot;
        genesis.Header.Hash = genesis.Header.CalculateHash();

        _logger.Info($"Arbitrum genesis block loaded from DigestInitMessage: Number={genesis.Header.Number}, Hash={genesis.Header.Hash}, StateRoot={genesis.Header.StateRoot}");

        return genesis;
    }
}
