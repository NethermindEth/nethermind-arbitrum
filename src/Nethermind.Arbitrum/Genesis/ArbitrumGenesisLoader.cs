using Nethermind.Api;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Crypto;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.State;
using Nethermind.Logging;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Genesis;

public class ArbitrumGenesisLoader(INethermindApi api) : IGenesisLoader
{
    private readonly INethermindApi _api = api ?? throw new ArgumentNullException(nameof(api));
    private readonly ILogger _logger = api.LogManager.GetClassLogger();

    public Block Load()
    {
        ArgumentNullException.ThrowIfNull(_api.SpecProvider);
        ArgumentNullException.ThrowIfNull(_api.MainProcessingContext);

        var chainSpec = _api.ChainSpec;
        var specProvider = _api.SpecProvider;
        var worldState = _api.MainProcessingContext.WorldState;
        var arbitrumConfig = new ArbitrumConfig
        {
            GenesisBlockNum = 0,
            InitialChainOwner = "0x5E1497dD1f08C87b2d8FE23e9AAB6c1De833D927",
            Enabled = true,
            InitialArbOSVersion = 32
        };

        _logger.Info("Starting Arbitrum genesis loading process...");

        // 1. Create a burner instance
        var burner = new SystemBurner(_api.LogManager, readOnly: false);

        // 2. Create ParsedInitMessage (this will need to be populated from chainSpec or config)
        var parsedInitMessage = new ParsedInitMessage
        {
            // Example: Populate from IArbitrumConfig or ChainSpec extensions if available
            // SerializedChainConfig = ... get from chainSpec or a file ...
            // InitialL1BaseFee = ... get from chainSpec or config ...
            SerializedChainConfig = Convert.FromHexString("7b22636861696e4964223a3431323334362c22686f6d657374656164426c6f636b223a302c2264616f466f726b537570706f7274223a747275652c22656970313530426c6f636b223a302c2265697031353048617368223a22307830303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030222c22656970313535426c6f636b223a302c22656970313538426c6f636b223a302c2262797a616e7469756d426c6f636b223a302c22636f6e7374616e74696e6f706c65426c6f636b223a302c2270657465727362757267426c6f636b223a302c22697374616e62756c426c6f636b223a302c226d756972476c6163696572426c6f636b223a302c226265726c696e426c6f636b223a302c226c6f6e646f6e426c6f636b223a302c22636c69717565223a7b22706572696f64223a302c2265706f6368223a307d2c22617262697472756d223a7b22456e61626c654172624f53223a747275652c22416c6c6f774465627567507265636f6d70696c6573223a747275652c2244617461417661696c6162696c697479436f6d6d6974746565223a66616c73652c22496e697469616c4172624f5356657273696f6e223a33322c22496e697469616c436861696e4f776e6572223a22307835453134393764443166303843383762326438464532336539414142366331446538333344393237222c2247656e65736973426c6f636b4e756d223a307d7d"), // Placeholder
            InitialL1BaseFee = 92
        };
        _logger.Info($"ParsedInitMessage: InitialL1BaseFee = {parsedInitMessage.InitialL1BaseFee}");


        // 3. Call InitializeArbosStateAsync
        // This is an async method, Load is synchronous. This needs careful refactoring.
        // For now, let's assume we can block for genesis or refactor Load to be async.
        // Or, InitializeArbosStateAsync is called before Load, and its result (state root) is passed to Load.
        // For this step, I'm just adding the method. The integration into Load() is a larger task.

        // ArbosState arbosState = InitializeArbosStateAsync(worldState, burner, arbitrumConfig, chainSpec, parsedInitMessage)
        //     .GetAwaiter().GetResult(); // Blocking for now, not ideal for production flow.

        // worldState.Commit() and CommitTree() would happen after InitializeArbosStateAsync populates the state.
        // The StateRoot for the genesis block header would come from the committed worldState *after* ArbOS init.

        Block genesis = chainSpec.Genesis; // This is the ETH genesis block structure


        // Ensure the ArbOS system account is created.
        // The nonce and balance might be set during InitializeArbosStateAsync.
        if (!worldState.TryGetAccount(ArbosAddresses.ArbosSystemAccount, out var account))
        {
            worldState.CreateAccount(ArbosAddresses.ArbosSystemAccount, UInt256.Zero, UInt256.One);
            _logger.Info($"Preallocated ArbOS system account: {ArbosAddresses.ArbosSystemAccount}");
        }
        else
        {
            _logger.Info($"ArbosOS system account {ArbosAddresses.ArbosSystemAccount} already exists.");
        }

        InitializeArbosStateAsync(worldState, burner, arbitrumConfig, chainSpec, parsedInitMessage).GetAwaiter().GetResult();


        // The actual ArbOS state initialization would modify worldState here.
        // For now, we are just setting up the method.
        // After InitializeArbosStateAsync and subsequent commits:
        // genesis.Header.StateRoot = worldState.StateRoot;

        worldState.Commit(specProvider.GenesisSpec, true); // Initial commit if state is empty
        worldState.CommitTree(0);
        _logger.Info($"Initial world state root after empty commit: {worldState.StateRoot}");

        genesis.Header.StateRoot = worldState.StateRoot;
        genesis.Header.Hash = genesis.Header.CalculateHash();

        _logger.Info($"Arbitrum Genesis Block Loaded: Number={genesis.Header.Number}, Hash={genesis.Header.Hash}, StateRoot={genesis.Header.StateRoot}");

        return genesis;
    }

