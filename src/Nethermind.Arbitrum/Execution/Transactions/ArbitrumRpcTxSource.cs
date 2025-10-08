using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Consensus.Producers;
using Nethermind.Consensus.Transactions;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Execution.Transactions;

public class ArbitrumRpcTxSource(ISpecProvider specProvider, ILogManager logManager) : ITxSource
{
    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumRpcTxSource>();

    public bool SupportsBlobs => false;

    public IEnumerable<Transaction> GetTransactions(BlockHeader parent, long gasLimit, PayloadAttributes? payloadAttributes = null, bool filterSource = false)
    {
            _logger.Error($"Getting transactions for block {parent.Number + 1}, gas limit {gasLimit}");

        if (payloadAttributes is ArbitrumPayloadAttributes arbitrumPayloadAttributes)
        {
            var transactions = NitroL2MessageParser.ParseTransactions(
                arbitrumPayloadAttributes.MessageWithMetadata.Message,
                specProvider.ChainId,
                _logger);

                _logger.Error($"Parsed {transactions.Count()} L2 transactions from message for block {parent.Number + 1}");

            return transactions;
        }

            _logger.Error($"No ArbitrumPayloadAttributes provided for block {parent.Number + 1}");

        return [];
    }
}

public class ArbitrumPayloadTxSource(ISpecProvider specProvider, ILogger logger) : ITxSource
{
    public bool SupportsBlobs => false;

    public IEnumerable<Transaction> GetTransactions(BlockHeader parent, long gasLimit, PayloadAttributes? payloadAttributes = null, bool filterSource = false)
    {
        if (logger.IsTrace)
            logger.Trace($"Getting L2 transactions for block {parent.Number}, gas limit {gasLimit}");

        if (payloadAttributes is ArbitrumPayloadAttributes arbitrumPayloadAttributes)
        {
            return NitroL2MessageParser.ParseTransactions(arbitrumPayloadAttributes.MessageWithMetadata.Message, specProvider.ChainId, logger);
        }

        return [];
    }
}
