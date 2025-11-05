// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Compression;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Core.Crypto;
using Nethermind.Db;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Stylus;

public class WasmStoreRebuilder(
    IWasmDb wasmDb,
    IStylusTargetConfig targetConfig,
    StylusPrograms programs,
    ILogger logger)
{
    public void RebuildWasmStore(
        IDb codeDb,
        Hash256 position,
        ulong latestBlockTime,
        ulong rebuildStartBlockTime,
        bool debugMode,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<string> targets = targetConfig.GetWasmTargets();
        DateTime lastStatusUpdate = DateTime.UtcNow;

        // Get program params from StylusPrograms
        StylusParams progParams = programs.GetParams();

        foreach ((byte[] key, byte[]? code) in codeDb.GetAll(ordered: true))
        {
            if (code == null || code.Length == 0)
                continue;

            // Extract codeHash from key
            if (!TryExtractCodeHash(key, out Hash256 codeHash))
                continue;

            // Skip until we reach the start position
            if (codeHash.CompareTo(position) < 0)
                continue;

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
                if (logger.IsWarn)
                    logger.Warn($"Failed to save program {codeHash} during rebuild: {ex.Message}");
            }

            // Update position every second
            if (DateTime.UtcNow - lastStatusUpdate >= TimeSpan.FromSeconds(1) || cancellationToken.IsCancellationRequested)
            {
                if (logger.IsInfo)
                    logger.Info($"Storing rebuilding status to disk, codeHash: {codeHash}");

                wasmDb.SetRebuildingPosition(codeHash);

                if (cancellationToken.IsCancellationRequested)
                {
                    if (logger.IsInfo)
                        logger.Info("Rebuilding cancelled, position saved for resumption");
                    cancellationToken.ThrowIfCancellationRequested();
                }

                lastStatusUpdate = DateTime.UtcNow;
            }
        }

        // Mark as complete
        wasmDb.SetRebuildingPosition(WasmStoreSchema.RebuildingDone);


        if (logger.IsInfo)
            logger.Info("Rebuilding of wasm store was successful");
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

        // Step 1: Check if program is active
        if (!programs.IsProgramActive(in codeHashValue, latestBlockTime, progParams))
        {
            if (logger.IsDebug)
                logger.Debug($"Program is not active: {codeHash}");
            return;
        }

        // Step 2: Get program data
        (ushort version, uint activatedAtHours, ulong ageSeconds, bool cached) programData = programs.GetProgramInternalData(in codeHashValue, latestBlockTime);
        if (programData.version == 0)
            return;

        // Step 3: Check if activated after rebuild started
        ulong currentHoursSince = ArbitrumTime.HoursSinceArbitrum(rebuildStartBlockTime);
        if (currentHoursSince < programData.activatedAtHours)
        {
            if (logger.IsDebug)
                logger.Debug($"Program {codeHash} was activated during rebuild session, skipping");
            return;
        }

        // Step 4: Get expected moduleHash from state
        ValueHash256? expectedModuleHashNullable = programs.GetModuleHashForRebuild(in codeHashValue);
        if (expectedModuleHashNullable == null)
        {
            if (logger.IsWarn)
                logger.Warn($"Failed to get module hash for code hash {codeHash}");
            return;
        }
        ValueHash256 expectedModuleHash = expectedModuleHashNullable.Value;

        // Step 5: Check which targets are missing
        List<string> missingTargets = GetMissingTargets(expectedModuleHash, targets);
        if (missingTargets.Count == 0)
        {
            if (logger.IsDebug)
                logger.Debug($"All targets already present for module {expectedModuleHash}");
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
            if (logger.IsError)
                logger.Error($"Failed to extract WASM from code for {codeHash}: {ex.Message}");
            return;
        }

        // Step 7: Recompile using existing ActivateProgramInternal
        ulong zeroArbosVersion = 0;
        IBurner zeroGasBurner = new ZeroGasBurner();

        StylusOperationResult<StylusPrograms.StylusActivationResult> activationResult =
            StylusPrograms.ActivateProgramInternal(
                in codeHashValue,
                wasm,
                progParams.PageLimit,
                programData.version,
                zeroArbosVersion,
                debugMode,
                zeroGasBurner,
                missingTargets,
                activationIsMandatory: false);

        if (!activationResult.IsSuccess)
        {
            if (logger.IsError)
                logger.Error($"Failed to compile program {codeHash}: {activationResult.Error}");
            return;
        }

        (StylusPrograms.StylusActivationInfo? info, IReadOnlyDictionary<string, byte[]> asmMap) = activationResult.Value;

        // Step 8: Verify moduleHash matches (warning only during rebuild)
        if (info.HasValue && info.Value.ModuleHash != expectedModuleHash)
        {
            if (logger.IsWarn)
                logger.Warn($"ModuleHash mismatch for {codeHash} during rebuild. Expected: {expectedModuleHash}, Got: {info.Value.ModuleHash}");
            // Continue despite mismatch during rebuild
        }

        if (asmMap.Count == 0)
        {
            if (logger.IsWarn)
                logger.Warn($"No targets compiled successfully for {codeHash}");
            return;
        }

        // Step 9: Write to store
        try
        {
            wasmDb.WriteActivation(expectedModuleHash, asmMap);

            if (logger.IsDebug)
                logger.Debug($"Successfully saved {asmMap.Count} target(s) for module {expectedModuleHash}");
        }
        catch (Exception ex)
        {
            if (logger.IsError)
                logger.Error($"Failed to write activation for {codeHash}: {ex.Message}");
            throw;
        }
    }

    private List<string> GetMissingTargets(ValueHash256 moduleHash, IReadOnlyCollection<string> allTargets)
    {
        List<string> missing = [];
        missing.AddRange(allTargets.Where(target => !wasmDb.TryGetActivatedAsm(target, in moduleHash, out _)));
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