    public async Task<ArbosState?> InitializeArbosStateAsync(
        IWorldState worldState,
        IBurner burner,
        IArbitrumConfig arbitrumConfig,
        ChainSpec chainSpec,
        ParsedInitMessage initMessage)
    {
        _logger.Info("InitializeArbosStateAsync: Starting ArbOS state initialization...");

        var rootStorage = new ArbosStorage(worldState, burner, ArbosAddresses.ArbosSystemAccount);
        var versionStorage = new ArbosStorageBackedUint64(rootStorage, ArbosConstants.ArbosStateOffsets.VersionOffset);

        ulong currentPersistedVersion = versionStorage.Get();
        _logger.Info($"Current persisted ArbOS version in storage: {currentPersistedVersion}");

        if (currentPersistedVersion != 0)
        {
            _logger.Error(
                $"InitializeArbosStateAsync: ArbOS appears to be already initialized with version {currentPersistedVersion}. Genesis initialization should only run on an empty state.");
            // This might be an error or indicate a restart with existing data. For genesis, it's an error.
            throw new InvalidOperationException($"ArbOS already initialized with version {currentPersistedVersion}. Cannot re-initialize for genesis.");
        }

        ulong desiredInitialArbosVersion = arbitrumConfig.InitialArbOSVersion;
        if (desiredInitialArbosVersion == 0)
        {
            _logger.Error("InitializeArbosStateAsync: Cannot initialize to ArbOS version 0. Invalid configuration.");
            throw new InvalidOperationException("Cannot initialize to ArbOS version 0.");
        }

        _logger.Info($"Desired initial ArbOS version from config: {desiredInitialArbosVersion}");

        // Set initial version to 1, then upgrade if necessary.
        // This matches the Go logic: initialize to version 1; upgrade at end of this func if needed
        versionStorage.Set(1);
        _logger.Info("Set ArbOS version in storage to 1.");

        var upgradeVersionStorage = new ArbosStorageBackedUint64(rootStorage, ArbosConstants.ArbosStateOffsets.UpgradeVersionOffset);
        upgradeVersionStorage.Set(0);
        var upgradeTimestampStorage = new ArbosStorageBackedUint64(rootStorage, ArbosConstants.ArbosStateOffsets.UpgradeTimestampOffset);
        upgradeTimestampStorage.Set(0);

        Address initialChainOwner = new Address(arbitrumConfig.InitialChainOwner);
        var networkFeeAccountStorage = new ArbosStorageBackedAddress(rootStorage, ArbosConstants.ArbosStateOffsets.NetworkFeeAccountOffset);
        if (desiredInitialArbosVersion >= 2) // ArbosVersion_2
        {
            networkFeeAccountStorage.Set(initialChainOwner);
            _logger.Info($"Set NetworkFeeAccount to initial chain owner: {initialChainOwner}");
        }
        else
        {
            networkFeeAccountStorage.Set(Address.Zero); // Zero address
            _logger.Info("Set NetworkFeeAccount to zero address (pre-ArbOS v2).");
        }

        var chainIdStorage = new ArbosStorageBackedInt256(rootStorage, ArbosConstants.ArbosStateOffsets.ChainIdOffset);
        chainIdStorage.Set((Int256.Int256)chainSpec.ChainId);
        _logger.Info($"Set ChainId in storage to: {chainSpec.ChainId}");

        var chainConfigStorage = new ArbosStorageBackedBytes(rootStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.ChainConfigSubspace));
        chainConfigStorage.Set(initMessage.SerializedChainConfig);
        _logger.Info($"Set SerializedChainConfig in storage (length: {initMessage.SerializedChainConfig.Length}).");

