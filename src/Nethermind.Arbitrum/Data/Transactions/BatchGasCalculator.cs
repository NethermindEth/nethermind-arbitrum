// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Evm;

namespace Nethermind.Arbitrum.Data.Transactions;

/// <summary>
/// Calculates gas costs for batch posting operations in Arbitrum.
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
        ulong gas = GasCostOf.TxDataZero * (length - nonZeros)
                    + GasCostOf.TxDataNonZeroEip2028 * nonZeros;
        ulong keccakWords = (length + 31) / 32;
        gas += GasCostOf.Sha3 + (keccakWords * GasCostOf.Sha3Word);
        gas += 2 * GasCostOf.SSet;
        return gas;
    }
}
