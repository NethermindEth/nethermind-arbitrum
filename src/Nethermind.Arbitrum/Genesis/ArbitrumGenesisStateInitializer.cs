// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

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

/// <summary>
/// Handles common initialization logic for Arbitrum genesis state.
/// </summary>
public class ArbitrumGenesisStateInitializer(
    ChainSpec chainSpec,
    IArbitrumSpecHelper specHelper,
    ILogManager logManager)
{
    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumGenesisStateInitializer>();

    public void ValidateInitMessage(ParsedInitMessage initMessage)
    {
        string? compatibilityError = initMessage.IsCompatibleWith(chainSpec);
        if (compatibilityError != null)
            throw new InvalidOperationException(
                $"Incompatible init message: {compatibilityError}. " +
                $"This indicates a mismatch between the init message and chainspec configuration.");
    }

    public Block InitializeAndBuildGenesisBlock(
        ParsedInitMessage initMessage,
        IWorldState worldState,
        ISpecProvider specProvider)
    {
        // NOTE: Do NOT pre-create ArbosSystemAccount here. Nitro creates it via
        // KVStorage.SetNonce(account, 1) which marks the account dirty. Pre-creating
        // with CreateAccountIfNotExists causes state divergence because when ArbosStorage
        // later calls SetNonce(1), the nonce is already 1 and no state change occurs.

        // Get canonical parameters to determine final ArbOS version
        ArbitrumChainSpecEngineParameters canonicalParams = initMessage.GetCanonicalArbitrumParameters(specHelper);
        ulong finalArbosVersion = canonicalParams.InitialArbOSVersion ?? specHelper.InitialArbOSVersion;

        InitializeArbosState(initMessage, worldState, specProvider);
        Preallocate(worldState, specProvider);

        worldState.Commit(specProvider.GenesisSpec, true);
        worldState.CommitTree(0);

        Block genesis = chainSpec.Genesis;
        genesis.Header.StateRoot = worldState.StateRoot;

        // Update MixHash with correct ArbOS version (chainspec has pre-computed value that may differ)
        ArbitrumBlockHeaderInfo genesisHeaderInfo = new()
        {
            SendRoot = Hash256.Zero,
            SendCount = 0,
            L1BlockNumber = 0,
            ArbOSFormatVersion = finalArbosVersion
        };
        ArbitrumBlockHeaderInfo.UpdateHeader(genesis.Header, genesisHeaderInfo);

        genesis.Header.Hash = genesis.Header.CalculateHash();

        if (_logger.IsInfo)
            _logger.Info($"Genesis block MixHash updated with ArbOS version {finalArbosVersion}");

        return genesis;
    }

    public void InitializeArbosState(ParsedInitMessage initMessage, IWorldState worldState, ISpecProvider specProvider)
    {
        _logger.Info("Initializing ArbOS...");

        // Initialize debug logger for genesis state comparison
        using GenesisDebugLogger debugLog = new(worldState, specProvider.GenesisSpec);

        SystemBurner burner = new(readOnly: false);
        ArbosStorage rootStorage = new(worldState, burner, ArbosAddresses.ArbosSystemAccount);
        ArbosStorageBackedULong versionStorage = new(rootStorage, ArbosStateOffsets.VersionOffset);

        ulong currentPersistedVersion = versionStorage.Get();
        if (currentPersistedVersion != ArbosVersion.Zero)
            throw new InvalidOperationException($"ArbOS already initialized with version {currentPersistedVersion}. Cannot re-initialize for genesis.");

        ArbitrumChainSpecEngineParameters canonicalArbitrumParams = initMessage.GetCanonicalArbitrumParameters(specHelper);

        if (canonicalArbitrumParams.InitialArbOSVersion == null)
            throw new InvalidOperationException("Cannot initialize ArbOS without initial ArbOS version");

        ulong desiredInitialArbosVersion = canonicalArbitrumParams.InitialArbOSVersion.Value;
        if (desiredInitialArbosVersion == ArbosVersion.Zero)
            throw new InvalidOperationException("Cannot initialize to ArbOS version 0.");

        debugLog.LogStepWithValue("00_init", "desiredArbosVersion", desiredInitialArbosVersion);

        // Initialize precompiles
        foreach ((Address address, ulong minVersion) in Arbos.Precompiles.PrecompileMinArbOSVersions)
        {
            if (minVersion == ArbosVersion.Zero)
            {
                worldState.CreateAccountIfNotExists(address, UInt256.Zero);
                worldState.InsertCode(address, Arbos.Precompiles.InvalidCodeHash, Arbos.Precompiles.InvalidCode, specProvider.GenesisSpec, true);
            }
        }
        debugLog.LogStep("01_precompiles_code");

        // Set nativeTokenEnabledFromTime to 0 (disabled by default)
        // If NativeTokenSupplyManagementEnabled were true in genesis config, this would be set to 1
        ArbosStorageBackedULong nativeTokenEnabledTimeStorage = new(rootStorage, ArbosStateOffsets.NativeTokenEnabledTimeOffset);
        nativeTokenEnabledTimeStorage.Set(0);
        debugLog.LogStepWithValue("02_nativeTokenEnabledFromTime", "value", 0);

        // Set transactionFilteringEnabledFromTime to 0 (disabled by default)
        // If TransactionFilteringEnabled were true in genesis config, this would be set to 1
        ArbosStorageBackedULong transactionFilteringEnabledTimeStorage = new(rootStorage, ArbosStateOffsets.TransactionFilteringEnabledTimeOffset);
        transactionFilteringEnabledTimeStorage.Set(0);
        debugLog.LogStepWithValue("03_transactionFilteringEnabledFromTime", "value", 0);

        versionStorage.Set(ArbosVersion.One);
        debugLog.LogStep("04_version_set_to_1");

        ArbosStorageBackedULong upgradeVersionStorage = new(rootStorage, ArbosStateOffsets.UpgradeVersionOffset);
        upgradeVersionStorage.Set(0);
        debugLog.LogStep("05_upgradeVersion_set_to_0");

        ArbosStorageBackedULong upgradeTimestampStorage = new(rootStorage, ArbosStateOffsets.UpgradeTimestampOffset);
        upgradeTimestampStorage.Set(0);
        debugLog.LogStep("06_upgradeTimestamp_set_to_0");

        if (canonicalArbitrumParams.InitialChainOwner == null)
            throw new InvalidOperationException("Cannot initialize ArbOS without initial chain owner");

        Address canonicalChainOwner = canonicalArbitrumParams.InitialChainOwner;
        ArbosStorageBackedAddress networkFeeAccountStorage = new(rootStorage, ArbosStateOffsets.NetworkFeeAccountOffset);
        networkFeeAccountStorage.Set(desiredInitialArbosVersion >= ArbosVersion.Two ? canonicalChainOwner : Address.Zero);
        debugLog.LogStepWithValue("07_networkFeeAccount", "owner", canonicalChainOwner);

        ArbosStorageBackedUInt256 chainIdStorage = new(rootStorage, ArbosStateOffsets.ChainIdOffset);
        chainIdStorage.Set(initMessage.ChainId);
        debugLog.LogStepWithValue("08_chainId", "chainId", initMessage.ChainId);

        ArbosStorageBackedBytes chainConfigStorage = new(rootStorage.OpenSubStorage(ArbosSubspaceIDs.ChainConfigSubspace));
        if (initMessage.SerializedChainConfig != null)
        {
            chainConfigStorage.Set(initMessage.SerializedChainConfig);
            debugLog.LogStepWithValue("09_chainConfig", "len", initMessage.SerializedChainConfig.Length);
        }
        else
            throw new InvalidOperationException("Cannot initialize ArbOS without serialized chain config");

        if (canonicalArbitrumParams.GenesisBlockNum == null)
            throw new InvalidOperationException("Cannot initialize ArbOS without genesis block number");

        ulong canonicalGenesisBlockNum = canonicalArbitrumParams.GenesisBlockNum.Value;
        ArbosStorageBackedULong genesisBlockNumStorage = new(rootStorage, ArbosStateOffsets.GenesisBlockNumOffset);
        genesisBlockNumStorage.Set(canonicalGenesisBlockNum);
        debugLog.LogStepWithValue("10_genesisBlockNum", "blockNum", canonicalGenesisBlockNum);

        ArbosStorageBackedULong brotliLevelStorage = new(rootStorage, ArbosStateOffsets.BrotliCompressionLevelOffset);
        brotliLevelStorage.Set(0);
        debugLog.LogStep("11_brotliCompressionLevel");

        ArbosStorage l1PricingStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.L1PricingSubspace);
        Address initialRewardsRecipient = desiredInitialArbosVersion >= ArbosVersion.Two ? canonicalChainOwner : ArbosAddresses.BatchPosterAddress;
        L1PricingState.Initialize(l1PricingStorage, initialRewardsRecipient, initMessage.InitialBaseFee);
        debugLog.LogStepWithValue("12_l1PricingState", "rewardsRecipient", initialRewardsRecipient);

        ArbosStorage l2PricingStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.L2PricingSubspace);
        L2PricingState.Initialize(l2PricingStorage);
        debugLog.LogStep("13_l2PricingState");

        ArbosStorage retryableStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.RetryablesSubspace);
        RetryableState.Initialize(retryableStorage);
        debugLog.LogStep("14_retryableState");

        ArbosStorage addressTableStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.AddressTableSubspace);
        AddressTable.Initialize(addressTableStorage);
        debugLog.LogStep("15_addressTable");

        // Nitro's InitializeMerkleAccumulator is a no-op ("// no initialization needed")
        // We just log the step for consistency
        debugLog.LogStep("16_sendMerkle");

        ArbosStorage blockhashesStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.BlockhashesSubspace);
        Blockhashes.Initialize(blockhashesStorage, _logger);
        debugLog.LogStep("17_blockhashes");

        ArbosStorage chainOwnerStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.ChainOwnerSubspace);
        AddressSet.Initialize(chainOwnerStorage);
        debugLog.LogStep("18_chainOwners_init");

        AddressSet chainOwners = new(chainOwnerStorage);
        chainOwners.Add(canonicalChainOwner);
        debugLog.LogStepWithValue("19_chainOwners_add", "owner", canonicalChainOwner);

        ArbosStorage nativeTokenOwnerStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.NativeTokenOwnerSubspace);
        AddressSet.Initialize(nativeTokenOwnerStorage);
        debugLog.LogStep("20_nativeTokenOwners_init");

        // Initialize transactionFilterers AddressSet
        ArbosStorage transactionFiltererStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.TransactionFiltererSubspace);
        AddressSet.Initialize(transactionFiltererStorage);
        debugLog.LogStep("21_transactionFilterers_init");

        ArbosState arbosState = ArbosState.OpenArbosState(worldState, burner, _logger);
        debugLog.LogStep("22_arbosState_opened");

        if (desiredInitialArbosVersion > ArbosVersion.One)
        {
            _logger.Info($"Upgrading ArbosState from version {arbosState.CurrentArbosVersion} to {desiredInitialArbosVersion} (first time setup)...");
            arbosState.UpgradeArbosVersionWithLog(desiredInitialArbosVersion, true, worldState, specProvider.GenesisSpec, debugLog);
            _logger.Info($"ArbosState upgraded to version {arbosState.CurrentArbosVersion}.");
            debugLog.LogStepWithValue("23_arbosState_upgraded", "version", desiredInitialArbosVersion);
        }

        debugLog.LogStep("24_init_complete");
        _logger.Info("ArbOS state initialization complete.");
    }

    public void Preallocate(IWorldState worldState, ISpecProvider specProvider)
    {
        if (!ShouldApplyAllocations(chainSpec.Allocations))
            return;

        foreach ((Address address, ChainSpecAllocation allocation) in chainSpec.Allocations)
        {
            worldState.CreateAccountIfNotExists(address, allocation.Balance, allocation.Nonce);

            Hash256 codeHash = Keccak.Compute(allocation.Code);
            worldState.InsertCode(address, codeHash, allocation.Code, specProvider.GenesisSpec, isGenesis: true);

            if (allocation.Storage is not null)
                foreach ((UInt256 index, byte[] value) in allocation.Storage)
                    worldState.Set(new StorageCell(address, index), value);

            if (_logger.IsDebug)
                _logger.Debug($"Applied genesis allocation: {address} with balance {allocation.Balance}");
        }

        if (_logger.IsDebug)
            _logger.Debug($"Applied {chainSpec.Allocations.Count} genesis account allocations");
    }

    private static bool ShouldApplyAllocations(IDictionary<Address, ChainSpecAllocation> allocations)
    {
        if (allocations is null || allocations.Count == 0)
            return false;

        if (allocations.Count > 1)
            return true;

        if (allocations.Count == 1)
        {
            ChainSpecAllocation allocation = allocations.Values.First();
            return allocation.Balance > UInt256.One;
        }

        return false;
    }
}
