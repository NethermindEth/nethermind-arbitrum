using Nethermind.Consensus.Producers;
using Nethermind.Consensus.Transactions;
using Nethermind.Core;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Execution.Transactions;

public class ArbitrumRpcTxSource(ILogManager logManager) : ITxSource
{
    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumRpcTxSource>();

    public bool SupportsBlobs => false;

    public IEnumerable<Transaction> GetTransactions(BlockHeader parent, long gasLimit, PayloadAttributes? payloadAttributes = null, bool filterSource = false)
    {
        if (_logger.IsTrace) _logger.Trace($"Getting transactions for block {parent.Number}, gas limit {gasLimit}");
        return [];
    }

    public void InjectTransactions(IReadOnlyList<Transaction> transactions)
    {
    }
}
