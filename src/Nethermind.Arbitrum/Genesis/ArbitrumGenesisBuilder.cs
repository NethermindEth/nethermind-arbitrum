using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Crypto;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Genesis;

/// <summary>
/// Builds Arbitrum genesis block by initializing ArbOS state.
/// </summary>
public class ArbitrumGenesisBuilder : IGenesisBuilder
{
    private readonly ChainSpec _chainSpec;
    private readonly ISpecProvider _specProvider;
    private readonly IArbitrumSpecHelper _specHelper;
    private readonly IWorldState _worldState;
    private readonly ArbitrumGenesisStateInitializer _stateInitializer;
    private readonly ILogger _logger;

    public ArbitrumGenesisBuilder(
        ChainSpec chainSpec,
        ISpecProvider specProvider,
        IArbitrumSpecHelper specHelper,
        IWorldState worldState,
        ArbitrumGenesisStateInitializer stateInitializer,
        ILogManager logManager)
    {
        _chainSpec = chainSpec;
        _specProvider = specProvider;
        _specHelper = specHelper;
        _worldState = worldState;
        _stateInitializer = stateInitializer;
        _logger = logManager.GetClassLogger<ArbitrumGenesisBuilder>();
    }

    public Block Build()
    {
        // Create init message from chainspec
        ChainSpecInitMessageProvider initMessageProvider = new(_chainSpec, _specHelper);
        ParsedInitMessage initMessage = initMessageProvider.GetInitMessage();

        _stateInitializer.ValidateInitMessage(initMessage);

        _worldState.CreateAccountIfNotExists(ArbosAddresses.ArbosSystemAccount, UInt256.Zero, UInt256.One);
        _logger.Info($"Preallocated ArbOS system account: {ArbosAddresses.ArbosSystemAccount}");

        _stateInitializer.InitializeArbosState(initMessage, _worldState, _specProvider);
        _stateInitializer.Preallocate(_worldState, _specProvider);

        _worldState.Commit(_specProvider.GenesisSpec, true);
        _worldState.CommitTree(0);

        Block genesis = _chainSpec.Genesis;
        genesis.Header.StateRoot = _worldState.StateRoot;
        genesis.Header.Hash = genesis.Header.CalculateHash();

        _logger.Info($"Arbitrum genesis block built: Number={genesis.Header.Number}, Hash={genesis.Header.Hash}, StateRoot={genesis.Header.StateRoot}");

        return genesis;
    }
}
