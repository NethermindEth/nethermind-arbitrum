using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Crypto;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Genesis;

/// <summary>
/// Builds Arbitrum genesis block by initializing ArbOS state.
/// </summary>
public class ArbitrumGenesisBuilder : IGenesisBuilder
{
    private readonly ChainSpec _chainSpec;
    private readonly ISpecProvider _specProvider;
    private readonly IArbitrumSpecHelper _specHelper;
    private readonly IWorldState _worldState;
    private readonly ILogManager _logManager;
    private readonly ILogger _logger;

    public ArbitrumGenesisBuilder(
        ChainSpec chainSpec,
        ISpecProvider specProvider,
        IArbitrumSpecHelper specHelper,
        IWorldState worldState,
        ILogManager logManager)
    {
        _chainSpec = chainSpec;
        _specProvider = specProvider;
        _specHelper = specHelper;
        _worldState = worldState;
        _logManager = logManager;
        _logger = logManager.GetClassLogger<ArbitrumGenesisBuilder>();
    }

    public Block Build()
    {
        // Create init message from chainspec
        ChainSpecInitMessageProvider initMessageProvider = new(_chainSpec, _specHelper);
        ParsedInitMessage initMessage = initMessageProvider.GetInitMessage();

        ValidateInitMessage(initMessage);

        _worldState.CreateAccountIfNotExists(ArbosAddresses.ArbosSystemAccount, UInt256.Zero, UInt256.One);
        _logger.Info($"Preallocated ArbOS system account: {ArbosAddresses.ArbosSystemAccount}");

        InitializeArbosState(initMessage);
        Preallocate();

        _worldState.Commit(_specProvider.GenesisSpec, true);
        _worldState.CommitTree(0);

        Block genesis = _chainSpec.Genesis;
        _logger.Info($"Before setting state root - Genesis header: Number={genesis.Header.Number}, ParentHash={genesis.Header.ParentHash}, TxRoot={genesis.Header.TxRoot}, ReceiptsRoot={genesis.Header.ReceiptsRoot}, Difficulty={genesis.Header.Difficulty}, GasLimit={genesis.Header.GasLimit}, Timestamp={genesis.Header.Timestamp}, ExtraData={genesis.Header.ExtraData?.ToHexString()}, MixHash={genesis.Header.MixHash}, Nonce={genesis.Header.Nonce}, BaseFee={genesis.Header.BaseFeePerGas}");
        genesis.Header.StateRoot = _worldState.StateRoot;
        genesis.Header.Hash = genesis.Header.CalculateHash();

        _logger.Info($"Arbitrum genesis block built: Number={genesis.Header.Number}, Hash={genesis.Header.Hash}, StateRoot={genesis.Header.StateRoot}");

        return genesis;
    }

    private void ValidateInitMessage(ParsedInitMessage initMessage)
    {
        string? compatibilityError = initMessage.IsCompatibleWith(_chainSpec);
        if (compatibilityError != null)
            throw new InvalidOperationException(
                $"Incompatible init message: {compatibilityError}. " +
                $"This indicates a mismatch between the chainspec configuration.");

        if (initMessage.SerializedChainConfig != null)
        {
            string serializedConfigJson = System.Text.Encoding.UTF8.GetString(initMessage.SerializedChainConfig);
            _logger.Info($"Using chain config: {serializedConfigJson}");
        }

        _logger.Info("Init message validation passed - configuration is compatible with chainspec");
    }

    private void InitializeArbosState(ParsedInitMessage initMessage)
    {
        _logger.Info("Initializing ArbOS...");

        SystemBurner burner = new(readOnly: false);
        ArbosStorage rootStorage = new(_worldState, burner, ArbosAddresses.ArbosSystemAccount);
        ArbosStorageBackedULong versionStorage = new(rootStorage, ArbosStateOffsets.VersionOffset);

        ulong currentPersistedVersion = versionStorage.Get();
        if (currentPersistedVersion != ArbosVersion.Zero)
            throw new InvalidOperationException($"ArbOS already initialized with version {currentPersistedVersion}. Cannot re-initialize for genesis.");

        ArbitrumChainSpecEngineParameters canonicalArbitrumParams = initMessage.GetCanonicalArbitrumParameters(_specHelper);

        if (canonicalArbitrumParams.InitialArbOSVersion == null)
            throw new InvalidOperationException("Cannot initialize ArbOS without initial ArbOS version");

        ulong desiredInitialArbosVersion = canonicalArbitrumParams.InitialArbOSVersion.Value;
        if (desiredInitialArbosVersion == ArbosVersion.Zero)
            throw new InvalidOperationException("Cannot initialize to ArbOS version 0.");

        if (_logger.IsDebug)
            _logger.Debug($"Using initial ArbOS version: {desiredInitialArbosVersion}");

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

        if (canonicalArbitrumParams.InitialChainOwner == null)
            throw new InvalidOperationException("Cannot initialize ArbOS without initial chain owner");

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
                _logger.Debug("Stored chain config in ArbOS state");
        }
        else
            throw new InvalidOperationException("Cannot initialize ArbOS without serialized chain config");

        if (canonicalArbitrumParams.GenesisBlockNum == null)
            throw new InvalidOperationException("Cannot initialize ArbOS without genesis block number");

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
        Blockhashes.Initialize(blockhashesStorage, _logManager.GetClassLogger<Blockhashes>());

        ArbosStorage chainOwnerStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.ChainOwnerSubspace);
        AddressSet.Initialize(chainOwnerStorage);
        AddressSet chainOwners = new(chainOwnerStorage);
        chainOwners.Add(canonicalChainOwner);
        ArbosStorage nativeTokenOwnerStorage = rootStorage.OpenSubStorage(ArbosSubspaceIDs.NativeTokenOwnerSubspace);
        AddressSet.Initialize(nativeTokenOwnerStorage);

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
        if (!ShouldApplyAllocations(_chainSpec.Allocations))
            return;

        foreach ((Address address, ChainSpecAllocation allocation) in _chainSpec.Allocations)
        {
            _worldState.CreateAccountIfNotExists(address, allocation.Balance, allocation.Nonce);

            Hash256 codeHash = Keccak.Compute(allocation.Code);
            _worldState.InsertCode(address, codeHash, allocation.Code, _specProvider.GenesisSpec, isGenesis: true);

            if (allocation.Storage is not null)
                foreach ((UInt256 index, byte[] value) in allocation.Storage)
                    _worldState.Set(new StorageCell(address, index), value);

            if (_logger.IsDebug)
                _logger.Debug($"Applied genesis allocation: {address} with balance {allocation.Balance}");
        }

        _logger.Info($"Applied {_chainSpec.Allocations.Count()} genesis account allocations");
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
