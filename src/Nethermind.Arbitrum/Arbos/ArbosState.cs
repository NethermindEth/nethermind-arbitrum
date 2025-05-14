// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.State;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle; // For ChainSpec
using System.Numerics;
// For InvalidOperationException, Math
using Nethermind.Int256; // For Task

namespace Nethermind.Arbitrum.Arbos
{
    public class ArbosState
    {
        private readonly ArbosStorage _backingStorage;
        private readonly IBurner _burner;
        private readonly ILogger _logger;
        private readonly IArbitrumConfig _arbitrumConfig; // To access InitialArbOSVersion etc.
        private readonly ChainSpec _chainSpec; // For chain-specific parameters

        public ulong CurrentArbosVersion { get; private set; }

        // Properties for various state components using StorageBacked{Type}
        public ArbosStorageBackedUint64 UpgradeVersion { get; }
        public ArbosStorageBackedUint64 UpgradeTimestamp { get; }
        public ArbosStorageBackedAddress NetworkFeeAccount { get; }
        public ArbosStorageBackedInt256 ChainId { get; }
        public ArbosStorageBackedBytes ChainConfigStorage { get; }
        public ArbosStorageBackedUint64 GenesisBlockNum { get; }
        public ArbosStorageBackedAddress InfraFeeAccount { get; }
        public ArbosStorageBackedUint64 BrotliCompressionLevel { get; }

        // Stubs for sub-state modules - these will be fleshed out later
        // For now, they might just hold their dedicated ArbosStorage instance
        public L1PricingState L1PricingState { get; } // Assuming L1PricingState class exists
        public L2PricingState L2PricingState { get; } // Assuming L2PricingState class exists
        public RetryableState RetryableState { get; }
        public AddressTable AddressTable { get; }
        public AddressSet ChainOwners { get; }
        public MerkleAccumulator SendMerkleAccumulator { get; }
        public Blockhashes Blockhashes { get; }
        public Programs Programs { get; }
        public Features Features { get; }


        private ArbosState(ArbosStorage backingStorage, IBurner burner, ILogger logger, IArbitrumConfig arbitrumConfig, ChainSpec chainSpec,
            ulong currentArbosVersion)
        {
            _backingStorage = backingStorage;
            _burner = burner;
            _logger = logger;
            _arbitrumConfig = arbitrumConfig;
            _chainSpec = chainSpec;
            CurrentArbosVersion = currentArbosVersion;

            // Initialize StorageBacked properties
            UpgradeVersion = new ArbosStorageBackedUint64(_backingStorage, ArbosConstants.ArbosStateOffsets.UpgradeVersionOffset);
            UpgradeTimestamp = new ArbosStorageBackedUint64(_backingStorage, ArbosConstants.ArbosStateOffsets.UpgradeTimestampOffset);
            NetworkFeeAccount = new ArbosStorageBackedAddress(_backingStorage, ArbosConstants.ArbosStateOffsets.NetworkFeeAccountOffset);
            ChainId = new ArbosStorageBackedInt256(_backingStorage, ArbosConstants.ArbosStateOffsets.ChainIdOffset);
            GenesisBlockNum = new ArbosStorageBackedUint64(_backingStorage, ArbosConstants.ArbosStateOffsets.GenesisBlockNumOffset);
            InfraFeeAccount = new ArbosStorageBackedAddress(_backingStorage, ArbosConstants.ArbosStateOffsets.InfraFeeAccountOffset);
            BrotliCompressionLevel = new ArbosStorageBackedUint64(_backingStorage, ArbosConstants.ArbosStateOffsets.BrotliCompressionLevelOffset);

            ChainConfigStorage = new ArbosStorageBackedBytes(_backingStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.ChainConfigSubspace));

