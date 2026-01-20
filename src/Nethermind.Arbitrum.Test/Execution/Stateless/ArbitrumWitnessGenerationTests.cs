using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using System.Threading.Tasks;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain;
using Nethermind.Blockchain.BeaconBlockRoot;
using Nethermind.Blockchain.Blocks;
using Nethermind.Blockchain.Find;
using Nethermind.Blockchain.Headers;
using Nethermind.Blockchain.Receipts;
using Nethermind.Blockchain.Tracing;
using Nethermind.Config;
using Nethermind.Consensus;
using Nethermind.Consensus.ExecutionRequests;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Consensus.Rewards;
using Nethermind.Consensus.Stateless;
using Nethermind.Consensus.Transactions;
using Nethermind.Consensus.Validators;
using Nethermind.Consensus.Withdrawals;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Core.Test;
using Nethermind.Core.Test.Blockchain;
using Nethermind.Core.Test.Builders;
using Nethermind.Core.Test.Db;
using Nethermind.Db;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Modules;
using Nethermind.Logging;
using Nethermind.Merge.Plugin;
using Nethermind.Specs;
using Nethermind.State;
using Nethermind.Trie;
using Nethermind.Trie.Pruning;
using NSubstitute;
using static Nethermind.Core.Block;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Stateless;

namespace Nethermind.Arbitrum.Test.Execution;

public class ArbitrumWitnessGenerationTests
{
    [TestCase(1ul)]
    [TestCase(2ul)]
    [TestCase(3ul)]
    [TestCase(4ul)]
    [TestCase(5ul)]
    [TestCase(6ul)]
    [TestCase(7ul)]
    [TestCase(8ul)]
    [TestCase(9ul)]
    [TestCase(10ul)]
    [TestCase(11ul)]
    [TestCase(12ul)]
    [TestCase(13ul)]
    [TestCase(14ul)]
    [TestCase(15ul)]
    [TestCase(16ul)]
    [TestCase(17ul)]
    [TestCase(18ul)]
    public async Task RecordBlockCreation_Witness_AllowsStatelessExecution(ulong messageIndex)
    {
        FullChainSimulationRecordingFile recording = new("./Recordings/1__arbos32_basefee92.jsonl");
        DigestMessageParameters digestMessage = recording.GetDigestMessages().First(m => m.Index == messageIndex);

        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(recording)
            .Build();

        ResultWrapper<RecordResult> recordResultWrapper = await chain.ArbitrumRpcModule.RecordBlockCreation(new RecordBlockCreationParameters(digestMessage.Index, digestMessage.Message));
        RecordResult recordResult = ThrowOnFailure(recordResultWrapper, digestMessage.Index);

        Witness witness = recordResult.Witness;

        ISpecProvider specProvider = FullChainSimulationChainSpecProvider.CreateDynamicSpecProvider();
        ArbitrumStatelessBlockProcessingEnv blockProcessingEnv =
            new(witness, specProvider, Always.Valid, chain.WasmStore, chain.ArbosVersionProvider, chain.LogManager);

        Block block = chain.BlockFinder.FindBlock(recordResult.BlockHash)
            ?? throw new ArgumentException($"Unable to find block {recordResult.BlockHash}");
        BlockHeader parent = chain.BlockFinder.FindHeader(block.ParentHash!)
            ?? throw new ArgumentException($"Unable to find parent for block {recordResult.BlockHash}");

        using (blockProcessingEnv.WorldState.BeginScope(parent))
        {
            (Block processed, TxReceipt[] _) = blockProcessingEnv.BlockProcessor.ProcessOne(
                block,
                ProcessingOptions.DoNotUpdateHead | ProcessingOptions.ReadOnlyChain,
                NullBlockTracer.Instance,
                specProvider.GetSpec(block.Header));

            Assert.That(processed.Hash, Is.EqualTo(block.Hash));
        }
    }

