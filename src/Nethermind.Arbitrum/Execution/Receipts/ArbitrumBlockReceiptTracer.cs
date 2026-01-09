// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Blockchain.Tracing;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm.TransactionProcessing;

namespace Nethermind.Arbitrum.Execution.Receipts;

public class ArbitrumBlockReceiptTracer(
    ArbitrumTxExecutionContext txExecContext,
    IArbitrumConfig arbitrumConfig) : BlockReceiptsTracer<ArbitrumGas>
{
    protected override TxReceipt BuildReceipt(Address recipient, long spentGas, byte statusCode, LogEntry[] logEntries, Hash256? stateRoot)
    {
        Transaction transaction = CurrentTx!;
        ArbitrumTxReceipt txReceipt = new()
        {
            Logs = logEntries,
            TxType = transaction.Type,
            // Bloom calculated in parallel with other receipts
            GasUsedTotal = Block.GasUsed,
            StatusCode = statusCode,
            Recipient = transaction.IsContractCreation ? null : recipient,
            BlockHash = Block.Hash,
            BlockNumber = Block.Number,
            Index = _currentIndex,
            GasUsed = spentGas,
            Sender = transaction.SenderAddress,
            ContractAddress = transaction.IsContractCreation ? recipient : null,
            TxHash = transaction.Hash,
            PostTransactionState = stateRoot,
            GasUsedForL1 = txExecContext.PosterGas, // Arbitrum specific receipt field
            // Multidimensional gas: only populate if config enables it
            MultiGasUsed = arbitrumConfig.ExposeMultiGas ? txExecContext.AccumulatedMultiGas : null
        };

        return txReceipt;
    }
}
