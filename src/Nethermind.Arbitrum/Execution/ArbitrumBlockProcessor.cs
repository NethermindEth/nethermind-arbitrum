// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Blockchain.BeaconBlockRoot;
using Nethermind.Blockchain.Blocks;
using Nethermind.Blockchain.Receipts;
using Nethermind.Consensus.ExecutionRequests;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Rewards;
using Nethermind.Consensus.Validators;
using Nethermind.Consensus.Withdrawals;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm.Tracing;
using Nethermind.Logging;
using Nethermind.State;

namespace Nethermind.Arbitrum.Execution
{
    public class ArbitrumBlockProcessor : BlockProcessor
    {
        public ArbitrumBlockProcessor(
            ISpecProvider specProvider,
            IBlockValidator blockValidator,
            IRewardCalculator rewardCalculator,
            IBlockProcessor.IBlockTransactionsExecutor blockTransactionsExecutor,
            IWorldState stateProvider,
            IReceiptStorage receiptStorage,
            IBlockhashStore blockhashStore,
            IBeaconBlockRootHandler beaconBlockRootHandler,
            ILogManager logManager,
            IWithdrawalProcessor withdrawalProcessor,
            IExecutionRequestsProcessor executionRequestsProcessor,
            IBlockCachePreWarmer? preWarmer = null)
            : base(
                specProvider,
                blockValidator,
                rewardCalculator,
                blockTransactionsExecutor,
                stateProvider,
                receiptStorage,
                beaconBlockRootHandler,
                blockhashStore,
                logManager,
                withdrawalProcessor,
                executionRequestsProcessor,
                preWarmer)
        {
        }

        protected override TxReceipt[] ProcessBlock(Block block, IBlockTracer blockTracer, ProcessingOptions options, CancellationToken token)
        {
            return base.ProcessBlock(block, blockTracer, options, token);
        }
    }
}
