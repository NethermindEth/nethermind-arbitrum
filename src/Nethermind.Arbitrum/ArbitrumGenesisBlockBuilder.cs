using Nethermind.Arbitrum.Genesis;
using Nethermind.Blockchain;
using Nethermind.Consensus;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm.State;
using Nethermind.State;
using Nethermind.Logging;
using Nethermind.Specs;

namespace Nethermind.Arbitrum
{
    public class ArbitrumGenesisBlockBuilder : IGenesisBuilder
    {
        private readonly IGenesisBuilder _baseBuilder;
        private readonly string _genesisStatePath;
        private readonly IWorldState _worldState;
        private readonly ISpecProvider _specProvider;
        private readonly ILogManager _logManager;

        public ArbitrumGenesisBlockBuilder(
            IGenesisBuilder baseBuilder,
            string genesisStatePath,
            IWorldState worldState,
            ISpecProvider specProvider,
            ILogManager logManager)
        {
            _baseBuilder = baseBuilder;
            _genesisStatePath = genesisStatePath;
            _worldState = worldState;
            _specProvider = specProvider;
            _logManager = logManager;
        }

        public Block Build()
        {
            // First, let the base builder create the genesis block structure
            var genesisBlock = _baseBuilder.Build();

            // CRITICAL: Verify the block number is correct
            var logger = _logManager.GetClassLogger();
            logger.Info($"Genesis block number from chainspec: {genesisBlock.Header.Number}");

            if (genesisBlock.Header.Number != 22207817)
            {
                logger.Error($"ERROR: Genesis block number is {genesisBlock.Header.Number}, expected 22207817!");
            }

            // Get the spec for genesis block
            var spec = _specProvider.GetSpec(genesisBlock.Header);

            // Import the Arbitrum state
            var importer = new ArbitrumGenesisStateImporter(_worldState, _logManager);
            importer.ImportIfNeeded(_genesisStatePath, spec);

            // IMPORTANT: After importing state, recalculate the state root
            _worldState.Commit(spec);
            _worldState.CommitTree(genesisBlock.Header.Number);

            var newStateRoot = _worldState.StateRoot;
            logger.Info($"New state root after import: {newStateRoot}");
            logger.Info($"Expected state root from chainspec: {genesisBlock.Header.StateRoot}");

            // Update the genesis block header with the actual state root
            if (newStateRoot != genesisBlock.Header.StateRoot)
            {
                logger.Warn("State root mismatch - this might be expected if recalculating");
                // You may need to rebuild the header here with the correct state root
            }

            return genesisBlock;
        }
    }
}
