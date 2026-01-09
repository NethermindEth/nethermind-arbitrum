// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Evm;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Execution.Transactions;

public class ArbitrumTxExecutionContext
{
    public Hash256? CurrentRetryable { get; set; }

    public Address? CurrentRefundTo { get; set; }

    public UInt256 PosterFee { get; set; }

    public ulong PosterGas { get; set; }

    public ulong IntrinsicGas { get; set; }

    // Amount of gas temporarily held to prevent compute from exceeding the block gas limit
    public ulong ComputeHoldGas { get; set; }

    public ArbitrumTxType TopLevelTxType { get; set; }

    /// <summary>
    /// The accumulated multi-dimensional gas breakdown for the current transaction.
    /// </summary>
    public MultiGas AccumulatedMultiGas { get; set; }

    /// <summary>
    /// Resets the context for the next transaction.
    /// </summary>
    public void Reset()
    {
        CurrentRetryable = null;
        CurrentRefundTo = null;
        PosterFee = 0;
        PosterGas = 0;
        IntrinsicGas = 0;
        ComputeHoldGas = 0;
        TopLevelTxType = ArbitrumTxType.EthLegacy;
        AccumulatedMultiGas = default;
    }
}
