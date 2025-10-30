// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

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

namespace Nethermind.Arbitrum.Core;

public class ArbitrumBlockTree(
    IBlockStore blockStore,
    IHeaderStore headerStore,
    IDbProvider dbProvider,
    IBadBlockStore badBlockStore,
    IChainLevelInfoRepository chainLevelInfoRepository,
    ISpecProvider specProvider,
    IBloomStorage bloomStorage,
    ISyncConfig syncConfig,
    ILogManager logManager,
    ArbitrumChainSpecEngineParameters chainSpecParams)
    : BlockTree(
        blockStore,
        headerStore,
        dbProvider.BlockInfosDb,
        dbProvider.MetadataDb,
        badBlockStore,
        chainLevelInfoRepository,
        specProvider,
        bloomStorage,
        syncConfig,
        logManager,
        (long)chainSpecParams.GenesisBlockNum!)
{
}
