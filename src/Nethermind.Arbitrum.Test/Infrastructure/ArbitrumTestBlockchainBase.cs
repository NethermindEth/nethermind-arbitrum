using Autofac;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Find;
using Nethermind.Blockchain.Receipts;
using Nethermind.Config;
using Nethermind.Consensus;
using Nethermind.Consensus.ExecutionRequests;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Consensus.Validators;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Blockchain;
using Nethermind.Core.Test.Builders;
using Nethermind.Core.Test.Modules;
using Nethermind.Core.Utils;
using Nethermind.Crypto;
using Nethermind.Evm.State;
using Nethermind.Db;
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
    public IEthereumEcdsa Ecdsa => new EthereumEcdsa(ChainSpec.ChainId);

    public IWorldStateManager WorldStateManager => Dependencies.WorldStateManager;
    public IWorldState MainWorldState => MainProcessingContext.WorldState;
    public IStateReader StateReader => Dependencies.StateReader;
    public IReceiptStorage ReceiptStorage => Dependencies.ReceiptStorage;

    public BuildBlocksWhenRequested BlockProductionTrigger { get; } = new();
    public ProducedBlockSuggester Suggester { get; protected set; } = null!;
    public IBlockTree BlockTree => Dependencies.BlockTree;
    public IBlockValidator BlockValidator => Dependencies.BlockValidator;
    public IBlockProducer BlockProducer { get; protected set; } = null!;
    public IBlockProducerRunner BlockProducerRunner { get; protected set; } = null!;
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

    public IWasmDb WasmDB => Container.Resolve<IWasmDb>();

    public IDb CodeDB => Container.ResolveKeyed<IDb>("code");

    public IStylusTargetConfig StylusTargetConfig => Container.Resolve<IStylusTargetConfig>();

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
        Action<ContainerBuilder> preConfigurer = cb =>
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
        configProvider.GetConfig<IBlocksConfig>().BuildBlocksOnMainState = true;

        ContainerBuilder builder = ConfigureContainer(new ContainerBuilder(), configProvider);
        configurer?.Invoke(builder);

        Container = builder.Build();
        Dependencies = Container.Resolve<BlockchainContainerDependencies>();

        InitializeArbitrumPluginSteps(Container);

        BlockProducer = InitBlockProducer();
        BlockProducerRunner = InitBlockProducerRunner(BlockProducer);

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

        _ = new NonProcessingProducedBlockSuggester(BlockTree, BlockProducerRunner);
        BlockProducerRunner.Start();

        RegisterTransactionDecoders();

        Cts = AutoCancelTokenSource.ThatCancelAfter(TimeSpan.FromMilliseconds(TestTimout));

        Configuration testConfig = Container.Resolve<Configuration>();
        IWorldState worldState = MainWorldState;

        Block? genesisBlock = null;

        if (testConfig.SuggestGenesisOnStart)
        {
            using IDisposable dispose = worldState.BeginScope(IWorldState.PreGenesis);
            ManualResetEvent resetEvent = new(false);
            BlockTree.OnUpdateMainChain += (sender, args) => { resetEvent.Set(); };

            DigestInitMessage digestInitMessage = FullChainSimulationInitMessage.CreateDigestInitMessage(testConfig.L1BaseFee);
            ParsedInitMessage parsedInitMessage = new(
                ChainSpec.ChainId,
                digestInitMessage.InitialL1BaseFee,
                null,
                digestInitMessage.SerializedChainConfig);

            ArbitrumGenesisStateInitializer stateInitializer = new(
                ChainSpec,
                Dependencies.SpecHelper,
                LimboLogs.Instance);

            ArbitrumGenesisLoader genesisLoader = new(SpecProvider,
                worldState,
                parsedInitMessage,
                stateInitializer,
                LimboLogs.Instance);

            genesisBlock = genesisLoader.Load();
            BlockTree.SuggestBlock(genesisBlock);

            bool genesisResult = resetEvent.WaitOne(TimeSpan.FromMilliseconds(DefaultTimeout));

            if (!genesisResult)
                throw new Exception("Failed to process Arbitrum genesis block!");
        }

        if (testConfig.FillWithTestDataOnStart)
        {
            using IDisposable dispose = worldState.BeginScope(genesisBlock?.Header ?? IWorldState.PreGenesis);

            worldState.CreateAccount(TestItem.AddressA, 100.Ether());
            worldState.CreateAccount(TestItem.AddressB, 200.Ether());
            worldState.CreateAccount(TestItem.AddressC, 300.Ether());
            worldState.CreateAccount(TestItem.AddressD, 0, 0);
            byte[] byteCode = Bytes.FromHexString("0x1234567890");
            worldState.InsertCode(TestItem.AddressD, Keccak.Compute(byteCode), byteCode, SpecProvider.GenesisSpec);

            worldState.Commit(SpecProvider.GenesisSpec);
            worldState.CommitTree(BlockTree.Head?.Number ?? 0 + 1);

            BlockHeader? parentBlockHeader = BlockTree.Head?.Header.Clone();
            if (parentBlockHeader is null)
                return this;
            parentBlockHeader.ParentHash = BlockTree.HeadHash;
            parentBlockHeader.StateRoot = worldState.StateRoot;
            parentBlockHeader.Number++;
            parentBlockHeader.Hash = parentBlockHeader.CalculateHash();
            parentBlockHeader.TotalDifficulty = (parentBlockHeader.TotalDifficulty ?? 0) + 1;
            Block newBlock = BlockTree.Head!.WithReplacedHeader(parentBlockHeader);
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
            .AddModule(new ArbitrumModule(ChainSpec, configProvider.GetConfig<IBlocksConfig>()))
            .AddSingleton<IDbFactory>(new MemDbFactory())
            .AddSingleton<Configuration>()
            .AddSingleton<BlockchainContainerDependencies>()
            .AddSingleton<ISealValidator>(Always.Valid)
            .AddSingleton<IUnclesValidator>(Always.Valid)
            .AddSingleton<ISealer>(new NethDevSealEngine(TestItem.AddressD))
            .AddSingleton<ArbitrumInitializeStylusNative>()
            .AddSingleton<ArbitrumInitializeWasmDb>();
    }

    public void RebuildWasmStore(Hash256? startPosition = null, CancellationToken cancellationToken = default)
    {
        Block? latestBlock = BlockTree.Head;

        using (MainWorldState.BeginScope(latestBlock?.Header))
        {
            ulong latestBlockTime = latestBlock?.Timestamp ?? 0;
            Block? startBlock = startPosition != null
                ? BlockTree.FindBlock(startPosition)
                : null;
            ulong rebuildStartBlockTime = startBlock?.Timestamp ?? latestBlockTime;

            try
            {
                ArbosState arbosState = ArbosState.OpenArbosState(
                    MainWorldState,
                    new SystemBurner(),
                    LogManager.GetClassLogger());

                StylusPrograms programs = arbosState.Programs;

                WasmStoreRebuilder rebuilder = new(
                    WasmDB,
                    StylusTargetConfig,
                    programs,
                    LogManager.GetClassLogger());

                rebuilder.RebuildWasmStore(
                    CodeDB,
                    startPosition ?? Keccak.Zero,
                    latestBlockTime,
                    rebuildStartBlockTime,
                    debugMode: false,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to rebuild WASM store: {ex.Message}", ex);
            }
        }
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

    private void InitializeArbitrumPluginSteps(IContainer container)
    {
        container.Resolve<ArbitrumInitializeStylusNative>()
            .Execute(CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        container.Resolve<ArbitrumInitializeWasmDb>()
            .Execute(CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    private IBlockProducer InitBlockProducer()
    {
        IBlockProducerEnvFactory blockProducerEnvFactory = Container.Resolve<IBlockProducerEnvFactory>();
        IBlockProducerEnv producerEnv = blockProducerEnvFactory.Create();

        return new ArbitrumBlockProducer(
            producerEnv.TxSource,
            producerEnv.ChainProcessor,
            producerEnv.BlockTree,
            producerEnv.ReadOnlyStateProvider,
            new ArbitrumGasPolicyLimitCalculator(),
            NullSealEngine.Instance,
            Timestamper,
            SpecProvider,
            LogManager,
            Container.Resolve<IBlocksConfig>());
    }

    private IBlockProducerRunner InitBlockProducerRunner(IBlockProducer blockProducer)
    {
        return new StandardBlockProducerRunner(BlockProductionTrigger, BlockTree, blockProducer);
    }

    private void InitTxTypesAndRlpDecoders()
    {
        TxDecoder.Instance.RegisterDecoder(new ArbitrumInternalTxDecoder());
        TxDecoder.Instance.RegisterDecoder(new ArbitrumSubmitRetryableTxDecoder());
        TxDecoder.Instance.RegisterDecoder(new ArbitrumRetryTxDecoder());
        TxDecoder.Instance.RegisterDecoder(new ArbitrumDepositTxDecoder());
        TxDecoder.Instance.RegisterDecoder(new ArbitrumUnsignedTxDecoder());
        TxDecoder.Instance.RegisterDecoder(new ArbitrumContractTxDecoder());
    }

    private void RegisterTransactionDecoders() => InitTxTypesAndRlpDecoders();
}