    private static T ThrowOnFailure<T>(ResultWrapper<T> result, ulong msgIndex)
    {
        if (result.Result != Result.Success)
            throw new InvalidOperationException($"Failed to execute RPC method, message index {msgIndex}, code {result.ErrorCode}: {result.Result.Error}");

        return result.Data;
    }

    // [Test]
    // public async Task SimpleArbitrumTest()
    // {
    //     IDbProvider dbProvider = TestMemDbProvider.Init();
    //     ILogManager logManager = LimboLogs.Instance;

    //     PruningConfig pruningConfig = new();
    //     TestFinalizedStateProvider finalizedStateProvider = new(pruningConfig.PruningBoundary);
    //     TrieStore store = new(
    //         new NodeStorage(dbProvider.StateDb),
    //         No.Pruning,
    //         Persist.EveryBlock,
    //         finalizedStateProvider,
    //         pruningConfig,
    //         LimboLogs.Instance);
    //     finalizedStateProvider.TrieStore = store;

    //     // StateTree stateTree = new(store.GetTrieStore(null), LimboLogs.Instance);

    //     long blockNumber = 0;
    //     // store.BeginBlockCommit(blockNumber);
    //     Address account123 = new("0x0000000000000000000000000000000000000123");
    //     StateTree stateTree;
    //     using (IBlockCommitter committer = store.BeginBlockCommit(blockNumber))
    //     {
    //         StorageTree storageTree = new(store.GetTrieStore(account123), LimboLogs.Instance);
    //         UInt256 key0 = 0;
    //         UInt256 value0 = 10;
    //         storageTree.Set(key0, value0.ToBigEndian());
    //         UInt256 key1 = 1;
    //         UInt256 value1 = 15;
    //         storageTree.Set(key1, value1.ToBigEndian());
    //         UInt256 key2 = 2;
    //         UInt256 value2 = 20;
    //         storageTree.Set(key2, value2.ToBigEndian());
    //         storageTree.Commit();

    //         Account account = Build.An.Account.WithBalance(1.Ether()).WithStorageRoot(storageTree.RootHash).TestObject;
    //         stateTree = new(store.GetTrieStore(null), LimboLogs.Instance);
    //         stateTree.Set(account123, account);
    //         stateTree.Commit();
    //     }

    //     WorldState worldState = new(store, dbProvider.CodeDb, logManager);
    //     StateReader stateReader = new(store, dbProvider.CodeDb, logManager);

    //     NullSealEngine sealer = NullSealEngine.Instance;
    //     MainnetSpecProvider specProvider = MainnetSpecProvider.Instance;


    //     Address sender = TestItem.AddressA;
    //     Hash256 stateRoot;
    //     using (var scope = worldState.BeginScope(new BlockHeader { StateRoot = stateTree.RootHash }))
    //     // using (var scope = worldState.BeginScope(null))
    //     {
    //         // 0x60, 0x00, 0x60, 0x01, 0x55 = store value 0 at slot 1 in contract storage
    //         byte[] runtimeCode = Prepare.EvmCode.PushData(1).PushData(0).SSTORE().Done;
    //         worldState.InsertCode(account123, runtimeCode, specProvider.GenesisSpec);

    //         worldState.CreateAccountIfNotExists(sender, 1.Ether());
    //         // worldState.AddToBalance(sender, 1.Ether(), specProvider.GenesisSpec);

    //         worldState.Commit(specProvider.GenesisSpec);

    //         var bal = worldState.GetBalance(account123);
    //         Console.WriteLine($"--- {bal} ---");

