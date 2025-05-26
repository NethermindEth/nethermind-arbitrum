using Nethermind.Api;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Crypto;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.State;
using Nethermind.Logging;
using Nethermind.Core.Specs;
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
            InitialChainOwner = new Address("0x5E1497dD1f08C87b2d8FE23e9AAB6c1De833D927"),
            Enabled = true,
            InitialArbOSVersion = 32
        };

        _logger.Info("Starting Arbitrum genesis loading process...");

        var burner = new SystemBurner(_api.LogManager, readOnly: false);
        var parsedInitMessage = new ParsedInitMessage
        {
            SerializedChainConfig = Convert.FromHexString("7b22636861696e4964223a3431323334362c22686f6d657374656164426c6f636b223a302c2264616f466f726b537570706f7274223a747275652c22656970313530426c6f636b223a302c2265697031353048617368223a22307830303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030222c22656970313535426c6f636b223a302c22656970313538426c6f636b223a302c2262797a616e7469756d426c6f636b223a302c22636f6e7374616e74696e6f706c65426c6f636b223a302c2270657465727362757267426c6f636b223a302c22697374616e62756c426c6f636b223a302c226d756972476c6163696572426c6f636b223a302c226265726c696e426c6f636b223a302c226c6f6e646f6e426c6f636b223a302c22636c69717565223a7b22706572696f64223a302c2265706f6368223a307d2c22617262697472756d223a7b22456e61626c654172624f53223a747275652c22416c6c6f774465627567507265636f6d70696c6573223a747275652c2244617461417661696c6162696c697479436f6d6d6974746565223a66616c73652c22496e697469616c4172624f5356657273696f6e223a33322c22496e697469616c436861696e4f776e6572223a22307835453134393764443166303843383762326438464532336539414142366331446538333344393237222c2247656e65736973426c6f636b4e756d223a307d7d"),
            InitialL1BaseFee = 92
        };
        _logger.Info($"ParsedInitMessage: InitialL1BaseFee = {parsedInitMessage.InitialL1BaseFee}");

        Block genesis = chainSpec.Genesis;

        if (!worldState.TryGetAccount(ArbosAddresses.ArbosSystemAccount, out var account))
        {
            worldState.CreateAccount(ArbosAddresses.ArbosSystemAccount, UInt256.Zero, UInt256.One);
            _logger.Info($"Preallocated ArbOS system account: {ArbosAddresses.ArbosSystemAccount}");
        }
        else
        {
            _logger.Info($"ArbosOS system account {ArbosAddresses.ArbosSystemAccount} already exists.");
        }

        InitializeArbosState(worldState, burner, arbitrumConfig, chainSpec, specProvider.GenesisSpec, parsedInitMessage);

        worldState.Commit(specProvider.GenesisSpec, true);
        worldState.CommitTree(0);

        if (worldState.TryGetAccount(ArbosAddresses.ArbosSystemAccount, out var updatedAccount))
        {
            _logger.Info($"ArbOS account storage root: {updatedAccount.StorageRoot}");
        }

        _logger.Info($"Initial world state root after commit: {worldState.StateRoot}");

        genesis.Header.StateRoot = worldState.StateRoot;
        genesis.Header.Hash = genesis.Header.CalculateHash();

        _logger.Info($"Arbitrum Genesis Block Loaded: Number={genesis.Header.Number}, Hash={genesis.Header.Hash}, StateRoot={genesis.Header.StateRoot}");

        return genesis;
    }

    public ArbosState? InitializeArbosState(
        IWorldState worldState,
        IBurner burner,
        IArbitrumConfig arbitrumConfig,
        ChainSpec chainSpec,
        IReleaseSpec genesisSpec,
        ParsedInitMessage initMessage)
    {
        _logger.Info("Starting ArbOS state initialization...");

        var rootStorage = new ArbosStorage(worldState, burner, ArbosAddresses.ArbosSystemAccount);
        var versionStorage = new ArbosStorageBackedUint64(rootStorage, ArbosConstants.ArbosStateOffsets.VersionOffset);

        ulong currentPersistedVersion = versionStorage.Get();
        _logger.Info($"Current persisted ArbOS version in storage: {currentPersistedVersion}");

        if (currentPersistedVersion != 0)
        {
            throw new InvalidOperationException($"ArbOS already initialized with version {currentPersistedVersion}. Cannot re-initialize for genesis.");
        }

        ulong desiredInitialArbosVersion = arbitrumConfig.InitialArbOSVersion;
        if (desiredInitialArbosVersion == 0)
        {
            throw new InvalidOperationException("Cannot initialize to ArbOS version 0.");
        }

        _logger.Info($"Desired initial ArbOS version from config: {desiredInitialArbosVersion}");

        foreach (var (address, minVersion) in Precompiles.PrecompileMinArbOSVersions)
        {
            if (minVersion == 0)
            {
                worldState.CreateAccountIfNotExists(address, UInt256.Zero);
                worldState.InsertCode(address, Precompiles.InvalidCodeHash, Precompiles.InvalidCode, genesisSpec, true);
            }
        }

        versionStorage.Set(1);
        _logger.Info("Set ArbOS version in storage to 1.");

        var upgradeVersionStorage = new ArbosStorageBackedUint64(rootStorage, ArbosConstants.ArbosStateOffsets.UpgradeVersionOffset);
        upgradeVersionStorage.Set(0);
        var upgradeTimestampStorage = new ArbosStorageBackedUint64(rootStorage, ArbosConstants.ArbosStateOffsets.UpgradeTimestampOffset);
        upgradeTimestampStorage.Set(0);

        var networkFeeAccountStorage = new ArbosStorageBackedAddress(rootStorage, ArbosConstants.ArbosStateOffsets.NetworkFeeAccountOffset);
        if (desiredInitialArbosVersion >= 2)
        {
            networkFeeAccountStorage.Set(arbitrumConfig.InitialChainOwner);
            _logger.Info($"Set NetworkFeeAccount to initial chain owner: {arbitrumConfig.InitialChainOwner}");
        }
        else
        {
            networkFeeAccountStorage.Set(Address.Zero);
            _logger.Info("Set NetworkFeeAccount to zero address (pre-ArbOS v2).");
        }

        var chainIdStorage = new ArbosStorageBackedInt256(rootStorage, ArbosConstants.ArbosStateOffsets.ChainIdOffset);
        chainIdStorage.Set((Int256.Int256)chainSpec.ChainId);
        _logger.Info($"Set ChainId in storage to: {chainSpec.ChainId}");

        var chainConfigStorage = new ArbosStorageBackedBytes(rootStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.ChainConfigSubspace));
        chainConfigStorage.Set(initMessage.SerializedChainConfig);
        _logger.Info($"Set SerializedChainConfig in storage (length: {initMessage.SerializedChainConfig.Length}).");

        var genesisBlockNumStorage = new ArbosStorageBackedUint64(rootStorage, ArbosConstants.ArbosStateOffsets.GenesisBlockNumOffset);
        genesisBlockNumStorage.Set(arbitrumConfig.GenesisBlockNum);
        _logger.Info($"Set GenesisBlockNum in storage to: {arbitrumConfig.GenesisBlockNum}");

        var brotliLevelStorage = new ArbosStorageBackedUint64(rootStorage, ArbosConstants.ArbosStateOffsets.BrotliCompressionLevelOffset);
        brotliLevelStorage.Set(0);
        _logger.Info("Set BrotliCompressionLevel in storage to 0.");

        var l1PricingStorage = rootStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.L1PricingSubspace);
        Address initialRewardsRecipient = (desiredInitialArbosVersion >= 2) ? arbitrumConfig.InitialChainOwner : ArbosAddresses.BatchPosterAddress;
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
        chainOwners.Add(arbitrumConfig.InitialChainOwner);
        _logger.Info($"ChainOwners initialized and initial owner {arbitrumConfig.InitialChainOwner} added.");

        ArbosState arbosState = ArbosState.OpenArbosState(worldState, burner, _api.LogManager.GetClassLogger<ArbosState>());
        _logger.Info($"ArbosState opened with current version: {arbosState.CurrentArbosVersion}");

        if (desiredInitialArbosVersion > 1)
        {
            _logger.Info($"Upgrading ArbosState from version {arbosState.CurrentArbosVersion} to {desiredInitialArbosVersion} (first time setup)...");
            arbosState.UpgradeArbosVersion(desiredInitialArbosVersion, true, worldState, genesisSpec);
            _logger.Info($"ArbosState upgraded to version {arbosState.CurrentArbosVersion}.");
        }

        _logger.Info("ArbOS state initialization complete.");
        return arbosState;
    }
}
