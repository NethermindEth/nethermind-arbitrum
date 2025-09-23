// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Buffers;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Arbitrum.Arbos.Compression;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Arbos;

public class ArbosState
{
    private readonly ILogger _logger;

    private ArbosState(ArbosStorage backingStorage, ulong currentArbosVersion, ILogger logger)
    {
        BackingStorage = backingStorage;
        _logger = logger;

        CurrentArbosVersion = currentArbosVersion;
        UpgradeVersion = new ArbosStorageBackedULong(BackingStorage, ArbosStateOffsets.UpgradeVersionOffset);
        UpgradeTimestamp = new ArbosStorageBackedULong(BackingStorage, ArbosStateOffsets.UpgradeTimestampOffset);
        NetworkFeeAccount = new ArbosStorageBackedAddress(BackingStorage, ArbosStateOffsets.NetworkFeeAccountOffset);
        L1PricingState = new L1PricingState(BackingStorage.OpenSubStorage(ArbosSubspaceIDs.L1PricingSubspace), currentArbosVersion);
        L2PricingState = new L2PricingState(BackingStorage.OpenSubStorage(ArbosSubspaceIDs.L2PricingSubspace), currentArbosVersion);
        RetryableState = new RetryableState(BackingStorage.OpenSubStorage(ArbosSubspaceIDs.RetryablesSubspace));
        AddressTable = new AddressTable(BackingStorage.OpenSubStorage(ArbosSubspaceIDs.AddressTableSubspace));
        ChainOwners = new AddressSet(BackingStorage.OpenSubStorage(ArbosSubspaceIDs.ChainOwnerSubspace));
        NativeTokenOwners = new AddressSet(BackingStorage.OpenSubStorage(ArbosSubspaceIDs.NativeTokenOwnerSubspace));
        SendMerkleAccumulator = new MerkleAccumulator(BackingStorage.OpenSubStorage(ArbosSubspaceIDs.SendMerkleSubspace));
        Programs = new StylusPrograms(BackingStorage.OpenSubStorage(ArbosSubspaceIDs.ProgramsSubspace), CurrentArbosVersion);
        Features = new Features(BackingStorage.OpenSubStorage(ArbosSubspaceIDs.FeaturesSubspace));
        Blockhashes = new Blockhashes(BackingStorage.OpenSubStorage(ArbosSubspaceIDs.BlockhashesSubspace));
        ChainId = new ArbosStorageBackedUInt256(BackingStorage, ArbosStateOffsets.ChainIdOffset);
        ChainConfigStorage = new ArbosStorageBackedBytes(BackingStorage.OpenSubStorage(ArbosSubspaceIDs.ChainConfigSubspace));
        GenesisBlockNum = new ArbosStorageBackedULong(BackingStorage, ArbosStateOffsets.GenesisBlockNumOffset);
        InfraFeeAccount = new ArbosStorageBackedAddress(BackingStorage, ArbosStateOffsets.InfraFeeAccountOffset);
        NativeTokenEnabledTime = new ArbosStorageBackedULong(BackingStorage, ArbosStateOffsets.NativeTokenEnabledTimeOffset);
        BrotliCompressionLevel = new ArbosStorageBackedULong(BackingStorage, ArbosStateOffsets.BrotliCompressionLevelOffset);
    }

    public ArbosStorage BackingStorage { get; }
    public ulong CurrentArbosVersion { get; private set; }
    public ArbosStorageBackedULong UpgradeVersion { get; }
    public ArbosStorageBackedULong UpgradeTimestamp { get; }
    public ArbosStorageBackedAddress NetworkFeeAccount { get; }
    public L1PricingState L1PricingState { get; }
    public L2PricingState L2PricingState { get; }
    public RetryableState RetryableState { get; }
    public AddressTable AddressTable { get; }
    public AddressSet ChainOwners { get; }
    public AddressSet NativeTokenOwners { get; }
    public MerkleAccumulator SendMerkleAccumulator { get; }
    public StylusPrograms Programs { get; }
    public Features Features { get; }
    public Blockhashes Blockhashes { get; }
    public ArbosStorageBackedUInt256 ChainId { get; }
    public ArbosStorageBackedBytes ChainConfigStorage { get; }
    public ArbosStorageBackedULong GenesisBlockNum { get; }
    public ArbosStorageBackedAddress InfraFeeAccount { get; }
    public ArbosStorageBackedULong NativeTokenEnabledTime { get; }
    public ArbosStorageBackedULong BrotliCompressionLevel { get; }

