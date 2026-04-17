// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core.Caching;

namespace Nethermind.Arbitrum.Rpc;

public interface IBlockMetadataProvider
{
    Task<byte[]?> GetBlockMetadataAsync(long blockNumber);
}

public sealed class BlockMetadataProvider(IArbitrumConsensusClient consensusClient) : IBlockMetadataProvider
{
    private readonly LruCache<long, byte[]> _cache = new(1024, "BlockMetadata");

    public async Task<byte[]?> GetBlockMetadataAsync(long blockNumber)
    {
        if (_cache.TryGet(blockNumber, out byte[] cached))
            return cached;

        byte[]? result = await consensusClient.GetBlockMetadataAsync(blockNumber);
        if (result is not null)
            _cache.Set(blockNumber, result);

        return result;
    }
}
