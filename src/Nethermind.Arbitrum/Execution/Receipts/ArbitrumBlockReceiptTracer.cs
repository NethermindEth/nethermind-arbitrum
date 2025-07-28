using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm.Tracing;

namespace Nethermind.Arbitrum.Execution.Receipts;

public class ArbitrumBlockReceiptTracer(ArbitrumTxExecutionContext txExecContext) : BlockReceiptsTracer
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
            GasUsedForL1 = txExecContext.PosterGas // Arbitrum specific receipt field
        };

        return txReceipt;
    }
}
