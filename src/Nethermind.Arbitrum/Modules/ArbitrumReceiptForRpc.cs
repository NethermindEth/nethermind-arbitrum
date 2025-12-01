// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Text.Json.Serialization;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution.Receipts;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.JsonRpc.Data;

namespace Nethermind.Arbitrum.Modules;

/// <summary>
/// Arbitrum-specific receipt for RPC responses.
/// Extends base receipt with multi-dimensional gas tracking.
/// </summary>
public class ArbitrumReceiptForRpc : ReceiptForRpc
{
    public ArbitrumReceiptForRpc(
        Hash256 txHash,
        ArbitrumTxReceipt receipt,
        ulong blockTimestamp,
        TxGasInfo gasInfo,
        bool exposeMultiGas,
        int logIndexStart = 0) : base(txHash, receipt, blockTimestamp, gasInfo, logIndexStart)
    {
        if (exposeMultiGas)
        {
            MultiGasUsed = receipt.MultiGasUsed;
        }
    }

    public ArbitrumReceiptForRpc(
        Hash256 txHash,
        TxReceipt receipt,
        ulong blockTimestamp,
        TxGasInfo gasInfo,
        int logIndexStart = 0) : base(txHash, receipt, blockTimestamp, gasInfo, logIndexStart)
    {
    }

    /// <summary>
    /// Multi-dimensional gas used by this transaction.
    /// Only present when ExposeMultiGas config is enabled.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MultiGas? MultiGasUsed { get; set; }
}
