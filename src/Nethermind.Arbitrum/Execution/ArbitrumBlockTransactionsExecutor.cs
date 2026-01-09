// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Evm;
using Nethermind.Consensus.Processing;
using Nethermind.Evm.State;
using Nethermind.Evm.TransactionProcessing;

namespace Nethermind.Arbitrum.Execution;

public class ArbitrumBlockTransactionsExecutor(
    ITransactionProcessorAdapter<ArbitrumGas> transactionProcessor,
    IWorldState stateProvider,
    BlockProcessor.BlockValidationTransactionsExecutor<ArbitrumGas>.ITransactionProcessedEventHandler? transactionProcessedEventHandler = null)
    : BlockProcessor.BlockValidationTransactionsExecutor<ArbitrumGas>(transactionProcessor, stateProvider, transactionProcessedEventHandler)
{
}