            // Initialize sub-state modules with their dedicated storage
            // These will be proper classes later. For now, placeholder constructors.
            L1PricingState = new L1PricingState(_backingStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.L1PricingSubspace), _logger);
            L2PricingState = new L2PricingState(_backingStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.L2PricingSubspace), _logger);
            RetryableState = new RetryableState(_backingStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.RetryablesSubspace), _logger);
            AddressTable = new AddressTable(_backingStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.AddressTableSubspace), _logger);
            ChainOwners = new AddressSet(_backingStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.ChainOwnerSubspace), _logger);
            SendMerkleAccumulator = new MerkleAccumulator(_backingStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.SendMerkleSubspace), _logger);
            Blockhashes = new Blockhashes(_backingStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.BlockhashesSubspace), _logger);
            Programs = new Programs(_backingStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.ProgramsSubspace), _logger, CurrentArbosVersion);
            Features = new Features(_backingStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.FeaturesSubspace), _logger);
        }

        public static async Task<ArbosState> OpenArbosStateAsync(IWorldState worldState, IBurner burner, ILogger logger, IArbitrumConfig arbitrumConfig,
            ChainSpec chainSpec)
        {
            var backingStorage = new ArbosStorage(worldState, burner, ArbosAddresses.ArbosSystemAccount);
            var versionStorage = new ArbosStorageBackedUint64(backingStorage, ArbosConstants.ArbosStateOffsets.VersionOffset);
            ulong currentVersion = versionStorage.Get();

            if (currentVersion == 0)
            {
                // This typically means ArbOS is uninitialized.
                // Initialization should happen in ArbitrumGenesisLoader.InitializeArbosStateAsync
                logger.Error("ArbosState.OpenArbosStateAsync: ArbOS appears uninitialized (version 0). This method expects an initialized state.");
                throw new InvalidOperationException("ArbOS uninitialized. Call InitializeArbosStateAsync for genesis.");
            }

            return new ArbosState(backingStorage, burner, logger, arbitrumConfig, chainSpec, currentVersion);
        }

        public async Task UpgradeArbosVersionAsync(ulong targetVersion, bool isFirstTime, IWorldState worldState /* for SetCode */)
        {
            _logger.Info($"ArbosState: Attempting to upgrade ArbOS from version {CurrentArbosVersion} to {targetVersion}. First time: {isFirstTime}");

            while (CurrentArbosVersion < targetVersion)
            {
                ulong nextArbosVersion = CurrentArbosVersion + 1;
                _logger.Info($"ArbosState: Upgrading to version {nextArbosVersion}");

                try
                {
                    // Precompiles are handled by Nethermind's SpecProvider based on block number/fork.
                    // We might need to ensure specific precompile addresses have *some* code if Solidity expects it.
                    // Go code: stateDB.SetCode(addr, []byte{byte(vm.INVALID)})
                    // This is less of a concern if precompiles are actual contracts or handled by EVM directly.
                    // For now, we assume Nethermind's fork mechanism handles precompile availability.

                    switch (nextArbosVersion)
                    {
                        // Versions based on go-ethereum/params/config_arbitrum.go and arbosState.go
                        case 2: // ArbosVersion_2
                            await L1PricingState.SetLastSurplusAsync(BigInteger.Zero, 1);
                            break;
                        case 3: // ArbosVersion_3
                            await L1PricingState.SetPerBatchGasCostAsync(0);
                            await L1PricingState.SetAmortizedCostCapBipsAsync(ulong.MaxValue); // MaxUint64 in Go
                            break;
                        case 4: // ArbosVersion_4
                        case 5: // ArbosVersion_5
                        case 6: // ArbosVersion_6
                        case 7: // ArbosVersion_7
                        case 8: // ArbosVersion_8
                        case 9: // ArbosVersion_9
                            // No state changes needed
                            break;
                        case 10: // ArbosVersion_10
                            // BigInteger l1PricerFunds = worldState.GetBalance(ArbosAddresses.L1PricerFundsPoolAddress);
                            // await L1PricingState.SetL1FeesAvailableAsync(l1PricerFunds);
                            // GetBalance on IWorldState returns UInt256, need to convert.
                            UInt256 balance = worldState.GetBalance(ArbosAddresses.L1PricerFundsPoolAddress);
                            await L1PricingState.SetL1FeesAvailableAsync(balance);
                            break;
                        case 11: // ArbosVersion_11 (FixRedeemGas)
                            await L1PricingState.SetPerBatchGasCostAsync(L1PricingState.InitialPerBatchGasCostV12); // Using V12 constant from Go
                            ulong oldAmortizationCap = await L1PricingState.AmortizedCostCapBipsAsync();
                            if (oldAmortizationCap == ulong.MaxValue)
                            {
                                await L1PricingState.SetAmortizedCostCapBipsAsync(0);
                            }

                            if (!isFirstTime)
                            {
                                ChainOwners.Clear();
                            }

                            break;

                        // Versions 12-19 are for Orbit chains
                        case 12:
                        case 13:
                        case 14:
                        case 15:
                        case 16:
                        case 17:
                        case 18:
                        case 19:
                            _logger.Info($"ArbosState: Orbit chain custom upgrade for version {nextArbosVersion}. No specific actions implemented in core.");
                            break;

                        case 20: // ArbosVersion_20
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
                            _logger.Info($"ArbosState: Orbit chain custom upgrade for version {nextArbosVersion}. No specific actions implemented in core.");
                            break;

                        case 30: // ArbosVersion_30 (Stylus)
                            await Programs.InitializeAsync(nextArbosVersion, _backingStorage.OpenSubStorage(ArbosConstants.ArbosSubspaceIDs.ProgramsSubspace), _logger);
                            break;

                        case 31: // ArbosVersion_31 (StylusFixes)
                            var programsParamsV1 = await Programs.GetParamsAsync();
                            await programsParamsV1.UpgradeToVersionAsync(2); // Assuming ProgramsParams class with this method
                            await programsParamsV1.SaveAsync();
                            break;

                        case 32: // ArbosVersion_32 (StylusChargingFixes)
                            // No state changes needed
                            break;

                        // Versions 33-39 are for Orbit chains
                        case 33:
                        case 34:
                        case 35:
                        case 36:
                        case 37:
                        case 38:
                        case 39:
                            _logger.Info($"ArbosState: Orbit chain custom upgrade for version {nextArbosVersion}. No specific actions implemented in core.");
                            break;

                        case 40: // ArbosVersion_40
                            var programsParamsV2 = await Programs.GetParamsAsync();
                            await programsParamsV2.UpgradeToArbosVersionAsync(nextArbosVersion);
                            await programsParamsV2.SaveAsync();
                            break;

                        default:
                            var errMsg = $"ArbosState: Chain is upgrading to unsupported ArbOS version {nextArbosVersion}. Please upgrade node software.";
                            _logger.Error(errMsg);
                            throw new NotSupportedException(errMsg);
                    }
                }
                catch (Exception ex)
                {
                    var errMsg = $"ArbosState: Failed to upgrade ArbOS from version {CurrentArbosVersion} to {nextArbosVersion}.";
                    _logger.Error(errMsg, ex);
                    _burner.Restrict(ex); // Notify burner of failure
                    await _burner.HandleErrorAsync(new InvalidOperationException(errMsg, ex)); // Propagate error through burner
                    throw; // Re-throw to halt the process
                }

                CurrentArbosVersion = nextArbosVersion;
                // Update ArbosVersion in Programs if it's a separate field there
                if (Programs != null) Programs.ArbosVersion = nextArbosVersion;
            }

            if (isFirstTime && targetVersion >= 6) // ArbosVersion_6
            {
                if (targetVersion < 11) // ArbosVersion_11
                {
                    await L1PricingState.SetPerBatchGasCostAsync(L1PricingState.InitialPerBatchGasCostV6);
                }

                await L1PricingState.SetEquilibrationUnitsAsync(L1PricingState.InitialEquilibrationUnitsV6);
                await L2PricingState.SetSpeedLimitPerSecondAsync(L2PricingState.InitialSpeedLimitPerSecondV6);
                await L2PricingState.SetMaxPerBlockGasLimitAsync(L2PricingState.InitialPerBlockGasLimitV6);
            }

            // Persist the final upgraded version
            var versionStorage = new ArbosStorageBackedUint64(_backingStorage, ArbosConstants.ArbosStateOffsets.VersionOffset);
            versionStorage.Set(CurrentArbosVersion);

            _logger.Info($"ArbosState: Successfully upgraded ArbOS to version {CurrentArbosVersion}.");
        }

        public void SetBrotliCompressionLevelAsync(ulong level)
        {
            // Add validation if necessary, e.g., level <= arbcompress.LEVEL_WELL from Go
            BrotliCompressionLevel.Set(level);
        }
    }
}
