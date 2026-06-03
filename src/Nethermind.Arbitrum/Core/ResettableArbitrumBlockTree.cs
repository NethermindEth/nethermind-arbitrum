// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Find;
using Nethermind.Blockchain.Visitors;
using Nethermind.Core;
using Nethermind.Core.Collections;
using Nethermind.Core.Crypto;
using Nethermind.Db;

namespace Nethermind.Arbitrum.Core;

/// <summary>
/// Decorator around <see cref="ArbitrumBlockTree"/> that supports test state reset
/// by creating a fresh inner instance instead of resetting individual fields.
/// All <see cref="IBlockTree"/> members delegate to the current inner instance.
/// Events are forwarded: on reset, the decorator unsubscribes from the old inner
/// and subscribes to the new one, so consumers see a seamless stream.
/// </summary>
public sealed class ResettableArbitrumBlockTree : IBlockTree, IArbitrumResettableBlockTree, IBlockTreeHealer
{
    private IBlockTree _inner;
    private readonly Func<ArbitrumBlockTree> _factory;

    public ResettableArbitrumBlockTree(Func<ArbitrumBlockTree> factory)
    {
        _factory = factory;
        _inner = factory();
        SubscribeToInner(_inner);
    }

    public void ResetForTesting()
    {
        UnsubscribeFromInner(_inner);
        _inner = _factory();
        SubscribeToInner(_inner);
    }

    public Hash256 HeadHash => _inner.HeadHash;
    public Hash256 GenesisHash => _inner.GenesisHash;
    public Hash256? PendingHash => _inner.PendingHash;
    public Hash256? FinalizedHash => _inner.FinalizedHash;
    public Hash256? SafeHash => _inner.SafeHash;
    public Block? Head => _inner.Head;
    public long? BestPersistedState { get => _inner.BestPersistedState; set => _inner.BestPersistedState = value; }

    public ulong NetworkId => _inner.NetworkId;
    public ulong ChainId => _inner.ChainId;
    public BlockHeader? Genesis => _inner.Genesis;
    public BlockHeader? BestSuggestedHeader => _inner.BestSuggestedHeader;
    public Block? BestSuggestedBody => _inner.BestSuggestedBody;
    public BlockHeader? BestSuggestedBeaconHeader => _inner.BestSuggestedBeaconHeader;
    public BlockHeader? LowestInsertedHeader { get => _inner.LowestInsertedHeader; set => _inner.LowestInsertedHeader = value; }
    public BlockHeader? LowestInsertedBeaconHeader { get => _inner.LowestInsertedBeaconHeader; set => _inner.LowestInsertedBeaconHeader = value; }
    public long BestKnownNumber => _inner.BestKnownNumber;
    public long BestKnownBeaconNumber => _inner.BestKnownBeaconNumber;
    public bool CanAcceptNewBlocks => _inner.CanAcceptNewBlocks;
    public (long BlockNumber, Hash256 BlockHash) SyncPivot { get => _inner.SyncPivot; set => _inner.SyncPivot = value; }
    public bool IsProcessingBlock { get => _inner.IsProcessingBlock; set => _inner.IsProcessingBlock = value; }

    public Block? FindBlock(Hash256 blockHash, BlockTreeLookupOptions options, long? blockNumber = null)
        => _inner.FindBlock(blockHash, options, blockNumber);

    public Block? FindBlock(long blockNumber, BlockTreeLookupOptions options)
        => _inner.FindBlock(blockNumber, options);

    public bool HasBlock(long blockNumber, Hash256 blockHash)
        => _inner.HasBlock(blockNumber, blockHash);

    public BlockHeader? FindHeader(Hash256 blockHash, BlockTreeLookupOptions options, long? blockNumber = null)
        => _inner.FindHeader(blockHash, options, blockNumber);

    public BlockHeader? FindHeader(long blockNumber, BlockTreeLookupOptions options)
        => _inner.FindHeader(blockNumber, options);

