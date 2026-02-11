// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core;
using Nethermind.Core.Caching;
using Nethermind.Core.Crypto;
using Nethermind.Crypto;

namespace Nethermind.Arbitrum.Arbos.Compression
{
    public static class TransactionExtensions
    {
        // Arbitrum cache of the calldata units at a brotli compression level.
        // The top 8 bits are the brotli compression level last used to compute this,
        // and the remaining 56 bits are the calldata units at that compression level.
        private const int CompressionLevelBits = 8;
        private const int CalldataUnitsBits = 56;
        private const int CalldataUnitsCacheCapacity = 100;
        private const ulong CalldataUnitsMask = (1UL << CalldataUnitsBits) - 1;
        private const ulong MaxCompressionLevel = 1UL << CompressionLevelBits;

        private static readonly ClockCache<Hash256AsKey, ulong> _cachedCalldataUnits = new(maxCapacity: CalldataUnitsCacheCapacity);

        public static (ulong, ulong) GetRawCachedCalldataUnits(this Transaction transaction)
        {
            if (_cachedCalldataUnits.TryGet(transaction.Hash ?? transaction.CalculateHash(), out ulong repr))
            {
                ulong cachedCompressionLevel = repr >> CalldataUnitsBits;
                ulong cachedCalldataUnits = repr & CalldataUnitsMask;
                return (cachedCompressionLevel, cachedCalldataUnits);
            }
            return (0, 0);
        }

        // GetCachedCalldataUnits returns the cached calldata units for a given brotli compression level,
        // returning nil if no cache is present or the cache is for a different compression level.
        public static ulong GetCachedCalldataUnits(this Transaction transaction, ulong requestedCompressionLevel)
        {
            (ulong cachedCompressionLevel, ulong cachedCalldataUnits) = transaction.GetRawCachedCalldataUnits();
            // different compression level
            if (cachedCompressionLevel != requestedCompressionLevel)
                return 0;

            return cachedCalldataUnits;
        }

        // SetCachedCalldataUnits sets the cached brotli compression level and corresponding calldata units,
        // or clears the cache if the values are too large to fit (at least 2**8 and 2**56 respectively).
        // Note that a zero calldataUnits is also treated as an empty cache.
        public static void SetCachedCalldataUnits(this Transaction transaction, ulong compressionLevel, ulong calldataUnits)
        {
            ulong repr = 0;

            // Ensure the compressionLevel and calldataUnits will fit.
            // Otherwise, just clear the cache.
            if (compressionLevel < MaxCompressionLevel && calldataUnits <= CalldataUnitsMask)
            {
                repr = (compressionLevel << CalldataUnitsBits) | calldataUnits;
            }

            _cachedCalldataUnits.Set(transaction.Hash ?? transaction.CalculateHash(), repr);
        }
    }
}
