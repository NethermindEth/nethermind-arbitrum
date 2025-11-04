// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Compression;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Arbos.Stylus;
using Nethermind.Core.Crypto;
using Nethermind.Db;
using Nethermind.Logging;
using Bytes32 = Nethermind.Arbitrum.Arbos.Stylus.Bytes32;

namespace Nethermind.Arbitrum.Stylus;

public class WasmStoreRebuilder
{
    private readonly IWasmDb _wasmDb;
    private readonly IStylusTargetConfig _targetConfig;
    private readonly StylusPrograms _programs;
    private readonly ILogger _logger;

    public WasmStoreRebuilder(
        IWasmDb wasmDb,
        IStylusTargetConfig targetConfig,
        StylusPrograms programs,
        ILogger logger)
    {
        _wasmDb = wasmDb;
        _targetConfig = targetConfig;
        _programs = programs;
        _logger = logger;
    }

    public void RebuildWasmStore(
        IDb codeDb,
        Hash256 position,
        ulong latestBlockTime,
        ulong rebuildStartBlockTime,
        bool debugMode,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<string> targets = _targetConfig.GetWasmTargets();
        DateTime lastStatusUpdate = DateTime.UtcNow;

        // Get program params from StylusPrograms
        StylusParams progParams = _programs.GetParams();

        // Iterate through all contract codes in the database
        byte[] startKey = GetCodeKey(position);

        foreach (KeyValuePair<byte[], byte[]?> entry in codeDb.GetAll(ordered: true))
        {
            if (entry.Value == null || entry.Value.Length == 0)
                continue;

            // Extract codeHash from key
            if (!TryExtractCodeHash(entry.Key, out Hash256 codeHash))
                continue;

            // Skip until we reach the start position
            if (codeHash.CompareTo(position) < 0)
                continue;

            byte[] code = entry.Value;

            // Only process Stylus programs
            if (!StylusCode.IsStylusProgram(code))
                continue;

            try
            {
                SaveActiveProgramToWasmStore(
                    codeHash,
                    code,
                    latestBlockTime,
                    rebuildStartBlockTime,
                    debugMode,
                    progParams,
                    targets);
            }
            catch (Exception ex)
            {
                if (_logger.IsWarn)
                    _logger.Warn($"Failed to save program {codeHash} during rebuild: {ex.Message}");
            }

            // Update position every second
            if (DateTime.UtcNow - lastStatusUpdate >= TimeSpan.FromSeconds(1) || cancellationToken.IsCancellationRequested)
            {
                if (_logger.IsInfo)
                    _logger.Info($"Storing rebuilding status to disk, codeHash: {codeHash}");

                _wasmDb.SetRebuildingPosition(codeHash);

                if (cancellationToken.IsCancellationRequested)
                {
                    if (_logger.IsInfo)
                        _logger.Info("Rebuilding cancelled, position saved for resumption");
                    cancellationToken.ThrowIfCancellationRequested();
                }

                lastStatusUpdate = DateTime.UtcNow;
            }
        }

        // Mark as complete
        _wasmDb.SetRebuildingPosition(WasmStoreSchema.RebuildingDone);


        if (_logger.IsInfo)
            _logger.Info("Rebuilding of wasm store was successful");
    }

    private void SaveActiveProgramToWasmStore(
        Hash256 codeHash,
        byte[] code,
        ulong latestBlockTime,
        ulong rebuildStartBlockTime,
        bool debugMode,
        StylusParams progParams,
        IReadOnlyCollection<string> targets)
    {
        ValueHash256 codeHashValue = new(codeHash.Bytes);

        // Step 1: Check if program is active (matches Nitro's getActiveProgram)
        if (!_programs.IsProgramActive(in codeHashValue, latestBlockTime, progParams))
        {
            if (_logger.IsDebug)
                _logger.Debug($"Program is not active: {codeHash}");
            return;
        }

        // Step 2: Get program data
        var programData = _programs.GetProgramInternalData(in codeHashValue, latestBlockTime);
        if (programData.version == 0)
            return;

        // Step 3: Check if activated after rebuild started
        // Matches: currentHoursSince := hoursSinceArbitrum(rebuildingStartBlockTime)
        //          if currentHoursSince < program.activatedAt { return nil }
        ulong currentHoursSince = ArbitrumTime.HoursSinceArbitrum(rebuildStartBlockTime);
        if (currentHoursSince < programData.activatedAtHours)
        {
            if (_logger.IsDebug)
                _logger.Debug($"Program {codeHash} was activated during rebuild session, skipping");
            return;
        }

        // Step 4: Get expected moduleHash from state
        // Matches: moduleHash, err := p.moduleHashes.Get(codeHash)
        ValueHash256? expectedModuleHashNullable = _programs.GetModuleHashForRebuild(in codeHashValue);
        if (expectedModuleHashNullable == null)
        {
            if (_logger.IsWarn)
                _logger.Warn($"Failed to get module hash for code hash {codeHash}");
            return;
        }
        ValueHash256 expectedModuleHash = expectedModuleHashNullable.Value;

        // Step 5: Check which targets are missing
        // Matches: _, missingTargets, err := statedb.ActivatedAsmMap(targets, moduleHash)
        List<string> missingTargets = GetMissingTargets(expectedModuleHash, targets);
        if (missingTargets.Count == 0)
        {
            if (_logger.IsDebug)
                _logger.Debug($"All targets already present for module {expectedModuleHash}");
            return;
        }

        // Step 6: Extract and decompress WASM
        byte[] wasm;
        try
        {
            wasm = GetWasmFromContractCode(code, progParams.MaxWasmSize);
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"Failed to extract WASM from code for {codeHash}: {ex.Message}");
            return;
        }

