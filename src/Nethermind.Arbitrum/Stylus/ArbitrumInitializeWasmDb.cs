// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Autofac.Features.AttributeFilters;
using Nethermind.Api.Steps;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Db;
using Nethermind.Init.Steps;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Stylus;

[RunnerStepDependencies(typeof(InitializeBlockchain))]
public class ArbitrumInitializeWasmDb(
    IWasmDb wasmDb,
    IWasmStore wasmStore,
    [KeyFilter("code")] IDb codeDb,
    IBlockTree blockTree,
    IArbitrumConfig config,
    IStylusTargetConfig stylusConfig,
    ArbitrumChainSpecEngineParameters chainSpecEngineParameters,
    StylusParams stylusParams,
    ILogManager logManager)
    : IStep
{
    private readonly IWasmDb _wasmDb = wasmDb ?? throw new ArgumentNullException(nameof(wasmDb));
    private readonly IWasmStore _wasmStore = wasmStore ?? throw new ArgumentNullException(nameof(wasmStore));
    private readonly IDb _codeDb = codeDb ?? throw new ArgumentNullException(nameof(codeDb));
    private readonly IBlockTree _blockTree = blockTree ?? throw new ArgumentNullException(nameof(blockTree));
    private readonly IArbitrumConfig _config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly IStylusTargetConfig _stylusConfig = stylusConfig ?? throw new ArgumentNullException(nameof(stylusConfig));
    private readonly ArbitrumChainSpecEngineParameters _chainSpecEngineParameters = chainSpecEngineParameters ?? throw new ArgumentNullException(nameof(chainSpecEngineParameters));
    private readonly StylusParams _stylusParams = stylusParams ?? throw new ArgumentNullException(nameof(stylusParams));
    private readonly ILogger _logger = logManager?.GetClassLogger<ArbitrumInitializeWasmDb>() ?? throw new ArgumentNullException(nameof(logManager));

    public Task Execute(CancellationToken cancellationToken)
    {
        // Upgrade versions if needed (matches validateOrUpgradeWasmerSerializeVersion and validateOrUpgradeWasmStoreSchemaVersion)
        UpgradeWasmerSerializeVersion(_wasmDb);
        UpgradeWasmSerializeVersion(_wasmDb);

        // Rebuild local WASM store if needed (matches rebuildLocalWasm)
        RebuildLocalWasm(cancellationToken);

        WasmStore.Initialize(_wasmStore);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates and upgrades Wasmer serialize version.
    /// Equivalent to validateOrUpgradeWasmerSerializeVersion in Nitro (cmd/nitro/init.go:397)
    /// </summary>
    private void UpgradeWasmerSerializeVersion(IWasmDb store)
    {
        if (store.IsEmpty())
            return;

        uint versionInDb = store.GetWasmerSerializeVersion();
        if (versionInDb == WasmStoreSchema.WasmerSerializeVersion)
            return;

        if (_logger.IsWarn)
            _logger.Warn($"Detected wasmer serialize version {versionInDb}, expected version {WasmStoreSchema.WasmerSerializeVersion} - removing old wasm entries");

        IReadOnlyList<ReadOnlyMemory<byte>> prefixes = WasmStoreSchema.WasmPrefixesExceptWavm();
        DeleteWasmResult result = store.DeleteWasmEntries(prefixes);

        if (_logger.IsInfo)
            _logger.Info($"Wasm entries successfully removed. Deleted: {result.DeletedCount}");

        store.SetWasmerSerializeVersion(WasmStoreSchema.WasmerSerializeVersion);
    }

    /// <summary>
    /// Validates and upgrades WASM store schema version.
    /// Equivalent to validateOrUpgradeWasmStoreSchemaVersion in Nitro (cmd/nitro/init.go:415)
    /// </summary>
    private void UpgradeWasmSerializeVersion(IWasmDb store)
    {
        if (!store.IsEmpty())
        {
            byte version = store.GetWasmSchemaVersion();

            if (version > WasmStoreSchema.WasmSchemaVersion)
                throw new InvalidOperationException(
                    $"Unsupported wasm database schema version, current version: {WasmStoreSchema.WasmSchemaVersion}, read from wasm database: {version}");

            // Special step for upgrading from version 0 - remove all entries added in version 0
            if (version == 0)
            {
                if (_logger.IsWarn)
                    _logger.Warn("Detected wasm store schema version 0 - removing all old wasm store entries");

                (IReadOnlyList<ReadOnlyMemory<byte>> prefixes, int keyLength) = WasmStoreSchema.DeprecatedPrefixesV0();
                DeleteWasmResult result = store.DeleteWasmEntries(prefixes, keyLength);

                if (result.KeyLengthMismatchCount > 0 && _logger.IsWarn)
                    _logger.Warn($"Found {result.KeyLengthMismatchCount} keys with deprecated prefix but not matching length, skipping removal.");

                if (_logger.IsInfo)
                    _logger.Info($"Wasm store schema version 0 entries successfully removed. Deleted: {result.DeletedCount}");
            }
        }

        store.SetWasmSchemaVersion(WasmStoreSchema.WasmSchemaVersion);
    }

    /// <summary>
    /// Rebuild local WASM store - exact port of rebuildLocalWasm from nitro/cmd/nitro/init.go:556
    /// </summary>
    private void RebuildLocalWasm(CancellationToken cancellationToken)
    {
        Block? latestBlock = _blockTree.Head;

        // If there is only genesis block or no blocks in the blockchain, set Rebuilding of wasm store to Done
        // If Stylus upgrade hasn't yet happened, skipping rebuilding of wasm store
        if (ShouldMarkRebuildingAsDone(latestBlock))
        {
            if (_logger.IsInfo)
                _logger.Info("Setting rebuilding of wasm store to done");

            _wasmDb.SetRebuildingPosition(WasmStoreSchema.RebuildingDone);
            return;
        }

        // Check rebuild mode configuration (matches rebuildMode != "false" check in Nitro)
        string rebuildMode = _config.RebuildLocalWasm ?? "auto";
        if (rebuildMode.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            if (_logger.IsDebug)
                _logger.Debug("WASM store rebuilding disabled by configuration");
            return;
        }

        // Get or initialize rebuilding position
        Hash256 position = InitializeRebuildPosition(rebuildMode);

        // If already done, return (matches position != gethexec.RebuildingDone check in Nitro)
        if (WasmStoreSchema.IsRebuildingDone(position))
        {
            if (_logger.IsInfo)
                _logger.Info("WASM store rebuilding already completed");
            return;
        }

        // Get or initialize start block hash
        Hash256 startBlockHash = InitializeStartBlockHash(latestBlock!);

        if (_logger.IsInfo)
            _logger.Info($"Starting or continuing rebuilding of wasm store, codeHash: {position}, startBlockHash: {startBlockHash}");

        try
        {
            // Get timestamps for rebuild
            ulong latestBlockTime = latestBlock!.Timestamp;
            Block? startBlock = _blockTree.FindBlock(startBlockHash);
            ulong rebuildStartBlockTime = startBlock?.Timestamp ?? latestBlockTime;

            // Determine debug mode
            // TODO: Get from chain config if available
            bool debugMode = false;

            // Create rebuilder and execute
            WasmStoreRebuilder rebuilder = new(_wasmDb, _stylusConfig, _stylusParams, _logger);

            // Execute the rebuild
            rebuilder.RebuildWasmStore(
                _codeDb,
                position,
                latestBlockTime,
                rebuildStartBlockTime,
                debugMode,
                cancellationToken);

            if (_logger.IsInfo)
                _logger.Info("WASM store rebuilding completed successfully");
        }
        catch (OperationCanceledException)
        {
            if (_logger.IsInfo)
                _logger.Info("WASM store rebuilding was cancelled, progress saved");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error rebuilding of wasm store: {ex.Message}", ex);
            throw new InvalidOperationException($"Error rebuilding wasm store: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks if rebuilding should be skipped and marked as done.
    /// Returns true if:
    /// - There is only genesis block or no blocks in the blockchain
    /// - Stylus upgrade hasn't yet happened (ArbOS version less than Stylus version)
    /// Matches the logic in rebuildLocalWasm (lines 556-565 in Nitro)
    /// </summary>
    private bool ShouldMarkRebuildingAsDone(Block? latestBlock)
    {
        // No blocks in the blockchain
        if (latestBlock == null)
            return true;

        // Only genesis block (matches: latestBlock.Number.Uint64() <= l2BlockChain.Config().ArbitrumChainParams.GenesisBlockNum)
        if (latestBlock.Number <= (long)_chainSpecEngineParameters.GenesisBlockNum!)
            return true;

        // Check if Stylus upgrade has happened
        // Matches: types.DeserializeHeaderExtraInformation(latestBlock).ArbOSFormatVersion < params.ArbosVersion_Stylus
        ulong arbosFormatVersion = GetArbOSFormatVersion(latestBlock);
        if (arbosFormatVersion < WasmStoreSchema.ArbosVersionStylus)
        {
            if (_logger.IsInfo)
                _logger.Info($"Stylus upgrade hasn't yet happened (ArbOS format version: {arbosFormatVersion}), skipping rebuilding of wasm store");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Initialize rebuild position based on mode.
    /// Handles "force" mode which resets position to beginning.
    /// Matches the logic in rebuildLocalWasm (lines 571-587 in Nitro)
    /// </summary>
    private Hash256 InitializeRebuildPosition(string rebuildMode)
    {
        // Force mode - reset to beginning
        if (rebuildMode.Equals("force", StringComparison.OrdinalIgnoreCase))
        {
            if (_logger.IsInfo)
                _logger.Info("Commencing force rebuilding of wasm store by setting codehash position in rebuilding to beginning");

            _wasmDb.SetRebuildingPosition(Keccak.Zero);
            return Keccak.Zero;
        }

        // Try to read existing position
        Hash256? position = _wasmDb.GetRebuildingPosition();
        if (position == null)
        {
            if (_logger.IsInfo)
                _logger.Info("Unable to get codehash position in rebuilding of wasm store, its possible it isn't initialized yet, so initializing it and starting rebuilding");

            _wasmDb.SetRebuildingPosition(Keccak.Zero);
            return Keccak.Zero;
        }

        return position;
    }

    /// <summary>
    /// Initialize start block hash for rebuilding.
    /// Uses latest block hash if not already set.
    /// Matches the logic in rebuildLocalWasm (lines 589-598 in Nitro)
    /// </summary>
    private Hash256 InitializeStartBlockHash(Block latestBlock)
    {
        Hash256? startBlockHash = _wasmDb.GetRebuildingStartBlockHash();
        if (startBlockHash == null)
        {
            if (_logger.IsInfo)
                _logger.Info("Unable to get start block hash in rebuilding of wasm store, its possible it isn't initialized yet, so initializing it to latest block hash");

            _wasmDb.SetRebuildingStartBlockHash(latestBlock.Hash!);
            return latestBlock.Hash!;
        }

        return startBlockHash;
    }

    /// <summary>
    /// Extract ArbOS format version from block header extra data.
    /// Equivalent to types.DeserializeHeaderExtraInformation(block).ArbOSFormatVersion in Nitro.
    ///
    /// The extra data in Arbitrum blocks contains:
    /// - ArbOS format version (byte)
    /// - Various other ArbOS-specific information
    /// </summary>
    private ulong GetArbOSFormatVersion(Block block)
    {
        ArbitrumBlockHeaderInfo currentInfo = ArbitrumBlockHeaderInfo.Deserialize(block.Header, _logger);
        return currentInfo.ArbOSFormatVersion;
    }
}
