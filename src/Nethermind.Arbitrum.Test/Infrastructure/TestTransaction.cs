// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Int256;
using Nethermind.Crypto;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public static class TestTransaction
{
    public static ArbitrumRetryTransaction PrepareArbitrumRetryTx(IWorldState worldState, BlockHeader blockHeader, Hash256 ticketIdHash, Address from, Address to, Address beneficiary, UInt256 value)
    {
        ulong gasSupplied = 100_000_000;
        PrecompileTestContextBuilder setupContext = new(worldState, gasSupplied);
        setupContext.WithArbosState().WithBlockExecutionContext(blockHeader);

        ulong timeout = blockHeader.Timestamp + 1; // retryable not expired

        Retryable retryable = setupContext.ArbosState.RetryableState.CreateRetryable(
            ticketIdHash, from, to, value, beneficiary, timeout, []);

        ulong nonce = retryable.NumTries.Get(); // 0
        UInt256 maxRefund = UInt256.MaxValue;

        ArbitrumRetryTransaction tx = new()
        {
            ChainId = setupContext.ChainId,
            Nonce = nonce,
            SenderAddress = retryable.From.Get(),
            DecodedMaxFeePerGas = setupContext.BlockExecutionContext.Header.BaseFeePerGas,
            GasFeeCap = setupContext.BlockExecutionContext.Header.BaseFeePerGas,
            Gas = GasCostOf.Transaction,
            GasLimit = GasCostOf.Transaction,
            To = retryable.To?.Get(),
            Value = retryable.CallValue.Get(),
            Data = retryable.Calldata.Get(),
            TicketId = ticketIdHash,
            RefundTo = setupContext.Caller,
            MaxRefund = maxRefund,
            SubmissionFeeRefund = 0
        };

        tx.Hash = tx.CalculateHash();

        return tx;
    }

    public static Transaction CreateTransfer(ulong nonce = 0, PrivateKey? signer = null)
    {
        return Build.A.Transaction
            .WithNonce(nonce)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(TestItem.AddressC)
            .WithChainId(FullChainSimulationChainSpecProvider.ChainId)
            .SignedAndResolved(signer ?? FullChainSimulationAccounts.AccountA)
            .TestObject;
    }
}