    public void UpgradeArbosVersion(ulong targetVersion, bool isFirstTime, IWorldState worldState, IReleaseSpec genesisSpec)
    {
        if (Out.IsTargetBlock)
            Out.LogAlways($"arbos upgrade target={targetVersion} current={CurrentArbosVersion}");

        while (CurrentArbosVersion < targetVersion)
        {
            ulong nextArbosVersion = CurrentArbosVersion + 1;
            if (_logger.IsDebug)
            {
                _logger.Debug($"Upgrading to version {nextArbosVersion}");
            }

            try
            {
                switch (nextArbosVersion)
                {
                    case 2:
                        L1PricingState.SetLastSurplus(0, nextArbosVersion);
                        break;

                    case 3:
                        L1PricingState.SetPerBatchGasCost(0);
                        L1PricingState.SetAmortizedCostCapBips(ulong.MaxValue);
                        break;

                    case 4:
                    case 5:
                    case 6:
                    case 7:
                    case 8:
                    case 9:
                        break;

                    case 10:
                        UInt256 balance = worldState.GetBalance(ArbosAddresses.L1PricerFundsPoolAddress);
                        L1PricingState.SetL1FeesAvailable(balance);
                        break;

                    case 11:
                        // Update the PerBatchGasCost to a more accurate value compared to the old v6 default.
                        L1PricingState.SetPerBatchGasCost(L1PricingState.InitialPerBatchGasCostV12);

                        // We had mistakenly initialized AmortizedCostCapBips to math.MaxUint64 in older versions,
                        // but the correct value to disable the amortization cap is 0.
                        ulong oldAmortizationCap = L1PricingState.AmortizedCostCapBips();
                        if (oldAmortizationCap == ulong.MaxValue)
                        {
                            L1PricingState.SetAmortizedCostCapBips(0);
                        }

                        // Clear chainOwners list to allow rectification of the mapping.
                        if (!isFirstTime)
                        {
                            ChainOwners.ClearList();
                        }

                        break;

                    // Versions 12-19 are left to Orbit chains for custom upgrades
                    case 12:
                    case 13:
                    case 14:
                    case 15:
                    case 16:
                    case 17:
                    case 18:
                    case 19:
                        break;

                    case 20:
                        SetBrotliCompressionLevel(1);
                        break;

                    // Versions 21-29 are for Orbit chains
                    case 21:
                    case 22:
                    case 23:
                    case 24:
                    case 25:
                    case 26:
                    case 27:
                    case 28:
                    case 29:
                        break;

                    case 30: // Stylus
                        StylusPrograms.Initialize(nextArbosVersion, BackingStorage.OpenSubStorage(ArbosSubspaceIDs.ProgramsSubspace));
                        break;

                    case 31: // StylusFixes
                        StylusParams stylusParamsV31 = Programs.GetParams();
                        stylusParamsV31.UpgradeToStylusVersion(2);
                        stylusParamsV31.Save();
                        break;

                    case 32: // StylusChargingFixes
                        break;

                    // Versions 33-39 are for Orbit chains
                    case 33:
                    case 34:
                    case 35:
                    case 36:
                    case 37:
                    case 38:
                    case 39:
                        break;

                    case 40: // ArbosVersion_40
                        // EIP-2935: Add support for historical block hashes
                        // Deploy Arbitrum's custom EIP-2935 contract (not standard Ethereum version)
                        worldState.CreateAccountIfNotExists(Eip2935Constants.BlockHashHistoryAddress, UInt256.Zero, UInt256.One);
                        worldState.InsertCode(Eip2935Constants.BlockHashHistoryAddress, Precompiles.HistoryStorageCodeHash,
                            Precompiles.HistoryStorageCodeArbitrum,
                            genesisSpec, true);
                        StylusParams stylusParamsV40 = Programs.GetParams();
                        stylusParamsV40.UpgradeToArbosVersion(nextArbosVersion);
                        stylusParamsV40.Save();
                        break;
                    case 41: // NativeTokenManagement
                        // No state changes needed - only adds precompile methods
                        break;

                    // Versions 42-49 are reserved for Orbit chains
                    case 42:
                    case 43:
                    case 44:
                    case 45:
                    case 46:
                    case 47:
                    case 48:
                    case 49:
                        break;

                    case 50: // Dia
                        StylusParams stylusParamsV50 = Programs.GetParams();
                        stylusParamsV50.UpgradeToArbosVersion(nextArbosVersion);
                        stylusParamsV50.Save();
                        L2PricingState.SetMaxPerTxGasLimit(L2PricingState.InitialPerTxGasLimit);
                        break;

                    case 51:
                        // No state changes needed
                        break;

                    default:
                        throw new NotSupportedException($"Chain is upgrading to unsupported ArbOS version {nextArbosVersion}.");
                }
            }
            catch (Exception exception)
            {
                _logger.Error($"Failed to upgrade ArbOS from version {CurrentArbosVersion} to {nextArbosVersion}.", exception);
                throw;
            }

            foreach ((Address address, ulong minVersion) in Precompiles.PrecompileMinArbOSVersions)
            {
                if (minVersion == nextArbosVersion)
                {
                    worldState.CreateAccountIfNotExists(address, UInt256.Zero);
                    worldState.InsertCode(address, Precompiles.InvalidCodeHash, Precompiles.InvalidCode, genesisSpec, true);
                }
            }

            CurrentArbosVersion = nextArbosVersion;
            Programs.ArbosVersion = nextArbosVersion;
            L1PricingState.CurrentArbosVersion = nextArbosVersion;
            L2PricingState.CurrentArbosVersion = nextArbosVersion;
        }

        if (isFirstTime && targetVersion >= ArbosVersion.Six)
        {
            if (targetVersion < ArbosVersion.Eleven)
            {
                L1PricingState.SetPerBatchGasCost(L1PricingState.InitialPerBatchGasCostV6);
            }

            L1PricingState.SetEquilibrationUnits(L1PricingState.InitialEquilibrationUnitsV6);
            L2PricingState.SetSpeedLimitPerSecond(L2PricingState.InitialSpeedLimitPerSecondV6);
            L2PricingState.SetMaxPerBlockGasLimit(L2PricingState.InitialPerBlockGasLimitV6);
        }

        BackingStorage.Set(ArbosStateOffsets.VersionOffset, CurrentArbosVersion);
    }