    public Hash256? FindBlockHash(long blockNumber)
        => _inner.FindBlockHash(blockNumber);

    public bool IsMainChain(BlockHeader blockHeader)
        => _inner.IsMainChain(blockHeader);

    public bool IsMainChain(Hash256 blockHash, bool throwOnMissingHash = true)
        => _inner.IsMainChain(blockHash, throwOnMissingHash);

    public BlockHeader FindBestSuggestedHeader()
        => _inner.FindBestSuggestedHeader();

    public long GetLowestBlock()
        => _inner.GetLowestBlock();

    public AddBlockResult Insert(BlockHeader header, BlockTreeInsertHeaderOptions headerOptions = BlockTreeInsertHeaderOptions.None)
        => _inner.Insert(header, headerOptions);

    public void BulkInsertHeader(IReadOnlyList<BlockHeader> headers, BlockTreeInsertHeaderOptions headerOptions = BlockTreeInsertHeaderOptions.None)
        => _inner.BulkInsertHeader(headers, headerOptions);

    public AddBlockResult Insert(Block block,
        BlockTreeInsertBlockOptions insertBlockOptions = BlockTreeInsertBlockOptions.None,
        BlockTreeInsertHeaderOptions insertHeaderOptions = BlockTreeInsertHeaderOptions.None,
        WriteFlags bodiesWriteFlags = WriteFlags.None)
        => _inner.Insert(block, insertBlockOptions, insertHeaderOptions, bodiesWriteFlags);

    public void UpdateHeadBlock(Hash256 blockHash)
        => _inner.UpdateHeadBlock(blockHash);

    public void NewOldestBlock(long oldestBlock)
        => _inner.NewOldestBlock(oldestBlock);

    public AddBlockResult SuggestBlock(Block block, BlockTreeSuggestOptions options = BlockTreeSuggestOptions.ShouldProcess)
        => _inner.SuggestBlock(block, options);

    public ValueTask<AddBlockResult> SuggestBlockAsync(Block block, BlockTreeSuggestOptions options = BlockTreeSuggestOptions.ShouldProcess)
        => _inner.SuggestBlockAsync(block, options);

    public AddBlockResult SuggestHeader(BlockHeader header)
        => _inner.SuggestHeader(header);

    public bool IsKnownBlock(long number, Hash256 blockHash)
        => _inner.IsKnownBlock(number, blockHash);

    public bool IsKnownBeaconBlock(long number, Hash256 blockHash)
        => _inner.IsKnownBeaconBlock(number, blockHash);

    public bool WasProcessed(long number, Hash256 blockHash)
        => _inner.WasProcessed(number, blockHash);

    public void UpdateMainChain(IReadOnlyList<Block> blocks, bool wereProcessed, bool forceHeadBlock = false)
        => _inner.UpdateMainChain(blocks, wereProcessed, forceHeadBlock);

    public void MarkChainAsProcessed(IReadOnlyList<Block> blocks)
        => _inner.MarkChainAsProcessed(blocks);

    public Task Accept(IBlockTreeVisitor blockTreeVisitor, CancellationToken cancellationToken)
        => _inner.Accept(blockTreeVisitor, cancellationToken);

    public (BlockInfo? Info, ChainLevelInfo? Level) GetInfo(long number, Hash256 blockHash)
        => _inner.GetInfo(number, blockHash);

    public ChainLevelInfo? FindLevel(long number)
        => _inner.FindLevel(number);

    public BlockInfo FindCanonicalBlockInfo(long blockNumber)
        => _inner.FindCanonicalBlockInfo(blockNumber);

    public Hash256? FindHash(long blockNumber)
        => _inner.FindHash(blockNumber);

    public IOwnedReadOnlyList<BlockHeader> FindHeaders(Hash256 hash, int numberOfBlocks, int skip, bool reverse)
        => _inner.FindHeaders(hash, numberOfBlocks, skip, reverse);

    public void DeleteInvalidBlock(Block invalidBlock)
        => _inner.DeleteInvalidBlock(invalidBlock);