    //         // TODO (Goal is to see why DumpState returns missing node)
    //         // 1. Check with current implem, i expect it might give an error when calling SetRootHash() in UpdateRootHash()
    //         // bc it'll try fetching it from the trie store but the root ref won't have been set there yet
    //         // |--> for that check the node shards after stateTree.Commit() and then continue from bulkWrite.Set()
    //         // ANSWER: the boolean is false and therefore RootRef is not fetched from trieStore and we keep the in-memory one,
    //         // so, it works fine but does not do what i want, need to call CommitTree() !
    //         // 2. Use CommitTree() instead of RecalculateStateRoot()
    //         // worldState.RecalculateStateRoot(); // TODO: 1. just try keeping this and see what DumpState does
    //         worldState.CommitTree(blockNumber: 0); // TODO: 2. try this, this should make the DumpState work
    //         stateRoot = worldState.StateRoot;
    //     }

    //     Console.WriteLine("--- print state 1 ---");
    //     Console.WriteLine(stateReader.DumpState(stateRoot));

    //     using (var scope = worldState.BeginScope(null))
    //     {
    //         // AddressA will be our sender
    //         // worldState.AddToBalanceAndCreateIfNotExists(new(accountAddr0), 1.Ether(), specProvider.GetSpec(new ForkActivation(0)));
    //         // // AddressB will be our receiver
    //         // worldState.CreateAccount(TestItem.AddressB, 0);
    //         // worldState.Commit(specProvider.GetSpec(new ForkActivation(0)));
    //         // worldState.RecalculateStateRoot();
    //     }

    //     // Create Genesis with the correct state root
    //     Block genesis = Build.A.Block.Genesis
    //         .WithStateRoot(stateRoot)
    //         .TestObject;
    //     // BlockTree blockTree = Build.A.BlockTree().TestObject;
    //     // Initialize BlockTree with Genesis (OfChainLength(1) ensures it's added)
    //     IHeaderStore headerStore = new HeaderStore(new MemDb(), new MemDb());

    //     BlockTreeBuilder blockTreeBuilder = Build.A.BlockTree(genesis);
    //     blockTreeBuilder.HeaderStore = headerStore;
    //     BlockTree blockTree = blockTreeBuilder
    //         .OfChainLength(1)
    //         .TestObject;

    //     IncrementalTimestamper timestamper = new();
    //     // var logManager = LimboLogs.Instance;
    //     BlocksConfig blocksConfig = new();
    //     NoBlockRewards rewardCalculator = NoBlockRewards.Instance;

    //     // new BlockhashCache(new HeaderStore(new MemDb(), new MemDb()))
    //     BlockhashProvider blockhashProvider = new(new BlockhashCache(headerStore, logManager), worldState, logManager);
    //     VirtualMachine vm = new(blockhashProvider, specProvider, logManager);
    //     TransactionProcessor txProcessor = new(new BlobBaseFeeCalculator(), specProvider, worldState, vm, new EthereumCodeInfoRepository(worldState), logManager);

    //     IBlockProcessor.IBlockTransactionsExecutor txExecutor =
    //         new BlockProcessor.BlockValidationTransactionsExecutor(
    //             new ExecuteTransactionProcessorAdapter(txProcessor), worldState);

    //     IHeaderValidator headerValidator = new HeaderValidator(blockTree, sealer, specProvider, logManager);
    //     IBlockValidator blockValidator = new BlockValidator(new TxValidator(specProvider.ChainId), headerValidator,
    //         new UnclesValidator(blockTree, headerValidator, logManager), specProvider, logManager);

    //     BeaconBlockRootHandler beacon = new(txProcessor, worldState);
    //     IBlockProcessor blockProcessor = new BlockProcessor(
    //         specProvider,
    //         blockValidator,
    //         rewardCalculator,
    //         txExecutor,
    //         worldState,
    //         NullReceiptStorage.Instance,
    //         beacon,
    //         new BlockhashStore(worldState),
    //         logManager,
    //         new WithdrawalProcessor(worldState, logManager),
    //         new ExecutionRequestsProcessor(txProcessor));

    //     BranchProcessor branchProcessor = new(
    //         blockProcessor,
    //         specProvider,
    //         worldState,
    //         beacon,
    //         blockhashProvider,
    //         logManager
    //     );

