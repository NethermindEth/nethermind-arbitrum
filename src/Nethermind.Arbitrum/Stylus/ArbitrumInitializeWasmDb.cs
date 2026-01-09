// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Autofac.Features.AttributeFilters;
using Nethermind.Api.Steps;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Evm;
using Nethermind.Blockchain;
using Nethermind.Consensus.Processing;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Db;
using Nethermind.Evm.State;
using Nethermind.Init.Steps;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Stylus;

[RunnerStepDependencies([typeof(InitializeBlockchain)], [typeof(StartBlockProcessor)])]
public class ArbitrumInitializeWasmDb(
    IWasmDb wasmDb,
    [KeyFilter("code")] IDb codeDb,
    IBlockTree blockTree,
    IArbitrumConfig config,
    IStylusTargetConfig stylusConfig,
    ArbitrumChainSpecEngineParameters chainSpecEngineParameters,
    IMainProcessingContext<ArbitrumGas> mainProcessingContext,
    ILogManager? logManager)
    : IStep
{
    private readonly ILogger _logger = logManager?.GetClassLogger<ArbitrumInitializeWasmDb>() ?? throw new ArgumentNullException(nameof(logManager));

    public Task Execute(CancellationToken cancellationToken)
    {
        UpgradeWasmerSerializeVersion(wasmDb);
        UpgradeWasmSerializeVersion(wasmDb);
        RebuildLocalWasm(cancellationToken);

        return Task.CompletedTask;
    }

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

    private void UpgradeWasmSerializeVersion(IWasmDb store)
    {
        if (!store.IsEmpty())
        {
            byte version = store.GetWasmSchemaVersion();

            if (version > WasmStoreSchema.WasmSchemaVersion)
                throw new InvalidOperationException(
                    $"Unsupported wasm database schema version, current version: {WasmStoreSchema.WasmSchemaVersion}, read from wasm database: {version}");

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

    private void RebuildLocalWasm(CancellationToken cancellationToken)
    {
        Block? latestBlock = blockTree.Head;

        if (ShouldMarkRebuildingAsDone(latestBlock))
        {
            if (_logger.IsInfo)
                _logger.Info("Setting rebuilding of wasm store to done");

            wasmDb.SetRebuildingPosition(WasmStoreSchema.RebuildingDone);
            return;
        }

        WasmRebuildMode rebuildMode = config.RebuildLocalWasm;
        if (rebuildMode.Equals(WasmRebuildMode.False))
        {
            if (_logger.IsDebug)
                _logger.Debug("WASM store rebuilding disabled by configuration");
            return;
        }

        Hash256 position = InitializeRebuildPosition(rebuildMode);

        if (WasmStoreSchema.IsRebuildingDone(position))
        {
            if (_logger.IsInfo)
                _logger.Info("WASM store rebuilding already completed");
            return;
        }

        Hash256 startBlockHash = InitializeStartBlockHash(latestBlock!);

        if (_logger.IsInfo)
            _logger.Info($"Starting or continuing rebuilding of wasm store, codeHash: {position}, startBlockHash: {startBlockHash}");

        try
        {
            ulong latestBlockTime = latestBlock!.Timestamp;
            Block? startBlock = blockTree.FindBlock(startBlockHash);
            ulong rebuildStartBlockTime = startBlock?.Timestamp ?? latestBlockTime;

            bool debugMode = chainSpecEngineParameters.AllowDebugPrecompiles ?? false;

            IWorldState worldState = mainProcessingContext.WorldState;

            using IDisposable scope = worldState.BeginScope(latestBlock.Header);
            ArbosState arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), _logger);
            StylusPrograms programs = arbosState.Programs;

            WasmStoreRebuilder rebuilder = new(wasmDb, stylusConfig, programs, _logger);

            rebuilder.RebuildWasmStore(
                codeDb,
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
            throw;
        }
    }

    private bool ShouldMarkRebuildingAsDone(Block? latestBlock)
    {
        if (latestBlock == null)
            return true;

        if (latestBlock.Number <= (long)chainSpecEngineParameters.GenesisBlockNum!)
            return true;

        // Check if Stylus upgrade has happened
        ulong arbosFormatVersion = GetArbOSFormatVersion(latestBlock);
        if (arbosFormatVersion < ArbosVersion.Thirty)
        {
            if (_logger.IsInfo)
                _logger.Info($"Stylus upgrade hasn't yet happened (ArbOS format version: {arbosFormatVersion}), skipping rebuilding of wasm store");
            return true;
        }

        return false;
    }

    private Hash256 InitializeRebuildPosition(WasmRebuildMode rebuildMode)
    {
        // Force mode - reset to beginning
        if (rebuildMode.Equals(WasmRebuildMode.Force))
        {
            if (_logger.IsInfo)
                _logger.Info("Commencing force rebuilding of wasm store by setting codehash position in rebuilding to beginning");

            wasmDb.SetRebuildingPosition(Keccak.Zero);
            return Keccak.Zero;
        }

        Hash256? position = wasmDb.GetRebuildingPosition();
        if (position != null)
        {
            return position;
        }

        if (_logger.IsInfo)
            _logger.Info("Unable to get codehash position in rebuilding of wasm store, its possible it isn't initialized yet, so initializing it and starting rebuilding");

        wasmDb.SetRebuildingPosition(Keccak.Zero);
        return Keccak.Zero;

    }

    private Hash256 InitializeStartBlockHash(Block latestBlock)
    {
        Hash256? startBlockHash = wasmDb.GetRebuildingStartBlockHash();
        if (startBlockHash != null)
        {
            return startBlockHash;
        }

        if (_logger.IsInfo)
            _logger.Info("Unable to get start block hash in rebuilding of wasm store, its possible it isn't initialized yet, so initializing it to latest block hash");

        wasmDb.SetRebuildingStartBlockHash(latestBlock.Hash!);
        return latestBlock.Hash!;

    }

    private ulong GetArbOSFormatVersion(Block block)
    {
        ArbitrumBlockHeaderInfo currentInfo = ArbitrumBlockHeaderInfo.Deserialize(block.Header, _logger);
        return currentInfo.ArbOSFormatVersion;
    }
}
