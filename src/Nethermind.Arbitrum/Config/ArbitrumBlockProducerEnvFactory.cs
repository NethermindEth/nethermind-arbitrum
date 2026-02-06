using Autofac;
using Google.Protobuf.WellKnownTypes;
using Nethermind.Arbitrum.Execution;
using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Consensus;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Collections;
using Nethermind.Core.Crypto;
using Nethermind.Core.Eip2930;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Evm.Tracing.State;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Trie;
using YamlDotNet.Core.Tokens;
using static Nethermind.Arbitrum.Execution.ArbitrumBlockProcessor;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumBlockProducerEnvFactory : BlockProducerEnvFactory
{
    public ArbitrumBlockProducerEnvFactory(
        ILifetimeScope rootLifetime,
        IWorldStateManager worldStateManager,
        IBlockProducerTxSourceFactory txSourceFactory) : base(rootLifetime, worldStateManager, txSourceFactory)
    {
    }

    protected override ContainerBuilder ConfigureBuilder(ContainerBuilder builder)
    {
        return base.ConfigureBuilder(builder)
            .AddScoped<IBlockProcessor.IBlockTransactionsExecutor, ArbitrumBlockProductionTransactionsExecutor>();
    }
}

public class ArbitrumGlobalWorldStateBlockProducerEnvFactory : GlobalWorldStateBlockProducerEnvFactory
{
    private readonly IBlocksConfig _blocksConfig;
    private readonly IArbitrumConfig _arbitrumConfig;

    public ArbitrumGlobalWorldStateBlockProducerEnvFactory(
        ILifetimeScope rootLifetime,
        IWorldStateManager worldStateManager,
        IBlockProducerTxSourceFactory txSourceFactory,
        IBlocksConfig blocksConfig,
        IArbitrumConfig arbitrumConfig) : base(rootLifetime, worldStateManager, txSourceFactory)
    {
        _blocksConfig = blocksConfig;
        _arbitrumConfig = arbitrumConfig;
    }

    protected override ContainerBuilder ConfigureBuilder(ContainerBuilder builder)
    {
        ContainerBuilder baseBuilder = base.ConfigureBuilder(builder)
            .AddScoped<IBlockProcessor.IBlockTransactionsExecutor, ArbitrumBlockProductionTransactionsExecutor>();

        if (_arbitrumConfig.DigestMessagePrefetchEnabled)
        {
            return baseBuilder
                .AddSingleton<NodeStorageCache>()
                // Singleton so that all child env share the same caches. Note: this module is applied per-processing
                // module, so singleton here is like scoped but exclude inner prewarmer lifetime.
                .AddSingleton<DoublePreBlockCaches>()
                .AddScoped<IBlockCachePreWarmer, IPrewarmerEnvFactory, NodeStorageCache, DoublePreBlockCaches, ILogManager>((envFactory, nodeStorage,
                    blockCaches, logManager) =>
                {
                    return new BlockCachePreWarmer(envFactory, _blocksConfig, nodeStorage, blockCaches, logManager);
                })
                .Add<IPrewarmerEnvFactory, ArbitrumPrewarmerEnvFactory>()

                // These are the actual decorated component that provide cached result
                .AddDecorator<IWorldStateScopeProvider>((ctx, worldStateScopeProvider) =>
                {
                    if (worldStateScopeProvider is ArbitrumPrewarmerScopeProvider)
                        return worldStateScopeProvider; // Inner world state

                    DoublePreBlockCaches doubleCaches = ctx.Resolve<DoublePreBlockCaches>();

                    return new ArbitrumPrewarmerScopeProvider(
                        worldStateScopeProvider,
                        doubleCaches,
                        populatePreBlockCache: false,
                        ctx.Resolve<ILogManager>());
                })
                .AddDecorator<ICodeInfoRepository>((ctx, originalCodeInfoRepository) =>
                {
                    IBlocksConfig blocksConfig = ctx.Resolve<IBlocksConfig>();
                    DoublePreBlockCaches doubleCaches = ctx.Resolve<DoublePreBlockCaches>();
                    PreBlockCaches preBlockCaches = doubleCaches.Front;

                    IPrecompileProvider precompileProvider = ctx.Resolve<IPrecompileProvider>();
                    // Note: The use of FrozenDictionary means that this cannot be used for other processing env also due to risk of memory leak.
                    return new CachedCodeInfoRepository(precompileProvider, originalCodeInfoRepository,
                        blocksConfig.CachePrecompilesOnBlockProcessing ? preBlockCaches?.PrecompileCache : null);
                })
                .AddSingleton<PrefetchManager>()
                .AddDecorator<IWorldState, PrefetchAwareWorldState>();
        }

        return baseBuilder;
    }
}

public class PrefetchAwareWorldState : IWorldState
{
    private readonly IWorldState _baseWorldState;
    private readonly PrefetchManager _prefetchManager;

    public PrefetchAwareWorldState(IWorldState baseWorldState, PrefetchManager prefetchManager)
    {
        _baseWorldState = baseWorldState;
        _prefetchManager = prefetchManager;
    }

    public void Restore(Snapshot snapshot)
    {
        _baseWorldState.Restore(snapshot);
    }

    public bool TryGetAccount(Address address, out AccountStruct account)
    {
        return _baseWorldState.TryGetAccount(address, out account);
    }