    //     BlockchainProcessor blockchainProcessor = new(
    //         blockTree,
    //         branchProcessor,
    //         new MergeProcessingRecoveryStep(NoPoS.Instance),
    //         stateReader,
    //         logManager,
    //         BlockchainProcessor.Options.Default
    //     );

    //     // Tx to call contract
    //     var signedTx = Build.A.Transaction
    //         .WithTo(account123)
    //         // .WithData(new byte[] { 0x01 })
    //         .WithNonce(0)
    //         .WithValue(0)
    //         .WithGasLimit(1_000_000)
    //         .WithGasPrice(10_0000_000)
    //         .WithSenderAddress(sender)
    //         .SignedAndResolved(TestItem.PrivateKeyA)
    //         .TestObject;

    //     // Transaction transaction = Build.A.Transaction
    //     //     .WithSenderAddress(sender)
    //     //     .WithTo(to)
    //     //     .WithGasLimit((long)gasLimit)
    //     //     .WithMaxFeePerGas(baseFeePerGas * 2)
    //     //     .WithValue(100)
    //     //     .WithType(TxType.EIP1559)
    //     //     .TestObject;


    //     var txSource = Substitute.For<ITxSource>();
    //     txSource.GetTransactions(Arg.Any<BlockHeader>(), Arg.Any<long>(), Arg.Any<PayloadAttributes>(), Arg.Any<bool>())
    //         .Returns(new[] { signedTx });

    //     var producer = new TestBlockProducer(
    //         txSource,
    //         blockchainProcessor,
    //         worldState,
    //         sealer,
    //         blockTree,
    //         timestamper,
    //         specProvider,
    //         logManager,
    //         blocksConfig
    //     );

    //     // 4. Produce block
    //     var parent = blockTree.Head!.Header;
    //     var payloadAttributes = new PayloadAttributes
    //     {
    //         Timestamp = (ulong)parent.Timestamp + 1,
    //         PrevRandao = Hash256.Zero,
    //         SuggestedFeeRecipient = TestItem.AddressB,
    //         Withdrawals = Array.Empty<Withdrawal>(),
    //         ParentBeaconBlockRoot = Hash256.Zero
    //     };
    //     var block = await producer.BuildBlock(payloadAttributes: payloadAttributes, token: CancellationToken.None);
    //     // Console.WriteLine($"Produced block {block!.ToString(Format.Full)}");

    //     Console.WriteLine("--- print state 2 ---");
    //     Console.WriteLine(stateReader.DumpState(stateRoot));
    // }

    // [Test]
    // public async Task SimpleTest()
    // {
    //     // StateTree stateTree = TestItem.Tree.GetStateTree();
    //     // TestItem.Tree.GetTrees(stateTree.TrieStore);

    //     // ITrieStore store = new TestRawTrieStore(new MemDb());

    //     IDbProvider dbProvider = TestMemDbProvider.Init();
    //     ILogManager logManager = LimboLogs.Instance;

    //     PruningConfig pruningConfig = new();
    //     TestFinalizedStateProvider finalizedStateProvider = new(pruningConfig.PruningBoundary);
    //     TrieStore store = new(
    //         new NodeStorage(dbProvider.StateDb),
    //         No.Pruning,
    //         Persist.EveryBlock,
    //         finalizedStateProvider,
    //         pruningConfig,
    //         LimboLogs.Instance);
    //     finalizedStateProvider.TrieStore = store;

    //     StateTree oldStateTree = new(store.GetTrieStore(null), LimboLogs.Instance);

    //     long blockNumber = 0;
    //     store.BeginBlockCommit(blockNumber);

    //     TestItem.Tree.FillStateTreeWithTestAccounts(oldStateTree);

