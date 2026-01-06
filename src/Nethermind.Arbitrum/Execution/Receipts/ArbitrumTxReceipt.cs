// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Evm;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Execution.Receipts;

public class ArbitrumTxReceipt : TxReceipt
{
    /// <summary>
    /// Gas used for L1 calldata posting costs.
    /// </summary>
    public ulong GasUsedForL1 { get; set; }

    /// <summary>
    /// Multidimensional gas breakdown for the transaction.
    /// </summary>
    public MultiGas? MultiGasUsed { get; set; }
}