    public ref readonly UInt256 GetBalance(Address address)
    {
        return ref _baseWorldState.GetBalance(address);
    }

    public ref readonly ValueHash256 GetCodeHash(Address address)
    {
        return ref _baseWorldState.GetCodeHash(address);
    }

    public bool HasStateForBlock(BlockHeader? baseBlock)
    {
        return _baseWorldState.HasStateForBlock(baseBlock);
    }

    public byte[] GetOriginal(in StorageCell storageCell)
    {
        return _baseWorldState.GetOriginal(in storageCell);
    }

    public ReadOnlySpan<byte> Get(in StorageCell storageCell)
    {
        return _baseWorldState.Get(in storageCell);
    }

    public void Set(in StorageCell storageCell, byte[] newValue)
    {
        _baseWorldState.Set(in storageCell, newValue);
    }

    public ReadOnlySpan<byte> GetTransientState(in StorageCell storageCell)
    {
        return _baseWorldState.GetTransientState(in storageCell);
    }

    public void SetTransientState(in StorageCell storageCell, byte[] newValue)
    {
        _baseWorldState.SetTransientState(in storageCell, newValue);
    }

    public void Reset(bool resetBlockChanges = true)
    {
        _baseWorldState.Reset(resetBlockChanges);
    }

    public Snapshot TakeSnapshot(bool newTransactionStart = false)
    {
        return _baseWorldState.TakeSnapshot(newTransactionStart);
    }

    public void WarmUp(AccessList? accessList)
    {
        _baseWorldState.WarmUp(accessList);
    }

    public void WarmUp(Address address)
    {
        _baseWorldState.WarmUp(address);
    }

    public void ClearStorage(Address address)
    {
        _baseWorldState.ClearStorage(address);
    }

    public void RecalculateStateRoot()
    {
        _baseWorldState.RecalculateStateRoot();
    }

    public void DeleteAccount(Address address)
    {
        _baseWorldState.DeleteAccount(address);
    }

    public void CreateAccount(Address address, in UInt256 balance, in UInt256 nonce = default)
    {
        _baseWorldState.CreateAccount(address, in balance, in nonce);
    }

    public void CreateAccountIfNotExists(Address address, in UInt256 balance, in UInt256 nonce = default)
    {
        _baseWorldState.CreateAccountIfNotExists(address, in balance, in nonce);
    }

    public void CreateEmptyAccountIfDeleted(Address address)
    {
        _baseWorldState.CreateEmptyAccountIfDeleted(address);
    }

    public bool InsertCode(Address address, in ValueHash256 codeHash, ReadOnlyMemory<byte> code, IReleaseSpec spec, bool isGenesis = false)
    {
        return _baseWorldState.InsertCode(address, in codeHash, code, spec, isGenesis);
    }

    public void AddToBalance(Address address, in UInt256 balanceChange, IReleaseSpec spec)
    {
        _baseWorldState.AddToBalance(address, in balanceChange, spec);
    }

    public bool AddToBalanceAndCreateIfNotExists(Address address, in UInt256 balanceChange, IReleaseSpec spec)
    {
        return _baseWorldState.AddToBalanceAndCreateIfNotExists(address, in balanceChange, spec);
    }

    public void SubtractFromBalance(Address address, in UInt256 balanceChange, IReleaseSpec spec)
    {
        _baseWorldState.SubtractFromBalance(address, in balanceChange, spec);
    }

    public void IncrementNonce(Address address, UInt256 delta)
    {
        _baseWorldState.IncrementNonce(address, delta);
    }

    public void DecrementNonce(Address address, UInt256 delta)
    {
        _baseWorldState.DecrementNonce(address, delta);
    }

    public void SetNonce(Address address, in UInt256 nonce)
    {
        _baseWorldState.SetNonce(address, in nonce);
    }

    public void Commit(IReleaseSpec releaseSpec, IWorldStateTracer tracer, bool isGenesis = false, bool commitRoots = true)
    {
        if (commitRoots)
            _prefetchManager.Cancel();
        _baseWorldState.Commit(releaseSpec, tracer, isGenesis, commitRoots);
    }

    public void CommitTree(long blockNumber)
    {
        _baseWorldState.CommitTree(blockNumber);
    }

    public ArrayPoolList<AddressAsKey>? GetAccountChanges()
    {
        return _baseWorldState.GetAccountChanges();
    }

    public void ResetTransient()
    {
        _baseWorldState.ResetTransient();
    }

    public Hash256 StateRoot => _baseWorldState.StateRoot;

    public byte[]? GetCode(Address address)
    {
        return _baseWorldState.GetCode(address);
    }

    public byte[]? GetCode(in ValueHash256 codeHash)
    {
        return _baseWorldState.GetCode(in codeHash);
    }

    public bool IsContract(Address address)
    {
        return _baseWorldState.IsContract(address);
    }

    public bool AccountExists(Address address)
    {
        return _baseWorldState.AccountExists(address);
    }

    public bool IsDeadAccount(Address address)
    {
        return _baseWorldState.IsDeadAccount(address);
    }

    public IDisposable BeginScope(BlockHeader? baseBlock)
    {
        return _baseWorldState.BeginScope(baseBlock);
    }

    public bool IsInScope => _baseWorldState.IsInScope;

    public IWorldStateScopeProvider ScopeProvider => _baseWorldState.ScopeProvider;
}
