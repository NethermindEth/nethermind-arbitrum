using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Data;
using Nethermind.Core;
using Nethermind.Crypto;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.State;
using Nethermind.Logging;
using Nethermind.Core.Specs;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Genesis;

public class ArbitrumGenesisLoader(
    ChainSpec chainSpec,
    ISpecProvider specProvider,
    IWorldState worldState,
    ParsedInitMessage parsedInitMessage,
    IArbitrumConfig arbitrumConfig,
    ILogManager logManager)
{
    private readonly ILogger _logger = logManager.GetClassLogger();

    public Block Load()
    {
        _logger.Info("Starting Arbitrum genesis loading process...");

        var burner = new SystemBurner(logManager, readOnly: false);
        _logger.Info($"ParsedInitMessage: InitialL1BaseFee = {parsedInitMessage.InitialBaseFee}");

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

        InitializeArbosState(burner, arbitrumConfig, parsedInitMessage);

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
        IBurner burner,
        IArbitrumConfig arbitrumConfig,
        ParsedInitMessage initMessage)
    {
        _logger.Info("Starting ArbOS state initialization...");

        var rootStorage = new ArbosStorage(worldState, burner, ArbosAddresses.ArbosSystemAccount);
        var versionStorage = new ArbosStorageBackedULong(rootStorage, ArbosConstants.ArbosStateOffsets.VersionOffset);

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
                worldState.InsertCode(address, Precompiles.InvalidCodeHash, Precompiles.InvalidCode, specProvider.GenesisSpec, true);
            }
        }

        versionStorage.Set(1);
        _logger.Info("Set ArbOS version in storage to 1.");

        var upgradeVersionStorage = new ArbosStorageBackedULong(rootStorage, ArbosConstants.ArbosStateOffsets.UpgradeVersionOffset);
        upgradeVersionStorage.Set(0);
        var upgradeTimestampStorage = new ArbosStorageBackedULong(rootStorage, ArbosConstants.ArbosStateOffsets.UpgradeTimestampOffset);
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

        var genesisBlockNumStorage = new ArbosStorageBackedULong(rootStorage, ArbosConstants.ArbosStateOffsets.GenesisBlockNumOffset);
        genesisBlockNumStorage.Set(arbitrumConfig.GenesisBlockNum);
        _logger.Info($"Set GenesisBlockNum in storage to: {arbitrumConfig.GenesisBlockNum}");

        var brotliLevelStorage = new ArbosStorageBackedULong(rootStorage, ArbosConstants.ArbosStateOffsets.BrotliCompressionLevelOffset);
        brotliLevelStorage.Set(0);
        _logger.Info("Set BrotliCompressionLevel in storage to 0.");

        var l1PricingStorage = rootStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.L1PricingSubspace);
        Address initialRewardsRecipient = (desiredInitialArbosVersion >= 2) ? arbitrumConfig.InitialChainOwner : ArbosAddresses.BatchPosterAddress;
        L1PricingState.Initialize(l1PricingStorage, initialRewardsRecipient, new Int256.Int256(initMessage.InitialBaseFee), logManager.GetClassLogger<L1PricingState>());
        _logger.Info($"L1PricingState initialized. Initial rewards recipient: {initialRewardsRecipient}");

        var l2PricingStorage = rootStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.L2PricingSubspace);
        L2PricingState.Initialize(l2PricingStorage, logManager.GetClassLogger<L2PricingState>());
        _logger.Info("L2PricingState initialized.");

        var retryableStorage = rootStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.RetryablesSubspace);
        RetryableState.Initialize(retryableStorage, logManager.GetClassLogger<RetryableState>());
        _logger.Info("RetryableState initialized.");

        var addressTableStorage = rootStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.AddressTableSubspace);
        AddressTable.Initialize(addressTableStorage, logManager.GetClassLogger<AddressTable>());
        _logger.Info("AddressTable initialized.");

        var sendMerkleStorage = rootStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.SendMerkleSubspace);
        MerkleAccumulator.Initialize(sendMerkleStorage, logManager.GetClassLogger<MerkleAccumulator>());
        _logger.Info("SendMerkleAccumulator initialized.");

        var blockhashesStorage = rootStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.BlockhashesSubspace);
        Blockhashes.Initialize(blockhashesStorage, logManager.GetClassLogger<Blockhashes>());
        _logger.Info("Blockhashes initialized.");

        var chainOwnerStorage = rootStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.ChainOwnerSubspace);
        AddressSet.Initialize(chainOwnerStorage, logManager.GetClassLogger<AddressSet>());
        var chainOwners = new AddressSet(chainOwnerStorage, logManager.GetClassLogger<AddressSet>());
        chainOwners.Add(arbitrumConfig.InitialChainOwner);
        _logger.Info($"ChainOwners initialized and initial owner {arbitrumConfig.InitialChainOwner} added.");

        ArbosState arbosState = ArbosState.OpenArbosState(worldState, burner, logManager.GetClassLogger<ArbosState>());
        _logger.Info($"ArbosState opened with current version: {arbosState.CurrentArbosVersion}");

        if (desiredInitialArbosVersion > 1)
        {
            _logger.Info($"Upgrading ArbosState from version {arbosState.CurrentArbosVersion} to {desiredInitialArbosVersion} (first time setup)...");
            arbosState.UpgradeArbosVersion(desiredInitialArbosVersion, true, worldState, specProvider.GenesisSpec);
            _logger.Info($"ArbosState upgraded to version {arbosState.CurrentArbosVersion}.");
        }

        _logger.Info("ArbOS state initialization complete.");
        return arbosState;
    }
}
