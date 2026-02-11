// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Consensus.Processing;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm.State;
using Nethermind.Int256;

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

        if (currentTx.SenderAddress is null)
            return args.Set(BlockProcessor.TxAction.Skip, "Null sender");

        if (!SkipNonceCheck(currentTx))
        {
            UInt256 expectedNonce = stateProvider.GetNonce(currentTx.SenderAddress);
            if (expectedNonce != currentTx.Nonce)
                return args.Set(BlockProcessor.TxAction.Skip, $"Invalid nonce - expected {expectedNonce}");
        }

        IReleaseSpec spec = _specProvider.GetSpec(block.Header);
        if (!SkipSenderIsContractCheck(currentTx) && stateProvider.IsInvalidContractSender(spec, currentTx.SenderAddress))
            return args.Set(BlockProcessor.TxAction.Skip, $"Sender is contract");

        OnAddingTransaction(args);

        return args;
    }

    private static bool SkipNonceCheck(Transaction transaction) // Similar to Nitro's skipNonceChecks
    {
        return transaction is ArbitrumTransaction and not ArbitrumUnsignedTransaction;
    }

    private static bool SkipSenderIsContractCheck(Transaction transaction) // Similar to Nitro's skipFromEOACheck
    {
        return transaction is ArbitrumTransaction and not ArbitrumUnsignedTransaction;
    }
}
