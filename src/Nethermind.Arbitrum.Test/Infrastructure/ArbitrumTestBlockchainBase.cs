using Autofac;
using Nethermind.Api;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Blockchain;
using Nethermind.Blockchain.BeaconBlockRoot;
using Nethermind.Blockchain.Blocks;
using Nethermind.Blockchain.Find;
using Nethermind.Blockchain.Receipts;
using Nethermind.Config;
using Nethermind.Consensus;
using Nethermind.Consensus.Comparers;
using Nethermind.Consensus.ExecutionRequests;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Consensus.Rewards;
using Nethermind.Consensus.Validators;
using Nethermind.Consensus.Withdrawals;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Blockchain;
using Nethermind.Core.Test.Builders;
using Nethermind.Core.Test.Modules;
using Nethermind.Core.Utils;
using Nethermind.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Facade.Find;
using Nethermind.Init.Modules;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Serialization.Json;
using Nethermind.Serialization.Rlp;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.State;
using Nethermind.TxPool;
using BlockchainProcessorOptions = Nethermind.Consensus.Processing.BlockchainProcessor.Options;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public abstract class ArbitrumTestBlockchainBase(ChainSpec chainSpec, ArbitrumConfig arbitrumConfig) : IDisposable
{
    public const int DefaultTimeout = 10000;
    public static readonly DateTime InitialTimestamp = new(2025, 6, 2, 12, 50, 30, DateTimeKind.Utc);

    protected BlockchainContainerDependencies Dependencies = null!;
    protected AutoCancelTokenSource Cts;
    protected TestBlockchainUtil TestUtil = null!;
    protected long TestTimout = DefaultTimeout;

    public IContainer Container { get; private set; } = null!;
    public CancellationToken CancellationToken => Cts.Token;
    public ILogManager LogManager { get; protected set; } = NullLogManager.Instance;
    public ManualTimestamper Timestamper { get; protected set; } = null!;
    public EthereumJsonSerializer JsonSerializer { get; protected set; } = null!;
    public ChainSpec ChainSpec => chainSpec;

    public IWorldStateManager WorldStateManager => Dependencies.WorldStateManager;
    public IStateReader StateReader => Dependencies.StateReader;
    public IReceiptStorage ReceiptStorage => Dependencies.ReceiptStorage;

    public BlocksConfig BlocksConfig { get; protected set; } = new();
    public BuildBlocksWhenRequested BlockProductionTrigger { get; } = new();
    public ProducedBlockSuggester Suggester { get; protected set; } = null!;
    public IBlockTree BlockTree => Dependencies.BlockTree;
    public IBlockValidator BlockValidator => Dependencies.BlockValidator;
    public IBlockProducer BlockProducer { get; protected set; } = null!;
    public IBlockProducerRunner BlockProducerRunner { get; protected set; } = null!;
    public IBlockProcessor BlockProcessor { get; protected set; } = null!;
    public IBranchProcessor BranchProcessor => Dependencies.MainProcessingContext.BranchProcessor;
    public IBlockchainProcessor BlockchainProcessor { get; protected set; } = null!;
    public IBlockProcessingQueue BlockProcessingQueue { get; protected set; } = null!;

    public ITxPool TxPool => Dependencies.TxPool;
    public ArbitrumRpcTxSource ArbitrumRpcTxSource { get; protected set; } = null!;
    public IReadOnlyTxProcessingEnvFactory ReadOnlyTxProcessingEnvFactory => Dependencies.ReadOnlyTxProcessingEnvFactory;
    public ITransactionProcessor TxProcessor => Dependencies.MainProcessingContext.TransactionProcessor;
    public IExecutionRequestsProcessor MainExecutionRequestsProcessor => ((MainProcessingContext)Dependencies.MainProcessingContext)
        .LifetimeScope.Resolve<IExecutionRequestsProcessor>();
    public IMainProcessingContext MainProcessingContext => Dependencies.MainProcessingContext;

    public IBlockFinder BlockFinder => Dependencies.BlockFinder;
    public ILogFinder LogFinder => Dependencies.LogFinder;

    public CachedL1PriceData CachedL1PriceData => Dependencies.CachedL1PriceData;

    public ISpecProvider SpecProvider => Dependencies.SpecProvider;

    public class Configuration
    {
        public bool SuggestGenesisOnStart = false;
        public UInt256 L1BaseFee = 92;
        public bool FillWithTestDataOnStart = false;
    }

    public void Dispose()
    {
        try
        {
            BlockProducerRunner?.StopAsync();
        }
        catch (ObjectDisposedException)
        {
            // Ignore if CancellationTokenSource is already disposed
        }

        Container?.Dispose();
    }

    public static ArbitrumRpcTestBlockchain CreateTestBlockchainWithGenesis()
    {
        Action<ContainerBuilder> preConfigurer = (ContainerBuilder cb) =>
        {
            cb.AddScoped(new Configuration()
            {
                SuggestGenesisOnStart = true,
            });
        };

        return ArbitrumRpcTestBlockchain.CreateDefault(preConfigurer);
    }

    protected virtual ArbitrumTestBlockchainBase Build(Action<ContainerBuilder>? configurer = null)
    {
        Timestamper = new ManualTimestamper(InitialTimestamp);
        JsonSerializer = new EthereumJsonSerializer();

        IConfigProvider configProvider = new ConfigProvider(arbitrumConfig);

        ContainerBuilder builder = ConfigureContainer(new ContainerBuilder(), configProvider);
        configurer?.Invoke(builder);

        Container = builder.Build();
        Dependencies = Container.Resolve<BlockchainContainerDependencies>();

        BlockProcessor = CreateBlockProcessor(Dependencies.WorldStateManager.GlobalWorldState);

        BlockchainProcessor chainProcessor = new(
            BlockTree,
            BranchProcessor,
            Dependencies.BlockPreprocessorStep,
            StateReader,
            LogManager,
            BlockchainProcessorOptions.Default);

        BlockchainProcessor = chainProcessor;
        BlockProcessingQueue = chainProcessor;
        chainProcessor.Start();

        TransactionComparerProvider transactionComparerProvider = new(Dependencies.SpecProvider, BlockFinder);

        BlockProducer = CreateTestBlockProducer(Dependencies.Sealer, transactionComparerProvider);
        BlockProducerRunner = new StandardBlockProducerRunner(BlockProductionTrigger, BlockTree, BlockProducer);
        BlockProducerRunner.Start();

        Suggester = new ProducedBlockSuggester(BlockTree, BlockProducerRunner);

        RegisterTransactionDecoders();

        Cts = AutoCancelTokenSource.ThatCancelAfter(TimeSpan.FromMilliseconds(TestTimout));
        //TestUtil = new TestBlockchainUtil(
        //    BlockProducerRunner,
        //    BlockProductionTrigger,
        //    Timestamper,
        //    BlockTree,
        //    TxPool,
        //    1
        //);

        Configuration testConfig = Container.Resolve<Configuration>();
        IWorldState worldState = WorldStateManager.GlobalWorldState;

        Block? genesisBlock = null;

        if (testConfig.SuggestGenesisOnStart)
        {
            using var dispose = worldState.BeginScope(IWorldState.PreGenesis);
            ManualResetEvent resetEvent = new(false);
            BlockTree.OnUpdateMainChain += (sender, args) => { resetEvent.Set(); };

            DigestInitMessage digestInitMessage = FullChainSimulationInitMessage.CreateDigestInitMessage(testConfig.L1BaseFee);
            ParsedInitMessage parsedInitMessage = new(
                ChainSpec.ChainId,
                digestInitMessage.InitialL1BaseFee,
                null,
                digestInitMessage.SerializedChainConfig);

            ArbitrumGenesisLoader genesisLoader = new(
                ChainSpec,
                FullChainSimulationSpecProvider.Instance,
                Dependencies.SpecHelper,
                worldState,
                parsedInitMessage,
                LimboLogs.Instance);

            genesisBlock = genesisLoader.Load();
            BlockTree.SuggestBlock(genesisBlock);

            var genesisResult = resetEvent.WaitOne(TimeSpan.FromMilliseconds(DefaultTimeout));

            if (!genesisResult)
                throw new Exception("Failed to process Arbitrum genesis block!");
        }

        if (testConfig.FillWithTestDataOnStart)
        {
            using var dispose = worldState.BeginScope(genesisBlock?.Header ?? IWorldState.PreGenesis);

            worldState.CreateAccount(TestItem.AddressA, 100.Ether());
            worldState.CreateAccount(TestItem.AddressB, 200.Ether());
            worldState.CreateAccount(TestItem.AddressC, 300.Ether());

            worldState.Commit(SpecProvider.GenesisSpec);
            worldState.CommitTree(BlockTree.Head?.Number ?? 0 + 1);

            var parentBlockHeader = BlockTree.Head?.Header.Clone();
            if (parentBlockHeader is null)
                return this;
            parentBlockHeader.ParentHash = BlockTree.HeadHash;
            parentBlockHeader.StateRoot = worldState.StateRoot;
            parentBlockHeader.Number++;
            parentBlockHeader.Hash = parentBlockHeader.CalculateHash();
            parentBlockHeader.TotalDifficulty = (parentBlockHeader.TotalDifficulty ?? 0) + 1;
            var newBlock = BlockTree.Head!.WithReplacedHeader(parentBlockHeader);
            BlockTree.SuggestBlock(newBlock, BlockTreeSuggestOptions.ForceSetAsMain);
            BlockTree.UpdateMainChain([newBlock], true, true);
        }
        return this;
    }

    protected virtual ContainerBuilder ConfigureContainer(ContainerBuilder builder, IConfigProvider configProvider)
    {
        return builder
            .AddModule(new PseudoNethermindModule(ChainSpec, configProvider, LimboLogs.Instance))
            .AddModule(new TestEnvironmentModule(TestItem.PrivateKeyA, Random.Shared.Next().ToString()))
            .AddModule(new ArbitrumModule(ChainSpec))
            .AddSingleton<ISpecProvider>(FullChainSimulationSpecProvider.Instance)
            .AddSingleton<Configuration>()
            .AddSingleton<BlockchainContainerDependencies>()

            .AddSingleton<IBlockProducerEnvFactory, ArbitrumBlockProducerEnvFactory>()
            .AddSingleton<IBlockProducerTxSourceFactory, ArbitrumBlockProducerTxSourceFactory>()
            .AddDecorator<ICodeInfoRepository, ArbitrumCodeInfoRepository>()

            .AddScoped<ITransactionProcessor, ArbitrumTransactionProcessor>()
            .AddScoped<IBlockProcessor, ArbitrumBlockProcessor>()
            .AddScoped<IVirtualMachine, ArbitrumVirtualMachine>()
            .AddScoped<BlockProcessor.IBlockProductionTransactionPicker, ISpecProvider, IBlocksConfig>((specProvider, blocksConfig) =>
                new ArbitrumBlockProductionTransactionPicker(specProvider))

            // Some validator configurations
            .AddSingleton<ISealValidator>(Always.Valid)
            .AddSingleton<IUnclesValidator>(Always.Valid)
            .AddSingleton<ISealer>(new NethDevSealEngine(TestItem.AddressD));
    }

    protected virtual IBlockProcessor CreateBlockProcessor(IWorldState worldState)
    {
        ArbitrumBlockProductionTransactionPicker transactionPicker = new(Dependencies.SpecProvider);
        return new ArbitrumBlockProcessor(
            Dependencies.SpecProvider,
            BlockValidator,
            NoBlockRewards.Instance,
            new ArbitrumBlockProcessor.ArbitrumBlockProductionTransactionsExecutor(TxProcessor, worldState, transactionPicker, LogManager),
            TxProcessor,
            Dependencies.CachedL1PriceData,
            worldState,
            ReceiptStorage,
            new BlockhashStore(Dependencies.SpecProvider, worldState),
            new BeaconBlockRootHandler(TxProcessor, worldState),
            LogManager,
            new WithdrawalProcessor(worldState, LogManager),
            new ExecutionRequestsProcessor(TxProcessor));
    }

    protected IBlockCachePreWarmer CreateBlockCachePreWarmer()
    {
        return new BlockCachePreWarmer(
            ReadOnlyTxProcessingEnvFactory,
            WorldStateManager.GlobalWorldState,
            4,
            LogManager,
            (WorldStateManager.GlobalWorldState as IPreBlockCaches)?.Caches);
    }

    protected virtual IBlockProducer CreateTestBlockProducer(ISealer sealer, ITransactionComparerProvider comparerProvider)
    {
        IBlockProducerEnv blockProducerEnv = Dependencies.BlockProducerEnvFactory.Create();

        return new ArbitrumBlockProducer(
            blockProducerEnv.TxSource,
            blockProducerEnv.ChainProcessor,
            blockProducerEnv.BlockTree,
            blockProducerEnv.ReadOnlyStateProvider,
            new ArbitrumGasLimitCalculator(),
            NullSealEngine.Instance,
            Timestamper,
            Dependencies.SpecProvider,
            LogManager,
            BlocksConfig);
    }

    protected void RegisterTransactionDecoders()
    {
        TxDecoder.Instance.RegisterDecoder(new ArbitrumInternalTxDecoder());
        TxDecoder.Instance.RegisterDecoder(new ArbitrumSubmitRetryableTxDecoder());
        TxDecoder.Instance.RegisterDecoder(new ArbitrumRetryTxDecoder());
        TxDecoder.Instance.RegisterDecoder(new ArbitrumDepositTxDecoder());
        TxDecoder.Instance.RegisterDecoder(new ArbitrumUnsignedTxDecoder());
        TxDecoder.Instance.RegisterDecoder(new ArbitrumContractTxDecoder());
    }

    protected record BlockchainContainerDependencies(
        IStateReader StateReader,
        IReceiptStorage ReceiptStorage,
        ITxPool TxPool,
        IWorldStateManager WorldStateManager,
        IBlockPreprocessorStep BlockPreprocessorStep,
        IBlockTree BlockTree,
        IBlockFinder BlockFinder,
        ILogFinder LogFinder,
        ISpecProvider SpecProvider,
        IBlockValidator BlockValidator,
        IMainProcessingContext MainProcessingContext,
        IReadOnlyTxProcessingEnvFactory ReadOnlyTxProcessingEnvFactory,
        IBlockProducerEnvFactory BlockProducerEnvFactory,
        ISealer Sealer,
        CachedL1PriceData CachedL1PriceData,
        IArbitrumSpecHelper SpecHelper);
}
