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
        ulong l1BlockNumber,
        int logIndexStart,
        bool? isTimeboosted) : base(txHash, receipt, blockTimestamp, gasInfo, logIndexStart)
    {
        GasUsedForL1 = receipt.GasUsedForL1;
        L1BlockNumber = l1BlockNumber;
        IsTimeboosted = isTimeboosted;

        if (receipt.MultiGasUsed is { } multiGas && !multiGas.IsZero())
            MultiGasUsed = multiGas.ToJson();
    }

    public ArbitrumReceiptForRpc(
        Hash256 txHash,
        TxReceipt receipt,
        ulong blockTimestamp,
        TxGasInfo gasInfo,
        ulong l1BlockNumber,
        int logIndexStart,
        bool? isTimeboosted) : base(txHash, receipt, blockTimestamp, gasInfo, logIndexStart)
    {
        L1BlockNumber = l1BlockNumber;
        IsTimeboosted = isTimeboosted;

        if (receipt is not ArbitrumTxReceipt arbitrumReceipt)
            return;

        GasUsedForL1 = arbitrumReceipt.GasUsedForL1;

        if (arbitrumReceipt.MultiGasUsed is { } multiGas && !multiGas.IsZero())
            MultiGasUsed = multiGas.ToJson();
    }

    [JsonPropertyName("l1BlockNumber")]
    public ulong L1BlockNumber { get; set; }

    [JsonPropertyName("gasUsedForL1")]
    public ulong GasUsedForL1 { get; set; }

    /// <summary>
    /// Multi-dimensional gas breakdown for the transaction.
    /// </summary>
    [JsonPropertyName("multiGasUsed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MultiGasForJson? MultiGasUsed { get; set; }

    [JsonPropertyName("timeboosted")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsTimeboosted { get; set; }
}