    public void ReportBadBlock(Block badBlock)
        => _inner.ReportBadBlock(badBlock);

    public void DeleteOldBlock(long blockNumber, Hash256 blockHash)
        => _inner.DeleteOldBlock(blockNumber, blockHash);

    public void ForkChoiceUpdated(Hash256? finalizedBlockHash, Hash256? safeBlockBlockHash)
        => _inner.ForkChoiceUpdated(finalizedBlockHash, safeBlockBlockHash);

    public int DeleteChainSlice(in long startNumber, long? endNumber = null, bool force = false)
        => _inner.DeleteChainSlice(in startNumber, endNumber, force);

    public bool IsBetterThanHead(BlockHeader? header)
        => _inner.IsBetterThanHead(header);

    public void UpdateBeaconMainChain(IReadOnlyList<BlockInfo>? blockInfos, long clearBeaconMainChainStartPoint)
    {
        throw new NotImplementedException();
    }

    public void UpdateBeaconMainChain(BlockInfo[]? blockInfos, long clearBeaconMainChainStartPoint)
        => _inner.UpdateBeaconMainChain(blockInfos, clearBeaconMainChainStartPoint);

    public void RecalculateTreeLevels()
        => _inner.RecalculateTreeLevels();

    public event EventHandler<BlockEventArgs>? NewBestSuggestedBlock;
    public event EventHandler<BlockEventArgs>? NewSuggestedBlock;
    public event EventHandler<BlockReplacementEventArgs>? BlockAddedToMain;
    public event EventHandler<BlockEventArgs>? NewHeadBlock;
    public event EventHandler<OnUpdateMainChainArgs>? OnUpdateMainChain;
    public event EventHandler<IBlockTree.ForkChoiceUpdateEventArgs>? OnForkChoiceUpdated;

    private void SubscribeToInner(IBlockTree inner)
    {
        inner.NewBestSuggestedBlock += ForwardNewBestSuggestedBlock;
        inner.NewSuggestedBlock += ForwardNewSuggestedBlock;
        inner.BlockAddedToMain += ForwardBlockAddedToMain;
        inner.NewHeadBlock += ForwardNewHeadBlock;
        inner.OnUpdateMainChain += ForwardOnUpdateMainChain;
        inner.OnForkChoiceUpdated += ForwardOnForkChoiceUpdated;
    }

    private void UnsubscribeFromInner(IBlockTree inner)
    {
        inner.NewBestSuggestedBlock -= ForwardNewBestSuggestedBlock;
        inner.NewSuggestedBlock -= ForwardNewSuggestedBlock;
        inner.BlockAddedToMain -= ForwardBlockAddedToMain;
        inner.NewHeadBlock -= ForwardNewHeadBlock;
        inner.OnUpdateMainChain -= ForwardOnUpdateMainChain;
        inner.OnForkChoiceUpdated -= ForwardOnForkChoiceUpdated;
    }

    private void ForwardNewBestSuggestedBlock(object? sender, BlockEventArgs e) => NewBestSuggestedBlock?.Invoke(this, e);
    private void ForwardNewSuggestedBlock(object? sender, BlockEventArgs e) => NewSuggestedBlock?.Invoke(this, e);
    private void ForwardBlockAddedToMain(object? sender, BlockReplacementEventArgs e) => BlockAddedToMain?.Invoke(this, e);
    private void ForwardNewHeadBlock(object? sender, BlockEventArgs e) => NewHeadBlock?.Invoke(this, e);
    private void ForwardOnUpdateMainChain(object? sender, OnUpdateMainChainArgs e) => OnUpdateMainChain?.Invoke(this, e);
    private void ForwardOnForkChoiceUpdated(object? sender, IBlockTree.ForkChoiceUpdateEventArgs e) => OnForkChoiceUpdated?.Invoke(this, e);
    public void HealCanonicalChain(Hash256 startHash, long maxBlockDepth) { }
}
