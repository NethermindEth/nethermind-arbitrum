// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

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
