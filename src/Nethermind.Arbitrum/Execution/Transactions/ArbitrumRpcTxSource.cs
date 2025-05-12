using Nethermind.Consensus.Producers;
using Nethermind.Consensus.Transactions;
using Nethermind.Core;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Execution.Transactions;

public class ArbitrumRpcTxSource(ILogger logger) : ITxSource
{
    public bool SupportsBlobs => false;

    public IEnumerable<Transaction> GetTransactions(BlockHeader parent, long gasLimit, PayloadAttributes? payloadAttributes = null, bool filterSource = false)
    {
        return [];
    }
}
