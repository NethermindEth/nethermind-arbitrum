// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Api.Steps;
using Nethermind.Init.Steps;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Stylus;

[RunnerStepDependencies(typeof(InitializeBlockchain))]
public class ArbitrumInitializeWasmStore(IWasmDb wasmDb, ILogManager logManager) : IStep
{
    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumInitializeWasmStore>();

    public Task Execute(CancellationToken cancellationToken)
    {
        WasmStore wasmStore = new(wasmDb, new StylusTargetConfig(), cacheTag: 1);
        UpgradeWasmerSerializeVersion(wasmDb);
        UpgradeWasmSerializeVersion(wasmDb);
        RebuildWasmDb();

        // TODO: This one should be resolved through DI and not hardcoded static. Fix it when we know how to pass it to Arbos.
        WasmStore.Initialize(wasmStore);

        return Task.CompletedTask;
    }

    private void UpgradeWasmerSerializeVersion(IWasmDb store)
    {
        if (store.IsEmpty())
            return;

        uint wasmerVersion = store.GetWasmerSerializeVersion();
        if (wasmerVersion == WasmStoreSchema.WasmerSerializeVersion)
            return;

        if (_logger.IsWarn)
            _logger.Warn($"Detected wasmer serialize version {wasmerVersion}, expected {WasmStoreSchema.WasmerSerializeVersion} - removing old wasm entries");

        IReadOnlyList<ReadOnlyMemory<byte>> prefixes = WasmStoreSchema.WasmPrefixesExceptWavm();

        store.DeleteWasmEntries(prefixes);
        store.SetWasmerSerializeVersion(WasmStoreSchema.WasmerSerializeVersion);
    }

    private void UpgradeWasmSerializeVersion(IWasmDb store)
    {
        if (!store.IsEmpty())
        {
            byte version = store.GetWasmSchemaVersion();
            if (version > WasmStoreSchema.WasmSchemaVersion)
                throw new InvalidOperationException(
                    $"Unsupported wasm database schema version, current version: {version}, expected: {WasmStoreSchema.WasmSchemaVersion}");

            if (version == 0)
            {
                if (_logger.IsWarn)
                    _logger.Warn("Detected wasm store schema version 0 - removing all old wasm store entries");

                (IReadOnlyList<ReadOnlyMemory<byte>> prefixes, int keyLength) = WasmStoreSchema.DeprecatedPrefixesV0();
                DeleteWasmResult result = store.DeleteWasmEntries(prefixes, keyLength);
                if (result.KeyLengthMismatchCount > 0 && _logger.IsWarn)
                    _logger.Warn($"Found {result.KeyLengthMismatchCount} keys with deprecated prefix but not matching length. " +
                                 $"Deleted {result.DeletedCount} entries.");

                if (_logger.IsInfo)
                    _logger.Info("Wasm store schema version 0 entries successfully removed.");
            }
        }

        store.SetWasmSchemaVersion(WasmStoreSchema.WasmSchemaVersion);
    }

    private void RebuildWasmDb()
    {
        // TODO: Implement rebuilding of the WASM store cmd/nitro/init.go:rebuildLocalWasm
    }
}
