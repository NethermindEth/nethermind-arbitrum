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
    ParsedInitMessage initMessage,
    IArbitrumConfig arbitrumConfig,
    ILogManager logManager)
{
    private readonly ILogger _logger = logManager.GetClassLogger();

    public Block Load()
    {
        worldState.CreateAccountIfNotExists(ArbosAddresses.ArbosSystemAccount, UInt256.Zero, UInt256.One);
        _logger.Info($"Preallocated ArbOS system account: {ArbosAddresses.ArbosSystemAccount}");

        InitializeArbosState();

        worldState.Commit(specProvider.GenesisSpec, true);
        worldState.CommitTree(0);

        Block genesis = chainSpec.Genesis;
        genesis.Header.StateRoot = worldState.StateRoot;
        genesis.Header.Hash = genesis.Header.CalculateHash();

        _logger.Info($"Arbitrum genesis block is loaded: Number={genesis.Header.Number}, Hash={genesis.Header.Hash}, StateRoot={genesis.Header.StateRoot}");

        return genesis;
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

        ulong desiredInitialArbosVersion = arbitrumConfig.InitialArbOSVersion;
        if (desiredInitialArbosVersion == ArbosVersion.Zero)
        {
            throw new InvalidOperationException("Cannot initialize to ArbOS version 0.");
        }

        if (_logger.IsDebug)
        {
            _logger.Debug($"Desired initial ArbOS version from config: {desiredInitialArbosVersion}");
        }

        foreach ((Address address, ulong minVersion) in Precompiles.PrecompileMinArbOSVersions)
        {
            if (minVersion == ArbosVersion.Zero)
            {
                worldState.CreateAccountIfNotExists(address, UInt256.Zero);
                worldState.InsertCode(address, Precompiles.InvalidCodeHash, Precompiles.InvalidCode, specProvider.GenesisSpec, true);
            }
        }

        versionStorage.Set(ArbosVersion.One);
        _logger.Debug("Set ArbOS version in storage to 1.");

        ArbosStorageBackedULong upgradeVersionStorage = new(rootStorage, ArbosStateOffsets.UpgradeVersionOffset);
        upgradeVersionStorage.Set(0);
        ArbosStorageBackedULong upgradeTimestampStorage = new(rootStorage, ArbosStateOffsets.UpgradeTimestampOffset);
        upgradeTimestampStorage.Set(0);

        ArbosStorageBackedAddress networkFeeAccountStorage = new(rootStorage, ArbosStateOffsets.NetworkFeeAccountOffset);
        networkFeeAccountStorage.Set(desiredInitialArbosVersion >= ArbosVersion.Two ? arbitrumConfig.InitialChainOwner : Address.Zero);

        ArbosStorageBackedUInt256 chainIdStorage = new(rootStorage, ArbosStateOffsets.ChainIdOffset);
        chainIdStorage.Set(chainSpec.ChainId);

        ArbosStorageBackedBytes chainConfigStorage = new(rootStorage.OpenSubStorage(ArbosSubspaceIDs.ChainConfigSubspace));
        chainConfigStorage.Set(initMessage.SerializedChainConfig!);

        ArbosStorageBackedULong genesisBlockNumStorage = new(rootStorage, ArbosStateOffsets.GenesisBlockNumOffset);
        genesisBlockNumStorage.Set(arbitrumConfig.GenesisBlockNum);

        ArbosStorageBackedULong brotliLevelStorage = new(rootStorage, ArbosStateOffsets.BrotliCompressionLevelOffset);
        brotliLevelStorage.Set(0);

        ArbosStorage l1PricingStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.L1PricingSubspace);
        Address initialRewardsRecipient = desiredInitialArbosVersion >= ArbosVersion.Two ? arbitrumConfig.InitialChainOwner : ArbosAddresses.BatchPosterAddress;
        L1PricingState.Initialize(l1PricingStorage, initialRewardsRecipient, initMessage.InitialBaseFee);

        ArbosStorage l2PricingStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.L2PricingSubspace);
        L2PricingState.Initialize(l2PricingStorage);

        ArbosStorage retryableStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.RetryablesSubspace);
        RetryableState.Initialize(retryableStorage);

        ArbosStorage addressTableStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.AddressTableSubspace);
        AddressTable.Initialize(addressTableStorage);

        ArbosStorage sendMerkleStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.SendMerkleSubspace);
        MerkleAccumulator.Initialize(sendMerkleStorage, logManager.GetClassLogger<MerkleAccumulator>());

        ArbosStorage blockhashesStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.BlockhashesSubspace);
        Blockhashes.Initialize(blockhashesStorage, logManager.GetClassLogger<Blockhashes>());

        ArbosStorage chainOwnerStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.ChainOwnerSubspace);
        AddressSet.Initialize(chainOwnerStorage);
        AddressSet chainOwners = new(chainOwnerStorage);
        chainOwners.Add(arbitrumConfig.InitialChainOwner);

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
