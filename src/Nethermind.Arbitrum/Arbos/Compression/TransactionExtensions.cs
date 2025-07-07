// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Arbos.Compression
{
    public static class TransactionExtensions
    {
        private static Hash256? txHash;

        // Arbitrum cache of the calldata units at a brotli compression level.
        // The top 8 bits are the brotli compression level last used to compute this,
        // and the remaining 56 bits are the calldata units at that compression level.
        private static ulong _calldataUnitsForBrotliCompressionLevel;
        private static readonly Lock _lock = new();

        private static ulong CalldataUnitsForBrotliCompressionLevel
        {
            get { lock (_lock) return _calldataUnitsForBrotliCompressionLevel; }
            set { lock (_lock) _calldataUnitsForBrotliCompressionLevel = value; }
        }

        private static (ulong, ulong) GetRawCachedCalldataUnits()
        {
            ulong repr = CalldataUnitsForBrotliCompressionLevel;
            ulong cachedCompressionLevel = repr >> 56;
            ulong cachedCalldataUnits = repr & ((1 << 56) - 1);
            return (cachedCompressionLevel, cachedCalldataUnits);
        }

        // GetCachedCalldataUnits returns the cached calldata units for a given brotli compression level,
        // returning nil if no cache is present or the cache is for a different compression level.
        public static ulong GetCachedCalldataUnits(this Transaction transaction, ulong requestedCompressionLevel)
        {
            if (txHash != transaction.Hash)
                return 0;

            (ulong cachedCompressionLevel, ulong cachedCalldataUnits) = GetRawCachedCalldataUnits();
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
            if (compressionLevel < (1 << 8) && calldataUnits < (1 << 56))
            {
                repr = (compressionLevel << 56) | calldataUnits;
            }

            CalldataUnitsForBrotliCompressionLevel = repr;
            txHash = transaction.Hash;
        }
    }
}