    //     (StateTree stateTree, StorageTree _, Hash256 accountAddr0) = TestItem.Tree.GetTrees(store);
    //     WorldState worldState = new(store, dbProvider.CodeDb, logManager);
    //     StateReader stateReader = new(store, dbProvider.CodeDb, logManager);
    //     using (var scope = worldState.BeginScope(new BlockHeader { StateRoot = stateTree.RootHash }))
    //     {
    //         var bal = worldState.GetBalance(new(accountAddr0));
    //         Console.WriteLine($"--- {bal} ---");
    //     }
    //     NullSealEngine sealer = NullSealEngine.Instance;
    //     MainnetSpecProvider specProvider = MainnetSpecProvider.Instance;

    //     Hash256 stateRoot;
    //     using (var scope = worldState.BeginScope(null))
    //     {
    //         // AddressA will be our sender
    //         // worldState.AddToBalanceAndCreateIfNotExists(new(accountAddr0), 1.Ether(), specProvider.GetSpec(new ForkActivation(0)));
    //         // // AddressB will be our receiver
    //         // worldState.CreateAccount(TestItem.AddressB, 0);
    //         // worldState.Commit(specProvider.GetSpec(new ForkActivation(0)));
    //         // worldState.RecalculateStateRoot();
    //         stateRoot = worldState.StateRoot;
    //     }

    //     // Create Genesis with the correct state root
    //     Block genesis = Build.A.Block.Genesis
    //         .WithStateRoot(stateRoot)
    //         .TestObject;
    //     // BlockTree blockTree = Build.A.BlockTree().TestObject;
    //     // Initialize BlockTree with Genesis (OfChainLength(1) ensures it's added)
    //     IHeaderStore headerStore = new HeaderStore(new MemDb(), new MemDb());

    //     BlockTreeBuilder blockTreeBuilder = Build.A.BlockTree(genesis);
    //     blockTreeBuilder.HeaderStore = headerStore;
    //     BlockTree blockTree = blockTreeBuilder
    //         .OfChainLength(1)
    //         .TestObject;
    //     IncrementalTimestamper timestamper = new();
    //     // var logManager = LimboLogs.Instance;
    //     BlocksConfig blocksConfig = new();
    //     NoBlockRewards rewardCalculator = NoBlockRewards.Instance;

    //     // BlockhashProvider blockhashProvider = new(blockTree, specProvider, worldState, logManager);
    //     // not blocktree ?
    //     BlockhashProvider blockhashProvider = new(new BlockhashCache(headerStore, logManager), worldState, logManager);
    //     VirtualMachine vm = new(blockhashProvider, specProvider, logManager);
    //     TransactionProcessor txProcessor = new(new BlobBaseFeeCalculator(), specProvider, worldState, vm, new EthereumCodeInfoRepository(worldState), logManager);

    //     IBlockProcessor.IBlockTransactionsExecutor txExecutor =
    //         new BlockProcessor.BlockValidationTransactionsExecutor(
    //             new ExecuteTransactionProcessorAdapter(txProcessor), worldState);

    //     IHeaderValidator headerValidator = new HeaderValidator(blockTree, sealer, specProvider, logManager);
    //     IBlockValidator blockValidator = new BlockValidator(new TxValidator(specProvider.ChainId), headerValidator,
    //         new UnclesValidator(blockTree, headerValidator, logManager), specProvider, logManager);

    //     BeaconBlockRootHandler beacon = new(txProcessor, worldState);
    //     IBlockProcessor blockProcessor = new BlockProcessor(
    //         specProvider,
    //         blockValidator,
    //         rewardCalculator,
    //         txExecutor,
    //         worldState,
    //         NullReceiptStorage.Instance,
    //         beacon,
    //         new BlockhashStore(worldState),
    //         logManager,
    //         new WithdrawalProcessor(worldState, logManager),
    //         new ExecutionRequestsProcessor(txProcessor));

