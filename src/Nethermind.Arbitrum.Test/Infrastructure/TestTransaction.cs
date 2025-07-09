using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Int256;
using Nethermind.State;
using Nethermind.Arbitrum.Math;
using Nethermind.Crypto;

namespace Nethermind.Arbitrum.Test.Infrastructure
{
    internal static class TestTransaction
    {
        public static ArbitrumTransaction<ArbitrumRetryTx> PrepareArbitrumRetryTx(IWorldState worldState, BlockHeader blockHeader, Hash256 ticketIdHash, Address from, Address to, Address beneficiary, UInt256 value)
        {
            ulong gasSupplied = 100_000_000;
            PrecompileTestContextBuilder setupContext = new(worldState, gasSupplied);
            setupContext.WithArbosState().WithBlockExecutionContext(blockHeader);

            ulong timeout = blockHeader.Timestamp + 1; // retryable not expired

            Retryable retryable = setupContext.ArbosState.RetryableState.CreateRetryable(
                ticketIdHash, from, to, value, beneficiary, timeout, []);

            ulong nonce = retryable.NumTries.Get(); // 0
            UInt256 maxRefund = UInt256.MaxValue;

            ArbitrumRetryTx innerTx = new(
                setupContext.ChainId,
                nonce,
                retryable.From.Get(),
                setupContext.BlockExecutionContext.Header.BaseFeePerGas,
                GasCostOf.Transaction,
                retryable.To?.Get(),
                retryable.CallValue.Get(),
                retryable.Calldata.Get(),
                ticketIdHash,
                setupContext.Caller,
                maxRefund,
                0
            );

            var tx = new ArbitrumTransaction<ArbitrumRetryTx>(innerTx)
            {
                ChainId = innerTx.ChainId,
                Type = (TxType)ArbitrumTxType.ArbitrumRetry,
                SenderAddress = innerTx.From,
                To = innerTx.To,
                Value = innerTx.Value,
                GasLimit = innerTx.Gas.ToLongSafe(),
                GasPrice = blockHeader.BaseFeePerGas,
                DecodedMaxFeePerGas = blockHeader.BaseFeePerGas,
            };
            tx.Hash = tx.CalculateHash();

            return tx;
        }
    }
}
