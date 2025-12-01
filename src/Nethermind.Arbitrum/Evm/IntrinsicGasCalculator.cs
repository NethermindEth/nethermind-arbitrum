// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Linq;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm;

namespace Nethermind.Arbitrum.Evm;

/// <summary>
/// Calculates intrinsic gas with multidimensional gas tracking.
/// </summary>
public static class IntrinsicGasCalculator
{
    /// <summary>
    /// Calculate multi-dimensional intrinsic gas for a transaction.
    /// Maps transaction components to specific resource dimensions:
    /// - Base transaction cost → Computation
    /// - Calldata → L2Calldata
    /// - Init code → Computation
    /// - Access list → StorageAccess
    /// - Authorization list (EIP-7702) → StorageGrowth
    /// </summary>
    public static MultiGas CalculateIntrinsicMultiGas(Transaction tx, IReleaseSpec spec, bool isContractCreation)
    {
        MultiGas multiGas = MultiGas.Zero;

        // Base transaction cost → Computation dimension
        // 21000 for regular tx, 32000 for CREATE
        long baseCost = isContractCreation ? GasCostOf.TxCreate : GasCostOf.Transaction;
        multiGas.SaturatingIncrementInto(ResourceKind.Computation, (ulong)baseCost);

        // Calldata cost → L2Calldata dimension
        if (!tx.Data.IsEmpty)
        {
            ReadOnlySpan<byte> data = tx.Data.Span;

            // Count zero and non-zero bytes
            int zeroCount = 0;
            for (int i = 0; i < data.Length; i++)
                if (data[i] == 0)
                    zeroCount++;
            int nonZeroCount = data.Length - zeroCount;

            // EIP-2028 reduces non-zero byte cost from 68 to 16
            long nonZeroGasCost = spec.IsEip2028Enabled
                ? GasCostOf.TxDataNonZeroEip2028
                : GasCostOf.TxDataNonZero;

            long calldataGas = zeroCount * GasCostOf.TxDataZero + nonZeroCount * nonZeroGasCost;
            multiGas.SaturatingIncrementInto(ResourceKind.L2Calldata, (ulong)calldataGas);

            // EIP-3860: Init code size limit and cost
            // Init code gas → Computation dimension
            if (isContractCreation && spec.IsEip3860Enabled)
            {
                long wordCount = (data.Length + 31) / 32; // Round up to word count
                long initCodeGas = wordCount * GasCostOf.InitCodeWord;
                multiGas.SaturatingIncrementInto(ResourceKind.Computation, (ulong)initCodeGas);
            }
        }

        // EIP-2930: Access list cost → StorageAccess dimension
        if (tx.AccessList is not null && !tx.AccessList.IsEmpty)
        {
            (int addressesCount, int storageKeysCount) = tx.AccessList.Count;

            long accessListGas = addressesCount * GasCostOf.AccessAccountListEntry;
            accessListGas += storageKeysCount * GasCostOf.AccessStorageListEntry;

            multiGas.SaturatingIncrementInto(ResourceKind.StorageAccess, (ulong)accessListGas);
        }

        // EIP-7702: Authorization list cost → StorageGrowth dimension
        // Authorization creates new account code, so it's storage growth
        if (tx.AuthorizationList is not null && tx.AuthorizationList.Length > 0)
        {
            long authGas = tx.AuthorizationList.Length * GasCostOf.PerAuthBaseCost;
            multiGas.SaturatingIncrementInto(ResourceKind.StorageGrowth, (ulong)authGas);
        }

        return multiGas;
    }
}
