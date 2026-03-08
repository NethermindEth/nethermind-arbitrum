// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Config;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Blocks;
using Nethermind.Blockchain.Headers;
using Nethermind.Blockchain.Synchronization;
using Nethermind.Core.Caching;
using Nethermind.Core.Specs;
using Nethermind.Db;
using Nethermind.Db.Blooms;
using Nethermind.Logging;
using Nethermind.State.Repositories;

namespace Nethermind.Arbitrum.Core;

/// <summary>
/// Interface for BlockTree implementations that support state reset for testing.
/// Does not extend IBlockTree to avoid nullability conflicts.
/// </summary>
public interface IResettableBlockTree
{
    /// <summary>
    /// Resets the BlockTree state for testing purposes.
    /// Clears cached Genesis, Head, and another in-memory state.
    /// </summary>
    void ResetForTesting();
}

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
    : BlockTree(blockStore,
        headerStore,
        dbProvider.BlockInfosDb,
        dbProvider.MetadataDb,
        badBlockStore,
        chainLevelInfoRepository,
        specProvider,
        bloomStorage,
        syncConfig,
        logManager,
        (long)chainSpecParams.GenesisBlockNum!), IResettableBlockTree
{
    private readonly IBlockStore _blockStoreRef = blockStore;
    private readonly IHeaderStore _headerStoreRef = headerStore;
    private readonly IChainLevelInfoRepository _chainLevelInfoRef = chainLevelInfoRepository;

    /// <summary>
    /// Resets the BlockTree state for testing purposes.
    /// Only clears Genesis and caches - keeps Head intact so block processor continues working.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Thread Safety:</b> This method is NOT thread-safe. Callers MUST ensure no concurrent
    /// block production or processing is occurring. In comparison test mode, this is guaranteed
    /// by the test runner stopping Nethermind between tests.
    /// </para>
    /// <para>
    /// <b>Required Preconditions:</b>
    /// <list type="bullet">
    /// <item>No active DigestMessage calls</item>
    /// <item>No pending block production</item>
    /// <item>Processing queue is empty</item>
    /// </list>
    /// </para>
    /// </remarks>
    public void ResetForTesting()
    {
        // Reset Genesis - allows ArbitrumBlockTreeInitializer to create new genesis
        // Uses protected setter from BlockTree base class (no reflection needed)
        Genesis = null;

        // Reset internal BlockTree state (Head, BestKnownNumber, BestSuggestedHeader, etc.)
        // This is critical because after clearing databases, these cached values
        // point to blocks that no longer exist, causing block processing to fail
        ResetInternalState();

        // Clear store caches using the IClearableCache interface
        (_headerStoreRef as IClearableCache)?.ClearCache();
        (_blockStoreRef as IClearableCache)?.ClearCache();

        // Clear ChainLevelInfoRepository cache - CRITICAL for genesis re-init
        // Without this, SuggestBlock thinks the old genesis still exists
        (_chainLevelInfoRef as IClearableCache)?.ClearCache();
    }
}
