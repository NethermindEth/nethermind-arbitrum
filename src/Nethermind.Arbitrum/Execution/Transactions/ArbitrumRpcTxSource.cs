using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Consensus.Producers;
using Nethermind.Consensus.Transactions;
using Nethermind.Core;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Execution.Transactions;

public class ArbitrumRpcTxSource(ILogManager logManager) : ITxSource
{
    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumRpcTxSource>();

    public bool SupportsBlobs => false;
    private IReadOnlyList<Transaction> _injectedTransactions;

    public IEnumerable<Transaction> GetTransactions(BlockHeader parent, long gasLimit, PayloadAttributes? payloadAttributes = null, bool filterSource = false)
    {
        if (_logger.IsTrace) _logger.Trace($"Getting transactions for block {parent.Number}, gas limit {gasLimit}");
        return [];
        return _injectedTransactions;
    }

    public void InjectTransactions(IReadOnlyList<Transaction> transactions)
    {
        _injectedTransactions = transactions;
    }
}

public class ArbitrumPayloadTxSource(ChainSpec chainSpec, ILogger logger) : ITxSource
{
    public bool SupportsBlobs => false;

    public IEnumerable<Transaction> GetTransactions(BlockHeader parent, long gasLimit, PayloadAttributes? payloadAttributes = null, bool filterSource = false)
    {
        if (logger.IsTrace) logger.Trace($"Getting L2 transactions for block {parent.Number}, gas limit {gasLimit}");

        if (payloadAttributes is ArbitrumPayloadAttributes arbitrumPayloadAttributes)
        {
            return NitroL2MessageParser.ParseTransactions(arbitrumPayloadAttributes.MessageWithMetadata.Message, chainSpec.ChainId, logger);
        }

        return [];
    }
}
