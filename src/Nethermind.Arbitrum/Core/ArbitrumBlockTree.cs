// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Config;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Blocks;
using Nethermind.Blockchain.Headers;
using Nethermind.Blockchain.Synchronization;
using Nethermind.Core.Specs;
using Nethermind.Db;
using Nethermind.Db.Blooms;
using Nethermind.Logging;
using Nethermind.State.Repositories;
using IBlockAccessListStore = Nethermind.Blockchain.Headers.IBlockAccessListStore;

namespace Nethermind.Arbitrum.Core;

/// <summary>
/// Interface for Arbitrum BlockTree implementations that support state reset for testing.
/// Implemented by <see cref="ResettableArbitrumBlockTree"/> decorator.
/// </summary>
public interface IArbitrumResettableBlockTree
{
    /// <summary>
    /// Resets the BlockTree state for testing purposes.
    /// The decorator implementation creates a fresh inner BlockTree instance.
    /// </summary>
    void ResetForTesting();
}

public class ArbitrumBlockTree(
    IBlockStore blockStore,
    IHeaderStore headerStore,
    IDbProvider dbProvider,
    IBadBlockStore badBlockStore,
    IBlockAccessListStore blockAccessListStore,
    IChainLevelInfoRepository chainLevelInfoRepository,
    ISpecProvider specProvider,
    IBloomStorage bloomStorage,
    ISyncConfig syncConfig,
    ILogManager logManager,
    ArbitrumChainSpecEngineParameters chainSpecParams)
    : BlockTree(blockStore,
        headerStore,
        dbProvider.BlockInfosDb,
        dbProvider.MetadataDb,
        badBlockStore,
        blockAccessListStore,
        chainLevelInfoRepository,
        specProvider,
        bloomStorage,
        syncConfig,
        logManager,
        (long)chainSpecParams.GenesisBlockNum!);
