// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.IO.Abstractions;
using Nethermind.Api;
using Nethermind.Blockchain;
using Nethermind.Blockchain.FullPruning;
using Nethermind.Config;
using Nethermind.Core.Extensions;
using Nethermind.Core.Timers;
using Nethermind.Db;
using Nethermind.Db.FullPruning;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.State;
using Nethermind.Trie;
using Nethermind.Trie.Pruning;

namespace Nethermind.Arbitrum.Execution.Stateless;

/// <summary>
/// Same to default <see cref="FullPrunerFactory"/> but uses <see cref="ValidatorStatePreservingStateReader"/>
/// to wrap the provided <see cref="IStateReader"/> so that full pruning also copies validator states
/// that predate the pruning base block and thus preserves them from being pruned away.
/// </summary>
public class ArbitrumFullPrunerFactory(
    IInitConfig initConfig,
    IPruningConfig pruningConfig,
    IDbProvider dbProvider,
    IBlockTree blockTree,
    INodeStorageFactory nodeStorageFactory,
    INodeStorage mainNodeStorage,
    IProcessExitSource processExit,
    ChainSpec chainSpec,
    IFileSystem fileSystem,
    ITimerFactory timerFactory,
    CompositePruningTrigger compositePruningTrigger,
    StateReconstructor stateReconstructor,
    ReconstructedStateTrieStore reconStore,
    ILogManager logManager
) : IFullPrunerFactory
{
    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumFullPrunerFactory>();

    public FullPruner? Create(IStateReader stateReader, IPruningTrieStore trieStore)
    {
        IDb stateDb = dbProvider.StateDb;

        if (!pruningConfig.Mode.IsFull() || stateDb is not IFullPruningDb fullPruningDb)
            return null;

        string pruningDbPath = fullPruningDb.GetPath(initConfig.BaseDbPath);
        IPruningTrigger? automaticTrigger = CreateAutomaticTrigger(pruningDbPath);
        if (automaticTrigger is not null)
        {
            compositePruningTrigger.Add(automaticTrigger);
        }

        IDriveInfo? drive = fileSystem.GetDriveInfos(pruningDbPath).FirstOrDefault();
        return new FullPruner(
            fullPruningDb,
            nodeStorageFactory,
            mainNodeStorage,
            compositePruningTrigger,
            pruningConfig,
            blockTree,
            new ValidatorStatePreservingStateReader(stateReader, stateReconstructor, reconStore, logManager),
            processExit,
            ChainSizes.CreateChainSizeInfo(chainSpec.ChainId),
            drive,
            trieStore,
            logManager);
    }

    private IPruningTrigger? CreateAutomaticTrigger(string dbPath)
    {
        long threshold = pruningConfig.FullPruningThresholdMb.MB;

        switch (pruningConfig.FullPruningTrigger)
        {
            case FullPruningTrigger.StateDbSize:
                if (_logger.IsInfo)
                    _logger.Info($"Full pruning will activate when the database size reaches {threshold.SizeToString(true)} (={threshold.SizeToString()}).");
                return new PathSizePruningTrigger(dbPath, threshold, timerFactory, fileSystem);
            case FullPruningTrigger.VolumeFreeSpace:
                if (_logger.IsInfo)
                    _logger.Info($"Full pruning will activate when disk free space drops below {threshold.SizeToString(true)} (={threshold.SizeToString()}).");
                return new DiskFreeSpacePruningTrigger(dbPath, threshold, timerFactory, fileSystem);
            default:
                return null;
        }
    }
}