    public void UpgradeArbosVersionIfNecessary(ulong timestamp, IWorldState worldState, IReleaseSpec genesisSpec)
    {
        ulong targetVersion = UpgradeVersion.Get();
        ulong plannedUpgrade = UpgradeTimestamp.Get();

        if (CurrentArbosVersion < targetVersion && timestamp >= plannedUpgrade)
            UpgradeArbosVersion(targetVersion, false, worldState, genesisSpec);
    }

    public void SetBrotliCompressionLevel(ulong level)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, BrotliCompression.LevelWell, nameof(level));

        BrotliCompressionLevel.Set(level);
    }

    public static ArbosState OpenArbosState(IWorldState worldState, IBurner burner, ILogger logger)
    {
        ArbosStorage backingStorage = new(worldState, burner, ArbosAddresses.ArbosSystemAccount);
        ulong arbosVersion = backingStorage.GetULong(ArbosStateOffsets.VersionOffset);
        if (arbosVersion == ArbosVersion.Zero)
        {
            throw new InvalidOperationException("ArbOS uninitialized. Please initialize ArbOS before using it.");
        }

        return new ArbosState(backingStorage, arbosVersion, logger);
    }

    public void ScheduleArbOSUpgrade(ulong version, ulong timestamp)
    {
        UpgradeVersion.Set(version);
        UpgradeTimestamp.Set(timestamp);
    }
}
