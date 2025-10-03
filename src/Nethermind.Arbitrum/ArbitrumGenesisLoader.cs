// using Nethermind.Api;
// using Nethermind.Arbitrum;
// using Nethermind.Blockchain;
// using Nethermind.Consensus.Processing;
// using Nethermind.Core;
// using Nethermind.Core.Crypto;
// using Nethermind.Core.Extensions;
// using Nethermind.Core.Specs;
// using Nethermind.Crypto;
// using Nethermind.Evm.State;
// using Nethermind.Logging;
// using Nethermind.Specs.ChainSpecStyle;
//
// public class ArbitrumGenesisLoader : IGenesisLoader
// {
//     private readonly IBlockTree _blockTree;
//     private readonly IWorldState _worldState;
//     private readonly IBlockchainProcessor _blockchainProcessor;
//     private readonly ISpecProvider _specProvider;
//     private readonly ILogManager _logManager;
//     private readonly IInitConfig _initConfig;
//     private readonly ChainSpec _chainSpec;
//     private readonly ILogger _logger;
//
//     public ArbitrumGenesisLoader(
//         IBlockTree blockTree,
//         IWorldState worldState,
//         IBlockchainProcessor blockchainProcessor,
//         ISpecProvider specProvider,
//         ChainSpec chainSpec,
//         IInitConfig initConfig,
//         ILogManager logManager)
//     {
//         _blockTree = blockTree;
//         _worldState = worldState;
//         _blockchainProcessor = blockchainProcessor;
//         _specProvider = specProvider;
//         _chainSpec = chainSpec;
//         _initConfig = initConfig;
//         _logManager = logManager;
//         _logger = logManager.GetClassLogger<ArbitrumGenesisLoader>();
//     }
//
//     public void Load()
//     {
//         _logger.Info($"Loading Arbitrum genesis (representing Arbitrum block 22207817 as Nethermind block 0)");
//
//         using var _ = _worldState.BeginScope(IWorldState.PreGenesis);
//
//         // Import the Arbitrum state
//         string genesisStatePath = Path.Combine(_initConfig.BaseDbPath, "arbitrum-genesis-state.json");
//         if (File.Exists(genesisStatePath))
//         {
//             var spec = _specProvider.GetSpec((ForkActivation)0);
//             var importer = new ArbitrumGenesisStateImporter(_worldState, _logManager);
//             importer.ImportIfNeeded(genesisStatePath, spec);
//             _worldState.Commit(spec);
//             _worldState.CommitTree(0);
//             _logger.Info($"Imported state from {genesisStatePath}");
//         }
//
//         // Create genesis at block 0 with Arbitrum's state
//         var genesisHeader = new BlockHeader(
//             new Hash256("0x7d237dd685b96381544e223f8906e35645d63b89c19983f2246db48568c07986"),
//             Keccak.OfAnEmptySequenceRlp,
//             Address.Zero,
//             1,
//             0, // Nethermind block 0
//             0x6400000000,
//             0x6310F0EF,
//             Bytes.Empty
//         )
//         {
//             StateRoot = _worldState.StateRoot,
//             TxRoot = Keccak.EmptyTreeHash,
//             ReceiptsRoot = Keccak.EmptyTreeHash,
//             BaseFeePerGas = 0x5f5e100,
//             MixHash = Keccak.Zero,
//             Nonce = 0,
//             Bloom = Bloom.Empty
//         };
//         genesisHeader.Hash = genesisHeader.CalculateHash();
//         var genesis = new Block(genesisHeader);
//
//         _logger.Info($"Created genesis block: Number={genesis.Number}, Hash={genesis.Hash}, StateRoot={genesis.StateRoot}");
//
//         // Standard genesis processing pattern
//         ManualResetEventSlim genesisProcessedEvent = new(false);
//
//         bool wasInvalid = false;
//         void OnInvalidBlock(object? sender, IBlockchainProcessor.InvalidBlockEventArgs args)
//         {
//             _logger.Error($"Genesis block was INVALID");
//             if (args.InvalidBlock.Number != 0) return;
//             _blockchainProcessor.InvalidBlock -= OnInvalidBlock;
//             wasInvalid = true;
//             genesisProcessedEvent.Set();
//         }
//         _blockchainProcessor.InvalidBlock += OnInvalidBlock;
//
//         void GenesisProcessed(object? sender, BlockEventArgs args)
//         {
//             _logger.Info($"Genesis processed event fired: Block {args.Block.Number}");
//             _blockTree.NewHeadBlock -= GenesisProcessed;
//             genesisProcessedEvent.Set();
//         }
//         _blockTree.NewHeadBlock += GenesisProcessed;
//
//         _logger.Info("Suggesting genesis block to BlockTree...");
//         _blockTree.SuggestBlock(genesis);
//
//         _logger.Info("Waiting for genesis to be processed...");
//         bool genesisLoaded = genesisProcessedEvent.Wait(TimeSpan.FromSeconds(40));
//
//         if (!genesisLoaded)
//         {
//             _logger.Error("Genesis processing TIMED OUT");
//             var head = _blockTree.Head;
//             var latestHeader = _blockTree.FindLatestHeader();
//             _logger.Error($"BlockTree.Head: {head?.Number}, FindLatestHeader: {latestHeader?.Number}");
//             throw new TimeoutException("Arbitrum genesis was not processed after 40 seconds.");
//         }
//
//         if (wasInvalid)
//         {
//             throw new InvalidBlockException(genesis, "Error while generating Arbitrum genesis block.");
//         }
//
//         var finalHead = _blockTree.Head;
//         _logger.Info($"Arbitrum genesis loaded successfully. BlockTree.Head is now: {finalHead?.Number}");
//     }
// }