        // Step 7: Compile only missing targets
        // Matches: activateProgramInternal(..., missingTargets, ...)
        Dictionary<string, byte[]> asmMap;
        try
        {
            asmMap = CompileForTargets(
                codeHash,
                wasm,
                programData.version,
                progParams.PageLimit,
                debugMode,
                missingTargets);
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"Failed to compile program {codeHash}: {ex.Message}");
            return;
        }

        if (asmMap.Count == 0)
        {
            if (_logger.IsWarn)
                _logger.Warn($"No targets compiled successfully for {codeHash}");
            return;
        }

        // Step 8: Verify moduleHash matches (if WAVM was compiled)
        // Matches: if info != nil && info.moduleHash != moduleHash { return error }
        if (asmMap.TryGetValue(StylusTargets.WavmTargetName, out byte[]? wavmModule))
        {
            Hash256 calculatedModuleHash = new(Keccak.Compute(wavmModule));
            if (calculatedModuleHash != new Hash256(expectedModuleHash.Bytes))
            {
                if (_logger.IsError)
                    _logger.Error($"ModuleHash mismatch for {codeHash}! Expected: {expectedModuleHash}, Got: {calculatedModuleHash}");
                return;
            }
        }

        // Step 9: Write to store
        // Matches: rawdb.WriteActivation(batch, moduleHash, asmMap)
        try
        {
            _wasmDb.WriteActivation(expectedModuleHash, asmMap);

            if (_logger.IsDebug)
                _logger.Debug($"Successfully saved {asmMap.Count} target(s) for module {expectedModuleHash}");
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"Failed to write activation for {codeHash}: {ex.Message}");
            throw;
        }
    }

    private List<string> GetMissingTargets(ValueHash256 moduleHash, IReadOnlyCollection<string> allTargets)
    {
        List<string> missing = new();

        foreach (string target in allTargets)
        {
            if (!_wasmDb.TryGetActivatedAsm(target, in moduleHash, out _))
            {
                missing.Add(target);
            }
        }

        return missing;
    }

    private static byte[] GetWasmFromContractCode(byte[] code, uint maxWasmSize)
    {
        // Extract Stylus bytes
        StylusOperationResult<StylusBytes> stylusBytes = StylusCode.StripStylusPrefix(code);
        if (!stylusBytes.IsSuccess)
            throw new InvalidOperationException($"Failed to strip Stylus: {stylusBytes.Error}");

        // Decompress
        byte[] wasm = BrotliCompression.Decompress(
            stylusBytes.Value.Bytes,
            maxSize: maxWasmSize,
            stylusBytes.Value.Dictionary);

        return wasm;
    }

    private Dictionary<string, byte[]> CompileForTargets(
        Hash256 codeHash,
        byte[] wasm,
        ushort version,
        ushort pageLimit,
        bool debugMode,
        List<string> missingTargets)
    {
        Dictionary<string, byte[]> asmMap = new();

        // Zero values for gas tracking (not used during rebuild)
        ulong zeroGas = 0;
        ulong zeroArbosVersion = 0;

        foreach (string target in missingTargets)
        {
            try
            {
                if (target == StylusTargets.WavmTargetName)
                {
                    // WAVM compilation
                    StylusNativeResult<ActivateResult> result = StylusNative.Activate(
                        wasm,
                        pageLimit: pageLimit,
                        stylusVersion: version,
                        arbosVersionForGas: zeroArbosVersion,
                        debugMode,
                        new Bytes32(codeHash.Bytes),
                        ref zeroGas);

                    if (result.IsSuccess && result.Value.WavmModule != null && result.Value.WavmModule.Length > 0)
                    {
                        asmMap[target] = result.Value.WavmModule;
                    }
                    else if (_logger.IsWarn)
                    {
                        _logger.Warn($"Failed to compile WAVM for {codeHash}: {result.Status}");
                    }
                }
                else
                {
                    // Native compilation
                    StylusNativeResult<byte[]> result = StylusNative.Compile(
                        wasm,
                        version: version,
                        debugMode,
                        target);

                    if (result.IsSuccess && result.Value != null && result.Value.Length > 0)
                    {
                        asmMap[target] = result.Value;
                    }
                    else if (_logger.IsWarn)
                    {
                        _logger.Warn($"Failed to compile {target} for {codeHash}: {result.Status}");
                    }
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsWarn)
                    _logger.Warn($"Exception compiling {target} for {codeHash}: {ex.Message}");
            }
        }

        return asmMap;
    }

    private static byte[] GetCodeKey(Hash256 codeHash)
    {
        // In go-ethereum: rawdb.CodePrefix is "c" (0x63)
        byte[] key = new byte[33];
        key[0] = 0x63; // 'c' prefix
        codeHash.Bytes.CopyTo(key.AsSpan()[1..]);
        return key;
    }

    private static bool TryExtractCodeHash(byte[] key, out Hash256 codeHash)
    {
        codeHash = Keccak.Zero;

        if (key.Length != 33 || key[0] != 0x63)
            return false;

        codeHash = new Hash256(key.AsSpan()[1..33].ToArray());
        return true;
    }
}
