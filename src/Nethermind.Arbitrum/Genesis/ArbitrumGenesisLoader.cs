using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Crypto;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Genesis;

public class ArbitrumGenesisLoader(
    ChainSpec chainSpec,
    ISpecProvider specProvider,
    IArbitrumSpecHelper specHelper,
    IWorldState worldState,
    ParsedInitMessage initMessage,
    ILogManager logManager,
    string? genesisStatePath = null)
{
    private readonly ILogger _logger = logManager.GetClassLogger();

    public Block Load()
    {
        ValidateInitMessage();

        // Check if block 22207817 already exists in the database (from snapshot)
        // If it does, we should not recreate genesis - just return the existing block
        // This check would need to be implemented based on your blockchain architecture

        _logger.Info("Loading Arbitrum genesis for block 22207817...");


        // Import account state FIRST (before ArbOS initialization)
        bool stateImportedFromFile = false;
        if (!string.IsNullOrEmpty(genesisStatePath) && File.Exists(genesisStatePath))
        {
            var importer = new ArbitrumGenesisStateImporter(worldState, logManager);
            importer.ImportIfNeeded(genesisStatePath, specProvider.GenesisSpec);
            _logger.Info($"Imported account state from {genesisStatePath}");
            stateImportedFromFile = true;

            // Commit the imported state immediately
            _logger.Info("Committing imported state...");
            worldState.Commit(specProvider.GenesisSpec, true);
            worldState.CommitTree(22207817);
            _logger.Info("Imported state committed successfully");
        }

        // If we imported from file, DON'T initialize ArbOS (it's already in the imported state)
        bool shouldInitializeArbos = !stateImportedFromFile;

        if (shouldInitializeArbos)
        {
            _logger.Info("Initializing ArbOS system state for fresh genesis...");
            worldState.CreateAccountIfNotExists(ArbosAddresses.ArbosSystemAccount, UInt256.Zero, UInt256.One);
            InitializeArbosState();
            worldState.Commit(specProvider.GenesisSpec, true);
            worldState.CommitTree(22207817);

            var committedStateRoot = worldState.StateRoot;
            _logger.Info($"State committed with root: {committedStateRoot}");
        }
        else
        {
            _logger.Info("State imported from file - skipping ArbOS initialization");
            var currentStateRoot = worldState.StateRoot;
            _logger.Info($"Using imported state with root: {currentStateRoot}");
        }

        // ✅ GET THE ACTUAL STATE ROOT
        Hash256 actualStateRoot = worldState.StateRoot;

        // Create genesis block from actual block 22207817 data
        BlockHeader genesisHeader = new BlockHeader(
            new Hash256("0xa903d86321a537beab1a892c387c3198a6dd75dbd4a68346b04642770d20d8fe"),
            new Hash256("0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347"),
            Address.Zero,
            UInt256.One,
            22207817,
            1125899906842624,
            1661956342,
            new byte[32]
        );

        genesisHeader.BaseFeePerGas = 100000000;
        genesisHeader.GasUsed = 0;

        // ✅ USE THE ACTUAL STATE ROOT INSTEAD OF HARDCODED
        genesisHeader.StateRoot = actualStateRoot;  // NOT hardcoded!

        genesisHeader.TxRoot = new Hash256("0x56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421");
        genesisHeader.ReceiptsRoot = new Hash256("0x56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421");
        genesisHeader.Bloom = Bloom.Empty;
        genesisHeader.MixHash = new Hash256("0x0000000000000000000000000000000000000000000000060000000000000000");
        genesisHeader.Nonce = 1;

        genesisHeader.Hash = genesisHeader.CalculateHash();

        _logger.Info($"Genesis header hash calculated: {genesisHeader.Hash}");
        _logger.Info($"Using actual state root: {actualStateRoot}");

        Block genesis = new Block(genesisHeader);

        return genesis;
    }

    private void ValidateInitMessage()
    {
        var compatibilityError = initMessage.IsCompatibleWith(chainSpec);
        if (compatibilityError != null)
        {
            throw new InvalidOperationException(
                $"Incompatible L1 init message: {compatibilityError}. " +
                $"This indicates a mismatch between the L1 initialization data and local configuration.");
        }

        if (initMessage.SerializedChainConfig != null)
        {
            string serializedConfigJson = System.Text.Encoding.UTF8.GetString(initMessage.SerializedChainConfig);
            _logger.Info($"Read serialized chain config from L1 init message: {serializedConfigJson}");
        }

        _logger.Info("L1 init message validation passed - configuration is compatible with local chainspec");
    }

    private void InitializeArbosState()
    {
        _logger.Info("Initializing ArbOS...");

        SystemBurner burner = new(readOnly: false);
        ArbosStorage rootStorage = new(worldState, burner, ArbosAddresses.ArbosSystemAccount);
        ArbosStorageBackedULong versionStorage = new(rootStorage, ArbosStateOffsets.VersionOffset);

        ulong currentPersistedVersion = versionStorage.Get();
        if (currentPersistedVersion != ArbosVersion.Zero)
        {
            throw new InvalidOperationException($"ArbOS already initialized with version {currentPersistedVersion}. Cannot re-initialize for genesis.");
        }

        var canonicalArbitrumParams = initMessage.GetCanonicalArbitrumParameters(specHelper);
        ulong desiredInitialArbosVersion = canonicalArbitrumParams.InitialArbOSVersion.Value;
        if (desiredInitialArbosVersion == ArbosVersion.Zero)
        {
            throw new InvalidOperationException("Cannot initialize to ArbOS version 0.");
        }

        if (_logger.IsDebug)
        {
            _logger.Debug($"Using canonical initial ArbOS version from L1: {desiredInitialArbosVersion}");
        }

        foreach ((Address address, ulong minVersion) in Arbos.Precompiles.PrecompileMinArbOSVersions)
        {
            if (minVersion == ArbosVersion.Zero)
            {
                worldState.CreateAccountIfNotExists(address, UInt256.Zero);
                worldState.InsertCode(address, Arbos.Precompiles.InvalidCodeHash, Arbos.Precompiles.InvalidCode, specProvider.GenesisSpec, true);
            }
        }

        versionStorage.Set(ArbosVersion.One);
        if (_logger.IsDebug)
        {
            _logger.Debug("Set ArbOS version in storage to 1.");
        }

        ArbosStorageBackedULong upgradeVersionStorage = new(rootStorage, ArbosStateOffsets.UpgradeVersionOffset);
        upgradeVersionStorage.Set(0);
        ArbosStorageBackedULong upgradeTimestampStorage = new(rootStorage, ArbosStateOffsets.UpgradeTimestampOffset);
        upgradeTimestampStorage.Set(0);

        Address canonicalChainOwner = canonicalArbitrumParams.InitialChainOwner;
        ArbosStorageBackedAddress networkFeeAccountStorage = new(rootStorage, ArbosStateOffsets.NetworkFeeAccountOffset);
        networkFeeAccountStorage.Set(desiredInitialArbosVersion >= ArbosVersion.Two ? canonicalChainOwner : Address.Zero);

        ArbosStorageBackedUInt256 chainIdStorage = new(rootStorage, ArbosStateOffsets.ChainIdOffset);
        chainIdStorage.Set(initMessage.ChainId);

        ArbosStorageBackedBytes chainConfigStorage = new(rootStorage.OpenSubStorage(ArbosSubspaceIDs.ChainConfigSubspace));
        if (initMessage.SerializedChainConfig != null)
        {
            chainConfigStorage.Set(initMessage.SerializedChainConfig);
            if (_logger.IsDebug)
            {
                _logger.Debug("Stored canonical chain config from L1 init message in ArbOS state");
            }
        }
        else
        {
            _logger.Warn("No serialized chain config provided - assuming chain config exists in imported state or will use chainspec");
        }

        ulong canonicalGenesisBlockNum = canonicalArbitrumParams.GenesisBlockNum.Value;
        ArbosStorageBackedULong genesisBlockNumStorage = new(rootStorage, ArbosStateOffsets.GenesisBlockNumOffset);
        genesisBlockNumStorage.Set(canonicalGenesisBlockNum);

        ArbosStorageBackedULong brotliLevelStorage = new(rootStorage, ArbosStateOffsets.BrotliCompressionLevelOffset);
        brotliLevelStorage.Set(0);

        ArbosStorage l1PricingStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.L1PricingSubspace);
        Address initialRewardsRecipient = desiredInitialArbosVersion >= ArbosVersion.Two ? canonicalChainOwner : ArbosAddresses.BatchPosterAddress;
        L1PricingState.Initialize(l1PricingStorage, initialRewardsRecipient, initMessage.InitialBaseFee);

        ArbosStorage l2PricingStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.L2PricingSubspace);
        L2PricingState.Initialize(l2PricingStorage);

        ArbosStorage retryableStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.RetryablesSubspace);
        RetryableState.Initialize(retryableStorage);

        ArbosStorage addressTableStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.AddressTableSubspace);
        AddressTable.Initialize(addressTableStorage);

        ArbosStorage blockhashesStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.BlockhashesSubspace);
        Blockhashes.Initialize(blockhashesStorage, logManager.GetClassLogger<Blockhashes>());

        ArbosStorage chainOwnerStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.ChainOwnerSubspace);
        AddressSet.Initialize(chainOwnerStorage);
        AddressSet chainOwners = new(chainOwnerStorage);
        chainOwners.Add(canonicalChainOwner);

        ArbosState arbosState = ArbosState.OpenArbosState(worldState, burner, logManager.GetClassLogger<ArbosState>());

        if (desiredInitialArbosVersion > ArbosVersion.One)
        {
            _logger.Info($"Upgrading ArbosState from version {arbosState.CurrentArbosVersion} to {desiredInitialArbosVersion} (first time setup)...");
            arbosState.UpgradeArbosVersion(desiredInitialArbosVersion, true, worldState, specProvider.GenesisSpec);
            _logger.Info($"ArbosState upgraded to version {arbosState.CurrentArbosVersion}.");
        }

        _logger.Info("ArbOS state initialization complete.");
    }
}
