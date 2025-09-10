// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Consensus.Processing;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Execution;

public class ArbitrumBlockProductionTransactionPicker : BlockProcessor.BlockProductionTransactionPicker
{
    public ArbitrumBlockProductionTransactionPicker(ISpecProvider specProvider) : base(specProvider)
    {
    }

    public override BlockProcessor.AddingTxEventArgs CanAddTransaction(Block block, Transaction currentTx,
        IReadOnlySet<Transaction> transactionsInBlock, IWorldState stateProvider)
    {
        BlockProcessor.AddingTxEventArgs args = new(transactionsInBlock.Count, currentTx, block, transactionsInBlock);
        OnAddingTransaction(args);
        return args;
    }
}
