// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.State;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos;
using Int256;

public class ArbosState
{
    private readonly ArbosStorage _backingStorage;
    private readonly IBurner _burner;
    private readonly ILogger _logger;

    private ArbosState(ArbosStorage backingStorage, IBurner burner, ILogger logger, ulong currentArbosVersion)
    {
        _backingStorage = backingStorage;
        _burner = burner;
        _logger = logger;

        CurrentArbosVersion = currentArbosVersion;
        UpgradeVersion = new ArbosStorageBackedUint64(_backingStorage, ArbosConstants.ArbosStateOffsets.UpgradeVersionOffset);
        UpgradeTimestamp = new ArbosStorageBackedUint64(_backingStorage, ArbosConstants.ArbosStateOffsets.UpgradeTimestampOffset);
        NetworkFeeAccount = new ArbosStorageBackedAddress(_backingStorage, ArbosConstants.ArbosStateOffsets.NetworkFeeAccountOffset);
        L1PricingState = new L1PricingState(_backingStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.L1PricingSubspace), _logger);
        L2PricingState = new L2PricingState(_backingStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.L2PricingSubspace), _logger);
        RetryableState = new RetryableState(_backingStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.RetryablesSubspace), _logger);
        AddressTable = new AddressTable(_backingStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.AddressTableSubspace), _logger);
        ChainOwners = new AddressSet(_backingStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.ChainOwnerSubspace), _logger);
        SendMerkleAccumulator = new MerkleAccumulator(_backingStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.SendMerkleSubspace), _logger);
        Programs = new Programs(_backingStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.ProgramsSubspace), CurrentArbosVersion, _logger);
        Features = new Features(_backingStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.FeaturesSubspace), _logger);
        Blockhashes = new Blockhashes(_backingStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.BlockhashesSubspace), _logger);
        ChainId = new ArbosStorageBackedInt256(_backingStorage, ArbosConstants.ArbosStateOffsets.ChainIdOffset);
        ChainConfigStorage = new ArbosStorageBackedBytes(_backingStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.ChainConfigSubspace));
        GenesisBlockNum = new ArbosStorageBackedUint64(_backingStorage, ArbosConstants.ArbosStateOffsets.GenesisBlockNumOffset);
        InfraFeeAccount = new ArbosStorageBackedAddress(_backingStorage, ArbosConstants.ArbosStateOffsets.InfraFeeAccountOffset);
        BrotliCompressionLevel = new ArbosStorageBackedUint64(_backingStorage, ArbosConstants.ArbosStateOffsets.BrotliCompressionLevelOffset);
    }

    public ulong CurrentArbosVersion { get; private set; }
    public ArbosStorageBackedUint64 UpgradeVersion { get; }
    public ArbosStorageBackedUint64 UpgradeTimestamp { get; }
    public ArbosStorageBackedAddress NetworkFeeAccount { get; }
    public L1PricingState L1PricingState { get; }
    public L2PricingState L2PricingState { get; }
    public RetryableState RetryableState { get; }
    public AddressTable AddressTable { get; }
    public AddressSet ChainOwners { get; }
    public MerkleAccumulator SendMerkleAccumulator { get; }
    public Programs Programs { get; }
    public Features Features { get; }
    public Blockhashes Blockhashes { get; }
    public ArbosStorageBackedInt256 ChainId { get; }
    public ArbosStorageBackedBytes ChainConfigStorage { get; }
    public ArbosStorageBackedUint64 GenesisBlockNum { get; }
    public ArbosStorageBackedAddress InfraFeeAccount { get; }
    public ArbosStorageBackedUint64 BrotliCompressionLevel { get; }

    public async Task UpgradeArbosVersionAsync(ulong targetVersion, bool isFirstTime, IWorldState worldState)
    {
        _logger.Info($"Attempting to upgrade ArbOS from version {CurrentArbosVersion} to {targetVersion}. First time: {isFirstTime}");

        while (CurrentArbosVersion < targetVersion)
        {
            ulong nextArbosVersion = CurrentArbosVersion + 1;
            _logger.Info($"Upgrading to version {nextArbosVersion}");

            try
            {
                switch (nextArbosVersion)
                {
                    case 2:
                        L1PricingState.SetLastSurplus(Int256.Zero, 1);
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
                            ChainOwners.Clear();
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
                        SetBrotliCompressionLevelAsync(1);
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
                        Programs.Initialize(nextArbosVersion, _backingStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.ProgramsSubspace), _logger);
                        break;

                    case 31: // StylusFixes
                        var stylusParamsV31 = Programs.GetParams();
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
                        var stylusParamsV40 = Programs.GetParams();
                        stylusParamsV40.UpgradeToArbosVersion(nextArbosVersion);
                        stylusParamsV40.Save();
                        break;

                    default:
                        throw new NotSupportedException($"Chain is upgrading to unsupported ArbOS version {nextArbosVersion}.");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to upgrade ArbOS from version {CurrentArbosVersion} to {nextArbosVersion}.", ex);
                throw;
            }

            CurrentArbosVersion = nextArbosVersion;
            Programs.ArbosVersion = nextArbosVersion;
        }

        if (isFirstTime && targetVersion >= 6)
        {
            if (targetVersion < 11)
            {
                L1PricingState.SetPerBatchGasCost(L1PricingState.InitialPerBatchGasCostV6);
            }

            L1PricingState.SetEquilibrationUnits(L1PricingState.InitialEquilibrationUnitsV6);
            L2PricingState.SetSpeedLimitPerSecond(L2PricingState.InitialSpeedLimitPerSecondV6);
            L2PricingState.SetMaxPerBlockGasLimit(L2PricingState.InitialPerBlockGasLimitV6);
        }

        _backingStorage.SetUint64ByUint64(ArbosConstants.ArbosStateOffsets.VersionOffset, CurrentArbosVersion);

        _logger.Info($"Successfully upgraded ArbOS to version {CurrentArbosVersion}.");
    }

    public void SetBrotliCompressionLevelAsync(ulong level)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, Compression.LevelWell, nameof(level));

        BrotliCompressionLevel.Set(level);
    }

    public static ArbosState OpenArbosState(IWorldState worldState, IBurner burner, ILogger logger)
    {
        var backingStorage = new ArbosStorage(worldState, burner, ArbosAddresses.ArbosSystemAccount);
        ulong arbosVersion = backingStorage.GetUint64ByUint64(ArbosConstants.ArbosStateOffsets.VersionOffset);
        if (arbosVersion == 0)
        {
            throw new InvalidOperationException("ArbOS uninitialized. Call InitializeArbosStateAsync for genesis.");
        }

        return new ArbosState(backingStorage, burner, logger, arbosVersion);
    }
}
