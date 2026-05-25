// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Threading;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Blockchain.Tracing;
using Nethermind.Consensus.Processing;
using Nethermind.Core;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Logging;
using static Nethermind.Consensus.Processing.IBlockProcessor;

namespace Nethermind.Arbitrum.Execution;

/// <summary>
/// Arbitrum-specific block validation transaction executor.
/// Wraps the base executor to commit multi-gas fees at block start.
/// </summary>
public class ArbitrumBlockValidationTransactionsExecutor(
    ITransactionProcessorAdapter transactionProcessor,
    IWorldState stateProvider,
    ILogManager logManager,
    BlockProcessor.BlockValidationTransactionsExecutor.ITransactionProcessedEventHandler? transactionProcessedEventHandler = null)
    : IBlockTransactionsExecutor
{
    private readonly BlockProcessor.BlockValidationTransactionsExecutor _inner = new(transactionProcessor, stateProvider, transactionProcessedEventHandler);
    private readonly IWorldState _stateProvider = stateProvider;
    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumBlockValidationTransactionsExecutor>();

    public void SetBlockExecutionContext(in BlockExecutionContext blockExecutionContext) =>
        _inner.SetBlockExecutionContext(in blockExecutionContext);

    public TxReceipt[] ProcessTransactions(Block block, ProcessingOptions processingOptions, BlockReceiptsTracer receiptsTracer, CancellationToken token = default)
    {
        // Commit multi-gas fees at block start (rotates next-block fees to current-block fees).
        // This must happen here because PrepareBlock uses a temporary state scope that gets discarded.
        ArbosState arbosState = ArbosState.OpenArbosState(_stateProvider, new ZeroGasBurner(), _logger);
        arbosState.L2PricingState.CommitMultiGasFees();

        return _inner.ProcessTransactions(block, processingOptions, receiptsTracer, token);
    }
}
