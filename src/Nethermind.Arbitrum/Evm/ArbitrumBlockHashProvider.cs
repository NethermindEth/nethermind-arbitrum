// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Blockchain;
using Nethermind.Blockchain.Blocks;
using Nethermind.Blockchain.Find;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Evm;

public class ArbitrumBlockhashProvider(
    IBlockFinder blockTree,
    IWorldState worldState,
    ILogManager? logManager)
    : IBlockhashProvider
{
    public const int MaxDepth = 256;
    private readonly IBlockhashStore _blockhashStore = new BlockhashStore(worldState);
    private readonly ILogger _logger = logManager?.GetClassLogger() ?? throw new ArgumentNullException(nameof(logManager));

    public Hash256? GetBlockhash(BlockHeader currentBlock, long number, IReleaseSpec? spec)
    {
        if (spec?.IsBlockHashInStateAvailable is true)
        {
            return _blockhashStore.GetBlockHashFromState(currentBlock, number, spec);
        }

        long current = currentBlock.Number;
        if (number >= current || number < current - System.Math.Min(current, MaxDepth))
        {
            return null;
        }

        BlockHeader? header = blockTree.FindParentHeader(currentBlock, BlockTreeLookupOptions.TotalDifficultyNotNeeded) ??
                              throw new InvalidDataException("Parent header cannot be found when executing BLOCKHASH operation");

        for (var i = 0; i < MaxDepth; i++)
        {
            if (number == header.Number)
            {
                if (_logger.IsTrace)
                    _logger.Trace($"BLOCKHASH opcode returning {header.Number},{header.Hash} for {currentBlock.Number} -> {number}");
                return header.Hash;
            }

            header = blockTree.FindParentHeader(header, BlockTreeLookupOptions.TotalDifficultyNotNeeded);
            if (header is null)
            {
                throw new InvalidDataException("Parent header cannot be found when executing BLOCKHASH operation");
            }
        }

        if (_logger.IsTrace)
            _logger.Trace($"BLOCKHASH opcode returning null for {currentBlock.Number} -> {number}");
        return null;
    }

    public Task Prefetch(BlockHeader currentBlock, CancellationToken token)
    {
        // No prefetching for Arbitrum - using synchronous lookups only
        return Task.CompletedTask;
    }
}
