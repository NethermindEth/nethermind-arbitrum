using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Crypto;
using Nethermind.Db;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.State;
using Nethermind.State.SnapServer;
using Nethermind.Trie.Pruning;

namespace Nethermind.Arbitrum.Genesis;

public class ArbitrumGenesisLoader
{
    private readonly ChainSpec _chainSpec;
    private readonly ISpecProvider _specProvider;
    private readonly IArbitrumSpecHelper _specHelper;
    private readonly IWorldState _worldState;
    private readonly ParsedInitMessage _initMessage;
    private readonly ILogManager _logManager;
    private readonly ILogger _logger;
    private readonly INodeStorage _nodeStorage;
    private readonly ISnapServer _snapServer;
    private readonly IDb? _codeDb;
    private readonly IStateReader _stateReader;
    private readonly IWorldStateManager _worldStateManager;
    private readonly string? _genesisStatePath;

    public ArbitrumGenesisLoader(
        ChainSpec chainSpec,
        ISpecProvider specProvider,
        IArbitrumSpecHelper specHelper,
        IWorldState worldState,
        ParsedInitMessage initMessage,
        ILogManager logManager,
        INodeStorage nodeStorage,
        ISnapServer? snapServer,  // Make this nullable
        IDb codeDb,
        IStateReader stateReader,
        IWorldStateManager worldStateManager,
        string? genesisStatePath = null)
    {
        _chainSpec = chainSpec;
        _specProvider = specProvider;
        _specHelper = specHelper;
        _worldState = worldState;
        _initMessage = initMessage;
        _logManager = logManager;
        _logger = logManager.GetClassLogger();
        _nodeStorage = nodeStorage;
        _snapServer = snapServer;
        _codeDb = codeDb;
        _stateReader = stateReader;
        _genesisStatePath = genesisStatePath;
    }

    public Block Load()
    {
        ValidateInitMessage();

        _logger.Info("Loading Arbitrum genesis for block 22207817...");

        // Wrap ALL WorldState operations in a scope
        using (_worldState.BeginScope(IWorldState.PreGenesis))
        {
            bool stateImportedFromFile = false;
            if (!string.IsNullOrEmpty(_genesisStatePath) && File.Exists(_genesisStatePath))
            {
                var importer = new ArbitrumGenesisStateImporter(_worldState, _nodeStorage, _codeDb, _logManager);
                importer.ImportIfNeeded(_genesisStatePath);
                _logger.Info($"Imported account state from {_genesisStatePath}");
                stateImportedFromFile = true;

                _worldState.Commit(_specProvider.GenesisSpec, true);
                _worldState.RecalculateStateRoot();

                var stateRootAfterCommit = _worldState.StateRoot;
                _logger.Info($"StateRoot after commit: {stateRootAfterCommit}");
            }

            // ALWAYS initialize ArbOS storage, even when importing from file
            _logger.Info("Initializing ArbOS system storage structures...");
            _worldState.CreateAccountIfNotExists(ArbosAddresses.ArbosSystemAccount, UInt256.Zero, UInt256.One);

            SystemBurner burner = new(readOnly: false);
            ArbosStorage rootStorage = new(_worldState, burner, ArbosAddresses.ArbosSystemAccount);

            // Initialize ChainConfigStorage (CRITICAL for block processing)
            ArbosStorageBackedBytes chainConfigStorage = new(rootStorage.OpenSubStorage(ArbosSubspaceIDs.ChainConfigSubspace));
            if (_initMessage.SerializedChainConfig != null)
            {
                byte[] existingConfig = chainConfigStorage.Get();
                if (existingConfig == null || existingConfig.Length == 0)
                {
                    chainConfigStorage.Set(_initMessage.SerializedChainConfig);
                    _logger.Info("ChainConfigStorage initialized from init message");

                    byte[] verifyRead = chainConfigStorage.Get();
                    _logger.Info($"ChainConfigStorage verification - readable: {verifyRead?.Length ?? 0} bytes");
                }
            }

            if (!stateImportedFromFile)
            {
                InitializeArbosState();
                Preallocate();
            }
            else
            {
                _logger.Info("Ensuring minimal ArbOS structures for imported state...");
                EnsureMinimalArbosStructures(rootStorage, burner);
            }

            _worldState.Commit(_specProvider.GenesisSpec, true);
            _worldState.CommitTree(22207817);

            Hash256 actualStateRoot = _worldState.StateRoot;

            // Create genesis block
            BlockHeader genesisHeader = new BlockHeader(
                new Hash256("0xa903d86321a537beab1a892c387c3198a6dd75dbd4a68346b04642770d20d8fe"),
                new Hash256("0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347"),
                Address.Zero,
                UInt256.One,
                22207817,
                1125899906842624,
                1661956342,
                Bytes.FromHexString("0x0000000000000000000000000000000000000000000000000000000000000000")
            );

            genesisHeader.BaseFeePerGas = 100000000;
            genesisHeader.GasUsed = 0;
            genesisHeader.StateRoot = actualStateRoot;
            genesisHeader.TxRoot = new Hash256("0x56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421");
            genesisHeader.ReceiptsRoot = new Hash256("0x56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421");
            genesisHeader.Bloom = Bloom.Empty;
            genesisHeader.MixHash = new Hash256("0x0000000000000000000000000000000000000000000000060000000000000000");
            genesisHeader.Nonce = 1;

            genesisHeader.Hash = genesisHeader.CalculateHash();

            _logger.Info($"Genesis block created with hash: {genesisHeader.Hash}");
            _logger.Info($"Final state root: {actualStateRoot}");

            return new Block(genesisHeader);
        } // Scope ends here
    }

