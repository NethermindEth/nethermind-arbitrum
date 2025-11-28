// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Blockchain;
using Nethermind.Blockchain.Blocks;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Logging;

namespace Nethermind.Arbitrum;

public class ArbitrumBlockhashProvider(
    IBlockhashCache blockhashCache,
    IWorldState worldState,
    ILogManager? logManager)
    : IBlockhashProvider
{
    public const int MaxDepth = 256;
    private readonly IBlockhashStore _blockhashStore = new BlockhashStore(worldState);
    private readonly ILogger _logger = logManager?.GetClassLogger() ?? throw new ArgumentNullException(nameof(logManager));

    public Hash256? GetBlockhash(BlockHeader currentBlock, long number, IReleaseSpec spec)
    {
        if (spec.IsBlockHashInStateAvailable)
        {
            return _blockhashStore.GetBlockHashFromState(currentBlock, number, spec);
        }

        long current = currentBlock.Number;
        long depth = current - number;
        if (number >= current || number < 0 || depth > MaxDepth)
        {
            if (_logger.IsTrace) _logger.Trace($"BLOCKHASH opcode returning null for {currentBlock.Number} -> {number}");
            return null;
        }

        // Simple synchronous cache lookup
        return blockhashCache.GetHash(currentBlock, (int)depth)
               ?? throw new InvalidDataException("Hash cannot be found when executing BLOCKHASH operation");
    }

    public Task Prefetch(BlockHeader currentBlock, CancellationToken token)
    {
        // No prefetching for Arbitrum - using synchronous lookups only
        return Task.CompletedTask;
    }
}
