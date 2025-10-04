using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Consensus.Producers;
using Nethermind.Consensus.Transactions;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Execution.Transactions;

public class ArbitrumRpcTxSource(ILogManager logManager) : ITxSource
{
    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumRpcTxSource>();

    public bool SupportsBlobs => false;
    private IReadOnlyList<Transaction> _injectedTransactions;

    public IEnumerable<Transaction> GetTransactions(BlockHeader parent, long gasLimit, PayloadAttributes? payloadAttributes = null, bool filterSource = false)
    {
        if (_logger.IsTrace)
            _logger.Trace($"Getting transactions for block {parent.Number}, gas limit {gasLimit}");
        return [];
        return _injectedTransactions;
    }

    public void InjectTransactions(IReadOnlyList<Transaction> transactions)
    {
        _injectedTransactions = transactions;
    }
}

public class ArbitrumPayloadTxSource(ISpecProvider specProvider, ILogger logger) : ITxSource
{
    public bool SupportsBlobs => false;

    public IEnumerable<Transaction> GetTransactions(BlockHeader parent, long gasLimit, PayloadAttributes? payloadAttributes = null, bool filterSource = false)
    {
        logger.Info($"[PAYLOAD_TX_SOURCE] Getting L2 transactions for block {parent.Number}, gas limit {gasLimit}, payloadAttributes type: {payloadAttributes?.GetType().Name}");

        if (payloadAttributes is ArbitrumPayloadAttributes arbitrumPayloadAttributes)
        {
            logger.Info($"[PAYLOAD_TX_SOURCE] Message kind: {arbitrumPayloadAttributes.MessageWithMetadata.Message.Header.Kind}, L2Msg length: {arbitrumPayloadAttributes.MessageWithMetadata.Message.L2Msg?.Length ?? 0}");
            
            var transactions = NitroL2MessageParser.ParseTransactions(arbitrumPayloadAttributes.MessageWithMetadata.Message, specProvider.ChainId, logger);
            
            logger.Info($"[PAYLOAD_TX_SOURCE] Parsed {transactions.Count} transactions");
            foreach (var tx in transactions)
            {
                logger.Info($"[PAYLOAD_TX_SOURCE] TX: hash={tx.Hash}, to={tx.To}, value={tx.Value}, gasLimit={tx.GasLimit}");
            }
            
            return transactions;
        }

        logger.Warn($"[PAYLOAD_TX_SOURCE] payloadAttributes is not ArbitrumPayloadAttributes! Returning empty");
        return [];
    }
}