    private void ValidateInitMessage()
    {
        var compatibilityError = _initMessage.IsCompatibleWith(_chainSpec);
        if (compatibilityError != null)
            throw new InvalidOperationException(
                $"Incompatible L1 init message: {compatibilityError}. " +
                $"This indicates a mismatch between the L1 initialization data and local configuration.");

        if (_initMessage.SerializedChainConfig != null)
        {
            string serializedConfigJson = System.Text.Encoding.UTF8.GetString(_initMessage.SerializedChainConfig);
            _logger.Info($"Read serialized chain config from L1 init message: {serializedConfigJson}");
        }

        _logger.Info("L1 init message validation passed - configuration is compatible with local chainspec");
    }

    private void InitializeArbosState()
    {
        _logger.Info("Initializing ArbOS...");

        SystemBurner burner = new(readOnly: false);
        ArbosStorage rootStorage = new(_worldState, burner, ArbosAddresses.ArbosSystemAccount);
        ArbosStorageBackedULong versionStorage = new(rootStorage, ArbosStateOffsets.VersionOffset);

        ulong currentPersistedVersion = versionStorage.Get();
        if (currentPersistedVersion != ArbosVersion.Zero)
            throw new InvalidOperationException($"ArbOS already initialized with version {currentPersistedVersion}. Cannot re-initialize for genesis.");

        ArbitrumChainSpecEngineParameters canonicalArbitrumParams = _initMessage.GetCanonicalArbitrumParameters(_specHelper);
        ulong desiredInitialArbosVersion = canonicalArbitrumParams.InitialArbOSVersion.Value;
        if (desiredInitialArbosVersion == ArbosVersion.Zero)
            throw new InvalidOperationException("Cannot initialize to ArbOS version 0.");

        if (_logger.IsDebug)
            _logger.Debug($"Using canonical initial ArbOS version from L1: {desiredInitialArbosVersion}");

        foreach ((Address address, ulong minVersion) in Arbos.Precompiles.PrecompileMinArbOSVersions)
        {
            if (minVersion == ArbosVersion.Zero)
            {
                _worldState.CreateAccountIfNotExists(address, UInt256.Zero);
                _worldState.InsertCode(address, Arbos.Precompiles.InvalidCodeHash, Arbos.Precompiles.InvalidCode, _specProvider.GenesisSpec, true);
            }
        }

        versionStorage.Set(ArbosVersion.One);
        if (_logger.IsDebug)
            _logger.Debug("Set ArbOS version in storage to 1.");

        ArbosStorageBackedULong upgradeVersionStorage = new(rootStorage, ArbosStateOffsets.UpgradeVersionOffset);
        upgradeVersionStorage.Set(0);
        ArbosStorageBackedULong upgradeTimestampStorage = new(rootStorage, ArbosStateOffsets.UpgradeTimestampOffset);
        upgradeTimestampStorage.Set(0);

        Address canonicalChainOwner = canonicalArbitrumParams.InitialChainOwner;
        ArbosStorageBackedAddress networkFeeAccountStorage = new(rootStorage, ArbosStateOffsets.NetworkFeeAccountOffset);
        networkFeeAccountStorage.Set(desiredInitialArbosVersion >= ArbosVersion.Two ? canonicalChainOwner : Address.Zero);

        ArbosStorageBackedUInt256 chainIdStorage = new(rootStorage, ArbosStateOffsets.ChainIdOffset);
        chainIdStorage.Set(_initMessage.ChainId);

        ArbosStorageBackedBytes chainConfigStorage = new(rootStorage.OpenSubStorage(ArbosSubspaceIDs.ChainConfigSubspace));
        if (_initMessage.SerializedChainConfig != null)
        {
            chainConfigStorage.Set(_initMessage.SerializedChainConfig);
            if (_logger.IsDebug)
                _logger.Debug("Stored canonical chain config from L1 init message in ArbOS state");
        }
        else
            throw new InvalidOperationException("Cannot initialize ArbOS without serialized chain config from L1 init message");

        ulong canonicalGenesisBlockNum = canonicalArbitrumParams.GenesisBlockNum.Value;
        ArbosStorageBackedULong genesisBlockNumStorage = new(rootStorage, ArbosStateOffsets.GenesisBlockNumOffset);
        genesisBlockNumStorage.Set(canonicalGenesisBlockNum);

        ArbosStorageBackedULong brotliLevelStorage = new(rootStorage, ArbosStateOffsets.BrotliCompressionLevelOffset);
        brotliLevelStorage.Set(0);

        ArbosStorage l1PricingStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.L1PricingSubspace);
        Address initialRewardsRecipient = desiredInitialArbosVersion >= ArbosVersion.Two ? canonicalChainOwner : ArbosAddresses.BatchPosterAddress;
        L1PricingState.Initialize(l1PricingStorage, initialRewardsRecipient, _initMessage.InitialBaseFee);

