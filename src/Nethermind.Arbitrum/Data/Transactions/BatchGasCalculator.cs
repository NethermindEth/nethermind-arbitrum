// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Evm;

namespace Nethermind.Arbitrum.Data.Transactions;

/// <summary>
/// Calculates gas costs for batch posting operations in Arbitrum.
/// This matches the LegacyCostForStats function from Nitro's Go implementation.
/// </summary>
public static class BatchGasCalculator
{
    /// <summary>
    /// Calculates the legacy gas cost for batch statistics.
    /// This includes:
    /// - Gas for zero and non-zero bytes in the batch data
    /// - Gas for keccak256 hashing the batch
    /// - Gas for two SSTORE operations (batch + batch-posting report into inbox)
    /// </summary>
    /// <param name="length">Total length of the batch data in bytes</param>
    /// <param name="nonZeros">Number of non-zero bytes in the batch data</param>
    /// <returns>Total gas cost for the batch posting</returns>
    public static ulong LegacyCostForStats(ulong length, ulong nonZeros)
    {
        // Calculate calldata gas cost (zero bytes are cheaper)
        ulong gas = GasCostOf.TxDataZero * (length - nonZeros)
                    + GasCostOf.TxDataNonZeroEip2028 * nonZeros;

        // The poster also pays to keccak the batch
        ulong keccakWords = (length + 31) / 32; // WordsForBytes: round up to 32-byte words
        gas += GasCostOf.Sha3 + (keccakWords * GasCostOf.Sha3Word);

        // And place it and a batch-posting report into the inbox (2 SSTORE operations)
        gas += 2 * GasCostOf.SSet;

        return gas;
    }
}
