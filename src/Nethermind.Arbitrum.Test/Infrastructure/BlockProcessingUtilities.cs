using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Compression;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Receipts;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public static class BlockProcessingUtilities
{
    public static BlockToProduce CreateBlockFromTx(ArbitrumRpcTestBlockchain chain, Transaction tx, UInt256 baseFeePerGas)
    {
        tx.Hash = tx.CalculateHash();

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        worldState.AddToBalanceAndCreateIfNotExists(tx.SenderAddress!, UInt256.MaxValue, chain.SpecProvider.GenesisSpec);
        worldState.RecalculateStateRoot();

        BlockToProduce block =
            new BlockToProduce(
                new BlockHeader(chain.BlockTree.HeadHash, null!, ArbosAddresses.BatchPosterAddress, UInt256.Zero, chain.BlockTree.Head!.Number + 1, 100_000, 100,
                    []), [tx], Array.Empty<BlockHeader>(), null);

        block.Header.BaseFeePerGas = baseFeePerGas;

        return block;
    }

    public static IReadOnlyList<TxReceipt> ProcessBlockWithInternalTx(ArbitrumRpcTestBlockchain chain, BlockToProduce block)
    {
        L1IncomingMessageHeader l1Header = new(ArbitrumL1MessageKind.Initialize, Address.Zero, 0, 0, Hash256.Zero, 0);
        ArbitrumInternalTransaction internalTx =
            ArbitrumBlockProducer.CreateInternalTransaction(l1Header, block.Header, block.Header, chain.SpecProvider);

        Transaction[] txsIncludingInternal = block.Transactions.Prepend(internalTx).ToArray();
        block.Transactions = txsIncludingInternal;

        var blockReceiptsTracer = new ArbitrumBlockReceiptTracer((chain.TxProcessor as ArbitrumTransactionProcessor)!.TxExecContext);
        blockReceiptsTracer.StartNewBlockTrace(block);

        chain.BlockProcessor.ProcessOne(block, ProcessingOptions.ProducingBlock, blockReceiptsTracer, chain.SpecProvider.GenesisSpec, CancellationToken.None);

        blockReceiptsTracer.EndBlockTrace();

        return blockReceiptsTracer.TxReceipts;
    }

    public static ulong GetCallDataUnits(IWorldState worldState, Transaction tx)
    {
        var arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), LimboLogs.Instance.GetLogger("arbosState"));
        ulong brotliCompressionLevel = arbosState.BrotliCompressionLevel.Get();

        Rlp encodedTx = Rlp.Encode(tx);
        ulong l1Bytes = (ulong)BrotliCompression.Compress(encodedTx.Bytes, brotliCompressionLevel).Length;
        ulong calldataUnits = l1Bytes * GasCostOf.TxDataNonZeroEip2028;

        return calldataUnits;
    }
}
