using Autofac;
using Nethermind.Api;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
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
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Blockchain;
using Nethermind.Core.Test.Builders;
using Nethermind.Core.Test.Modules;
using Nethermind.Core.Utils;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Facade.Find;
using Nethermind.Logging;
using Nethermind.Serialization.Json;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.State;
using Nethermind.TxPool;
using BlockchainProcessorOptions = Nethermind.Consensus.Processing.BlockchainProcessor.Options;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public abstract class ArbitrumTestBlockchainBase : IDisposable
{
    public const int DefaultTimeout = 10000;
    public static readonly DateTime InitialTimestamp = new(2025, 6, 2, 12, 50, 30, DateTimeKind.Utc);

    private ChainSpec? _chainSpec = null!;

    protected BlockchainContainerDependencies Dependencies = null!;
    protected AutoCancelTokenSource Cts;
    protected TestBlockchainUtil TestUtil = null!;
    protected long TestTimout = DefaultTimeout;

    public IContainer Container { get; private set; } = null!;
    public CancellationToken CancellationToken => Cts.Token;
    public ILogManager LogManager { get; protected set; } = LimboLogs.Instance;
    public ManualTimestamper Timestamper { get; protected set; } = null!;
    public IJsonSerializer JsonSerializer { get; protected set; } = null!;
    public ChainSpec ChainSpec => _chainSpec ??= FullChainSimulationChainSpecProvider.Create();

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
    public IBlockchainProcessor BlockchainProcessor { get; protected set; } = null!;
    public IBlockProcessingQueue BlockProcessingQueue { get; protected set; } = null!;

    public ITxPool TxPool => Dependencies.TxPool;
    public ArbitrumRpcTxSource ArbitrumRpcTxSource { get; protected set; } = null!;
    public IReadOnlyTxProcessingEnvFactory ReadOnlyTxProcessingEnvFactory => Dependencies.ReadOnlyTxProcessingEnvFactory;
    public ITransactionProcessor TxProcessor => Dependencies.MainProcessingContext.TransactionProcessor;
    public IExecutionRequestsProcessor MainExecutionRequestsProcessor => ((MainBlockProcessingContext)Dependencies.MainProcessingContext)
        .LifetimeScope.Resolve<IExecutionRequestsProcessor>();

    public IBlockFinder BlockFinder => Dependencies.BlockFinder;
    public ILogFinder LogFinder => Dependencies.LogFinder;

    public ISpecProvider SpecProvider => Dependencies.SpecProvider;

    public class Configuration
    {
        public bool SuggestGenesisOnStart = true;
        public bool FillWithTestDataOnStart = true;
    }

    public void Dispose()
    {
        BlockProducerRunner?.StopAsync();
        Container.Dispose();
    }

    protected virtual ArbitrumTestBlockchainBase Build(Action<ContainerBuilder>? configurer = null)
    {
        Timestamper = new ManualTimestamper(InitialTimestamp);
        JsonSerializer = new EthereumJsonSerializer();

        IConfigProvider configProvider = new ConfigProvider([]);

        ContainerBuilder builder = ConfigureContainer(new ContainerBuilder(), configProvider);
        ConfigureContainer(builder, configProvider);
        configurer?.Invoke(builder);

        Container = builder.Build();
        Dependencies = Container.Resolve<BlockchainContainerDependencies>();

        BlockProcessor = CreateBlockProcessor(Dependencies.WorldStateManager.GlobalWorldState);
        BlockchainProcessor chainProcessor = new(
            BlockTree,
            BlockProcessor,
            Dependencies.BlockPreprocessorStep,
            StateReader,
            LogManager,
            BlockchainProcessorOptions.Default);

        BlockchainProcessor = chainProcessor;
        BlockProcessingQueue = chainProcessor;
        chainProcessor.Start();

        ArbitrumRpcTxSource arbitrumRpcTxSource = new ArbitrumRpcTxSource(LogManager);
        TransactionComparerProvider transactionComparerProvider = new(Dependencies.SpecProvider, BlockFinder);

        BlockProducer = CreateTestBlockProducer(arbitrumRpcTxSource, Dependencies.Sealer, transactionComparerProvider);
        BlockProducerRunner = new StandardBlockProducerRunner(BlockProductionTrigger, BlockTree, BlockProducer);
        BlockProducerRunner.Start();

        Suggester = new ProducedBlockSuggester(BlockTree, BlockProducerRunner);

        Cts = AutoCancelTokenSource.ThatCancelAfter(TimeSpan.FromMilliseconds(TestTimout));
        TestUtil = new TestBlockchainUtil(
            BlockProducerRunner,
            BlockProductionTrigger,
            Timestamper,
            BlockTree,
            TxPool,
            1
        );

        var testConfig = Container.Resolve<Configuration>();
        var worldState = WorldStateManager.GlobalWorldState;

        if (testConfig.SuggestGenesisOnStart)
        {
            ManualResetEvent resetEvent = new(false);
            BlockTree.NewHeadBlock += (sender, args) => { resetEvent.Set(); };

            DigestInitMessage digestInitMessage = FullChainSimulationInitMessage.CreateDigestInitMessage(92);
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

            Block genesisBlock = genesisLoader.Load();
            BlockTree.SuggestBlock(genesisBlock);

            var genesisResult = resetEvent.WaitOne(TimeSpan.FromMilliseconds(DefaultTimeout));

            if (!genesisResult)
                throw new Exception("Failed to process Arbitrum genesis block!");
        }

        if (testConfig.FillWithTestDataOnStart)
        {
            worldState.CreateAccount(TestItem.AddressA, 100);
            worldState.CreateAccount(TestItem.AddressB, 200);
            worldState.CreateAccount(TestItem.AddressC, 300);

            worldState.Commit(SpecProvider.GenesisSpec);
            worldState.CommitTree(BlockTree.Head?.Number ?? 0 + 1);
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
            new BlockProcessor.BlockProductionTransactionsExecutor(TxProcessor, worldState, transactionPicker, LogManager),
            worldState,
            ReceiptStorage,
            new BlockhashStore(Dependencies.SpecProvider, worldState),
            new BeaconBlockRootHandler(TxProcessor, worldState),
            LogManager,
            new WithdrawalProcessor(worldState, LogManager),
            new ExecutionRequestsProcessor(TxProcessor),
            preWarmer: CreateBlockCachePreWarmer());
    }

    protected IBlockCachePreWarmer CreateBlockCachePreWarmer()
    {
        return new BlockCachePreWarmer(
            ReadOnlyTxProcessingEnvFactory,
            WorldStateManager.GlobalWorldState,
            Dependencies.SpecProvider,
            4,
            LogManager,
            (WorldStateManager.GlobalWorldState as IPreBlockCaches)?.Caches);
    }

    protected virtual IBlockProducer CreateTestBlockProducer(ArbitrumRpcTxSource txSource, ISealer sealer, ITransactionComparerProvider comparerProvider)
    {
        BlockProducerEnv blockProducerEnv = Dependencies.BlockProducerEnvFactory.Create();
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
        IArbitrumSpecHelper SpecHelper
    );
}
