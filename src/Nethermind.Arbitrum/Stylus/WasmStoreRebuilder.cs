using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Core.Crypto;
using Nethermind.Db;
using Nethermind.Logging;

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

        // TODO: Optimize - iterating all contract codes could be slow. Consider maintaining a Stylus-specific index.
        foreach ((byte[] key, byte[]? code) in codeDb.GetAll(ordered: true))
        {
            if (code == null || code.Length == 0)
                continue;

            // Nethermind CodeDB keys are just the code hash (32 bytes)
            if (key.Length != 32)
                continue;

            Hash256 codeHash = new(key);

            if (codeHash.CompareTo(position) < 0)
                continue;

            if (!StylusCode.IsStylusProgram(code))
                continue;

            try
            {
                programs.SaveActiveProgramForRebuild(
                    new ValueHash256(codeHash.Bytes),
                    code,
                    latestBlockTime,
                    rebuildStartBlockTime,
                    debugMode,
                    targets,
                    wasmDb,
                    logger);
            }
            catch (Exception ex)
            {
                if (logger.IsWarn)
                    logger.Warn($"Failed to save program {codeHash} during rebuild: {ex.Message}");
            }

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

        wasmDb.SetRebuildingPosition(WasmStoreSchema.RebuildingDone);

        if (logger.IsInfo)
            logger.Info("Rebuilding of wasm store was successful");
    }
}

public enum WasmRebuildMode
{
    Auto,
    Force,
    False
}