        ArbosStorage l2PricingStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.L2PricingSubspace);
        L2PricingState.Initialize(l2PricingStorage);

        ArbosStorage retryableStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.RetryablesSubspace);
        RetryableState.Initialize(retryableStorage);

        ArbosStorage addressTableStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.AddressTableSubspace);
        AddressTable.Initialize(addressTableStorage);

        ArbosStorage blockhashesStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.BlockhashesSubspace);
        Blockhashes.Initialize(blockhashesStorage, _logManager.GetClassLogger<Blockhashes>());

        ArbosStorage chainOwnerStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.ChainOwnerSubspace);
        AddressSet.Initialize(chainOwnerStorage);
        AddressSet chainOwners = new(chainOwnerStorage);
        chainOwners.Add(canonicalChainOwner);

        ArbosState arbosState = ArbosState.OpenArbosState(_worldState, burner, _logManager.GetClassLogger<ArbosState>());

        if (desiredInitialArbosVersion > ArbosVersion.One)
        {
            _logger.Info($"Upgrading ArbosState from version {arbosState.CurrentArbosVersion} to {desiredInitialArbosVersion} (first time setup)...");
            arbosState.UpgradeArbosVersion(desiredInitialArbosVersion, true, _worldState, _specProvider.GenesisSpec);
            _logger.Info($"ArbosState upgraded to version {arbosState.CurrentArbosVersion}.");
        }

        _logger.Info("ArbOS state initialization complete.");
    }

    private void Preallocate()
    {
        if (_chainSpec.Allocations is null || !ShouldApplyAllocations(_chainSpec.Allocations))
            return;

        foreach ((Address address, ChainSpecAllocation allocation) in _chainSpec.Allocations)
        {
            _worldState.CreateAccountIfNotExists(address, allocation.Balance, allocation.Nonce);

            if (allocation.Code is not null)
            {
                Hash256 codeHash = Keccak.Compute(allocation.Code);
                _worldState.InsertCode(address, codeHash, allocation.Code, _specProvider.GenesisSpec, isGenesis: true);
            }

            if (allocation.Constructor is not null)
                _logger.Warn($"Genesis allocation for {address} has Constructor field, which is not supported in Arbitrum genesis.");

            if (allocation.Storage is not null)
            {
                foreach ((UInt256 index, byte[] value) in allocation.Storage)
                    _worldState.Set(new StorageCell(address, index), value);
            }

            if (_logger.IsDebug)
                _logger.Debug($"Applied genesis allocation: {address} with balance {allocation.Balance}");
        }

        _logger.Info($"Applied {_chainSpec.Allocations.Count()} genesis account allocations");
    }

    private static bool ShouldApplyAllocations(IDictionary<Address, ChainSpecAllocation> allocations)
    {
        if (allocations.Count > 1)
            return true;

        if (allocations.Count == 1)
        {
            ChainSpecAllocation allocation = allocations.Values.First();
            return allocation.Balance > UInt256.One;
        }

        return false;
    }

    private void EnsureMinimalArbosStructures(ArbosStorage rootStorage, SystemBurner burner)
    {
        // When importing state, we assume most data exists, but ensure critical runtime structures

        // Check if version is set
        ArbosStorageBackedULong versionStorage = new(rootStorage, ArbosStateOffsets.VersionOffset);
        ulong currentVersion = versionStorage.Get();
        if (currentVersion == ArbosVersion.Zero)
        {
            // Set to expected version from init message
            var canonicalArbitrumParams = _initMessage.GetCanonicalArbitrumParameters(_specHelper);
            ulong desiredVersion = canonicalArbitrumParams.InitialArbOSVersion.Value;
            versionStorage.Set(desiredVersion);
            _logger.Info($"Set ArbOS version to {desiredVersion}");
        }
        else
        {
            _logger.Info($"ArbOS version already set to {currentVersion}");
        }

        // Ensure ChainId is set
        ArbosStorageBackedUInt256 chainIdStorage = new(rootStorage, ArbosStateOffsets.ChainIdOffset);
        UInt256 existingChainId = chainIdStorage.Get();
        if (existingChainId.IsZero)
        {
            chainIdStorage.Set(_initMessage.ChainId);
            _logger.Info($"Set ChainId to {_initMessage.ChainId}");
        }

        // Ensure GenesisBlockNum is set
        ArbosStorageBackedULong genesisBlockNumStorage = new(rootStorage, ArbosStateOffsets.GenesisBlockNumOffset);
        ulong existingGenesisBlockNum = genesisBlockNumStorage.Get();
        if (existingGenesisBlockNum == 0)
        {
            var canonicalArbitrumParams = _initMessage.GetCanonicalArbitrumParameters(_specHelper);
            genesisBlockNumStorage.Set(canonicalArbitrumParams.GenesisBlockNum.Value);
            _logger.Info($"Set GenesisBlockNum to {canonicalArbitrumParams.GenesisBlockNum.Value}");
        }

        // Just opening it ensures it exists

        _logger.Info("Minimal ArbOS structures verified/initialized");
    }
}
