// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text.Json.Serialization;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution.Receipts;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.JsonRpc.Data;

namespace Nethermind.Arbitrum.Rpc;

/// <summary>
/// Arbitrum-specific receipt for RPC responses with GasUsedForL1 and MultiGas fields.
/// </summary>
public class ArbitrumReceiptForRpc : ReceiptForRpc
{
    public ArbitrumReceiptForRpc(
        Hash256 txHash,
        ArbitrumTxReceipt receipt,
        ulong blockTimestamp,
        TxGasInfo gasInfo,
        int logIndexStart = 0) : base(txHash, receipt, blockTimestamp, gasInfo, logIndexStart)
    {
        GasUsedForL1 = receipt.GasUsedForL1;

        if (receipt.MultiGasUsed is { } multiGas && !multiGas.IsZero())
            MultiGasUsed = multiGas.ToJson();
    }

    public ArbitrumReceiptForRpc(
        Hash256 txHash,
        TxReceipt receipt,
        ulong blockTimestamp,
        TxGasInfo gasInfo,
        int logIndexStart = 0) : base(txHash, receipt, blockTimestamp, gasInfo, logIndexStart)
    {
        if (receipt is ArbitrumTxReceipt arbitrumReceipt)
        {
            GasUsedForL1 = arbitrumReceipt.GasUsedForL1;

            if (arbitrumReceipt.MultiGasUsed is { } multiGas && !multiGas.IsZero())
                MultiGasUsed = multiGas.ToJson();
        }
    }

    /// <summary>
    /// Gas used for L1 calldata posting costs.
    /// </summary>
    [JsonPropertyName("gasUsedForL1")]
    public ulong GasUsedForL1 { get; set; }

    /// <summary>
    /// Multi-dimensional gas breakdown for the transaction.
    /// </summary>
    [JsonPropertyName("multiGasUsed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MultiGasForJson? MultiGasUsed { get; set; }
}