    //     BranchProcessor branchProcessor = new(
    //         blockProcessor,
    //         specProvider,
    //         worldState,
    //         beacon,
    //         blockhashProvider,
    //         logManager
    //     );

    //     BlockchainProcessor blockchainProcessor = new(
    //         blockTree,
    //         branchProcessor,
    //         new MergeProcessingRecoveryStep(NoPoS.Instance),
    //         stateReader,
    //         logManager,
    //         BlockchainProcessor.Options.Default
    //     );

    //     // 2. Transaction
    //     var signedTx = Build.A.Transaction
    //         .WithTo(TestItem.AddressB)
    //         // .WithData(new byte[] { 0x01 })
    //         .WithNonce(0)
    //         .WithValue(100)
    //         .WithGasLimit(22000)
    //         .WithGasPrice(1000000000)
    //         .WithSenderAddress(new(accountAddr0))
    //         // .SignedAndResolved()
    //         .TestObject;

    //     // Transaction transaction = Build.A.Transaction
    //     //     .WithSenderAddress(sender)
    //     //     .WithTo(to)
    //     //     .WithGasLimit((long)gasLimit)
    //     //     .WithMaxFeePerGas(baseFeePerGas * 2)
    //     //     .WithValue(100)
    //     //     .WithType(TxType.EIP1559)
    //     //     .TestObject;


    //     var txSource = Substitute.For<ITxSource>();
    //     txSource.GetTransactions(Arg.Any<BlockHeader>(), Arg.Any<long>(), Arg.Any<PayloadAttributes>(), Arg.Any<bool>())
    //         .Returns(new[] { signedTx });

    //     var producer = new TestBlockProducer(
    //         txSource,
    //         blockchainProcessor,
    //         worldState,
    //         sealer,
    //         blockTree,
    //         timestamper,
    //         specProvider,
    //         logManager,
    //         blocksConfig
    //     );

    //     // 4. Produce block
    //     var parent = blockTree.Head!.Header;
    //     var payloadAttributes = new PayloadAttributes
    //     {
    //         Timestamp = (ulong)parent.Timestamp + 1,
    //         PrevRandao = Hash256.Zero,
    //         SuggestedFeeRecipient = TestItem.AddressB,
    //         Withdrawals = Array.Empty<Withdrawal>(),
    //         ParentBeaconBlockRoot = Hash256.Zero
    //     };
    //     var block = await producer.BuildBlock(payloadAttributes: payloadAttributes, token: CancellationToken.None);

    //     // worldState.CommitTree

    //     // Assert.IsNotNull(block);
    //     Console.WriteLine($"Produced block {block?.Hash}");


    //     //  WitnessGeneratingBlockProcessingEnv env = new(
    //     //     specProvider,
    //     //     stateReader,
    //     //     worldState,
    //     //     new ReadOnlyBlockTree(blockTree),
    //     //     sealer,
    //     //     rewardCalculator,
    //     //     logManager
    //     // );
    // }

    private async Task SaveWitnessToJsonFile(Witness witness)
    {
        var json = JsonSerializer.Serialize(witness, new JsonSerializerOptions {
            WriteIndented = true,
            IncludeFields = true,
            Converters = {
                new ByteArrayHexConverter()
            }
        });
        await File.WriteAllTextAsync("/Users/gugz/Documents/rpc_req/witness_from_test.json",
            json
        );
    }

    public class ByteArrayHexConverter : JsonConverter<byte[]>
    {
        // public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        // {
        //     string hex = reader.GetString()!;
        //     return Convert.FromHexString(hex.StartsWith("0x") ? hex.Substring(2) : hex);
        // }
        public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string hex = reader.GetString()!;

            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hex = hex.Substring(2);

            return Convert.FromHexString(hex);
        }

        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
        {
            writer.WriteStringValue("0x" + Convert.ToHexString(value).ToLowerInvariant());
            // writer.WriteStringValue(Convert.ToHexString(value)); // uppercase hex
        }
    }

}
