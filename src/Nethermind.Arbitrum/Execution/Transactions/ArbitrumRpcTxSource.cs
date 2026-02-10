// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

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

    public IEnumerable<Transaction> GetTransactions(BlockHeader parent, long gasLimit, PayloadAttributes? payloadAttributes = null, bool filterSource = false)
    {
        if (_logger.IsTrace)
            _logger.Trace($"Getting transactions for block {parent.Number}, gas limit {gasLimit}");
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
            if (arbitrumPayloadAttributes.MessageWithMetadata != null)
                return NitroL2MessageParser.ParseTransactions(arbitrumPayloadAttributes.MessageWithMetadata.Message, specProvider.ChainId, arbitrumPayloadAttributes.PreviousArbosVersion, logger);

        return [];
    }
}