        var genesisBlockNumStorage = new ArbosStorageBackedUint64(rootStorage, ArbosConstants.ArbosStateOffsets.GenesisBlockNumOffset);
        // ulong configGenesisBlockNum = arbitrumConfig.GenesisBlockNum; // Assuming this exists in IArbitrumConfig
        // For now, let's use chainSpec.Genesis.Header.Number if it's what we need, or add to IArbitrumConfig
        // The Go code uses chainConfig.ArbitrumChainParams.GenesisBlockNum
        // Let's assume arbitrumConfig.GenesisBlockNum is the correct one for Arbitrum's own genesis block number concept
        genesisBlockNumStorage.Set(arbitrumConfig.GenesisBlockNum);
        _logger.Info($"Set GenesisBlockNum in storage to: {arbitrumConfig.GenesisBlockNum}");

        var brotliLevelStorage = new ArbosStorageBackedUint64(rootStorage, ArbosConstants.ArbosStateOffsets.BrotliCompressionLevelOffset);
        brotliLevelStorage.Set(0); // Default is 0
        _logger.Info("Set BrotliCompressionLevel in storage to 0.");

        // Initialize sub-states
        var l1PricingStorage = rootStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.L1PricingSubspace);
        Address initialRewardsRecipient = (desiredInitialArbosVersion >= 2) ? initialChainOwner : ArbosAddresses.BatchPosterAddress;
        L1PricingState.Initialize(l1PricingStorage, initialRewardsRecipient, initMessage.InitialL1BaseFee, _api.LogManager.GetClassLogger<L1PricingState>());
        _logger.Info($"L1PricingState initialized. Initial rewards recipient: {initialRewardsRecipient}");

        var l2PricingStorage = rootStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.L2PricingSubspace);
        L2PricingState.Initialize(l2PricingStorage, _api.LogManager.GetClassLogger<L2PricingState>());
        _logger.Info("L2PricingState initialized.");

        var retryableStorage = rootStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.RetryablesSubspace);
        RetryableState.Initialize(retryableStorage, _api.LogManager.GetClassLogger<RetryableState>());
        _logger.Info("RetryableState initialized.");

        var addressTableStorage = rootStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.AddressTableSubspace);
        AddressTable.Initialize(addressTableStorage, _api.LogManager.GetClassLogger<AddressTable>());
        _logger.Info("AddressTable initialized.");

        var sendMerkleStorage = rootStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.SendMerkleSubspace);
        MerkleAccumulator.Initialize(sendMerkleStorage, _api.LogManager.GetClassLogger<MerkleAccumulator>());
        _logger.Info("SendMerkleAccumulator initialized.");

        var blockhashesStorage = rootStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.BlockhashesSubspace);
        Blockhashes.Initialize(blockhashesStorage, _api.LogManager.GetClassLogger<Blockhashes>());
        _logger.Info("Blockhashes initialized.");

        var chainOwnerStorage = rootStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.ChainOwnerSubspace);
        AddressSet.Initialize(chainOwnerStorage, _api.LogManager.GetClassLogger<AddressSet>());
        var chainOwners = new AddressSet(chainOwnerStorage, _api.LogManager.GetClassLogger<AddressSet>());
        chainOwners.Add(initialChainOwner);
        _logger.Info($"ChainOwners initialized and initial owner {initialChainOwner} added.");

        ArbosState arbosState = ArbosState.OpenArbosState(worldState, burner, _api.LogManager.GetClassLogger<ArbosState>());
        _logger.Info($"ArbosState opened with current version: {arbosState.CurrentArbosVersion}");

        if (desiredInitialArbosVersion > 1)
        {
            _logger.Info($"Upgrading ArbosState from version {arbosState.CurrentArbosVersion} to {desiredInitialArbosVersion} (first time setup)...");
            await arbosState.UpgradeArbosVersionAsync(desiredInitialArbosVersion, true, worldState);
            _logger.Info($"ArbosState upgraded to version {arbosState.CurrentArbosVersion}.");
        }

        _logger.Info("InitializeArbosStateAsync: ArbOS state initialization complete.");
        return arbosState;
    }
}
