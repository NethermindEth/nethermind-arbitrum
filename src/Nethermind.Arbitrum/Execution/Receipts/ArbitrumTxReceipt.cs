// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Evm;
using Nethermind.Core;
using Nethermind.Int256;

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

    /// <summary>
    /// Effective gas price for the transaction, stored at block processing time.
    /// For internal Arbitrum transactions, this is the block's base fee.
    /// </summary>
    public UInt256 EffectiveGasPrice { get; set; }
}
