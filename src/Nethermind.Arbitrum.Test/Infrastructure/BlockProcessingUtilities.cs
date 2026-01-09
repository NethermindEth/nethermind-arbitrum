using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Compression;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Blockchain.Tracing;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Blockchain;
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

        IWorldState worldState = chain.MainWorldState;
        worldState.AddToBalanceAndCreateIfNotExists(tx.SenderAddress!, UInt256.MaxValue, chain.SpecProvider.GenesisSpec);
        worldState.RecalculateStateRoot();

        BlockToProduce block =
            new BlockToProduce(
                new BlockHeader(chain.BlockTree.HeadHash, null!, ArbosAddresses.BatchPosterAddress, UInt256.Zero, chain.BlockTree.Head!.Number + 1, 100_000, 100,
                    []), [tx], Array.Empty<BlockHeader>(), null);

        block.Header.BaseFeePerGas = baseFeePerGas;

        return block;
    }

    /// <summary>
    /// Processes a block using the BlockProducer's production path which properly handles
    /// Arbitrum internal transactions (including gas limit = 0).
    /// Returns the receipts captured by the block tracer.
    /// </summary>
    public static IReadOnlyList<TxReceipt> ProcessBlockWithInternalTx(ArbitrumRpcTestBlockchain chain, BlockToProduce block)
    {
        long nextBlockNumber = chain.BlockTree.Head!.Number + 1;

        // Create L1 message header
        L1IncomingMessageHeader l1Header = new(
            ArbitrumL1MessageKind.L2Message,
            Address.Zero,
            0,
            block.Header.Timestamp,
            Hash256.Zero,
            0);

        // Create payload attributes with transactions for the block producer
        // Serialize transactions into L2 message format
        Transaction[] transactions = block.Transactions.ToArray();
        byte[] l2Msg = SerializeTransactionsToL2Message(transactions);

        ArbitrumPayloadAttributes payloadAttributes = new()
        {
            MessageWithMetadata = new MessageWithMetadata(new L1IncomingMessage(l1Header, l2Msg, null), 0),
            Number = nextBlockNumber
        };

        // Use non-generic BlockReceiptsTracer to capture receipts
        BlockReceiptsTracer blockTracer = new();

        // Use the block producer which uses production executor (handles internal tx with gas=0)
        Task<Block?> buildBlockTask = chain.BlockProducer.BuildBlock(
            parentHeader: chain.BlockTree.Head?.Header,
            blockTracer: blockTracer,
            payloadAttributes: payloadAttributes);

        buildBlockTask.Wait(ArbitrumTestBlockchainBase.DefaultTimeout);
        Block? producedBlock = buildBlockTask.Result;

        if (producedBlock?.Hash is null)
            throw new InvalidOperationException("Failed to produce block or block has no hash");

        // Get receipts from the tracer
        ReadOnlySpan<TxReceipt> receiptsSpan = blockTracer.TxReceipts;
        if (receiptsSpan.Length > 0)
            return receiptsSpan.ToArray();

        // Fallback: wait for block to be added to tree and get receipts from storage
        int waitAttempts = 0;
        const int maxWaitAttempts = 100;
        while (chain.BlockTree.Head?.Number != nextBlockNumber && waitAttempts < maxWaitAttempts)
        {
            Thread.Sleep(10);
            waitAttempts++;
        }

        if (chain.BlockTree.Head?.Number == nextBlockNumber)
        {
            TxReceipt[] storedReceipts = chain.ReceiptStorage.Get(chain.BlockTree.Head!);
            if (storedReceipts.Length > 0)
                return storedReceipts;
        }

        throw new InvalidOperationException($"No receipts captured for block {nextBlockNumber}. Produced block: {producedBlock.Transactions.Length} txs, hash: {producedBlock.Hash}");
    }

    /// <summary>
    /// Serializes transactions into L2 message format for the block producer.
    /// Format: [1 byte L2MessageKind.Batch] + [for each tx: 8 bytes length + 1 byte SignedTx kind + RLP-encoded tx]
    /// </summary>
    private static byte[] SerializeTransactionsToL2Message(Transaction[] transactions)
    {
        if (transactions.Length == 0)
            return [];

        if (transactions.Length == 1)
        {
            // Single transaction: just [kind][RLP tx]
            Rlp txRlp = TxDecoder.Instance.Encode(transactions[0]);
            byte[] result = new byte[1 + txRlp.Bytes.Length];
            result[0] = (byte)ArbitrumL2MessageKind.SignedTx;
            txRlp.Bytes.CopyTo(result.AsSpan(1));
            return result;
        }

        // Multiple transactions: batch format
        // [1 byte Batch kind] + [for each tx: 8 bytes length + 1 byte kind + RLP tx]
        List<byte[]> encodedTxs = new(transactions.Length);
        int totalSize = 1; // 1 byte for Batch kind

        foreach (Transaction tx in transactions)
        {
            Rlp txRlp = TxDecoder.Instance.Encode(tx);
            encodedTxs.Add(txRlp.Bytes);
            totalSize += 8 + 1 + txRlp.Bytes.Length; // 8 bytes length + 1 byte kind + tx data
        }

        byte[] result2 = new byte[totalSize];
        Span<byte> span = result2.AsSpan();
        span[0] = (byte)ArbitrumL2MessageKind.Batch;
        int written = 1;

        for (int i = 0; i < encodedTxs.Count; i++)
        {
            byte[] txBytes = encodedTxs[i];
            ulong msgLen = (ulong)(txBytes.Length + 1); // +1 for kind byte
            msgLen.ToBigEndianByteArray().CopyTo(span.Slice(written, 8));
            written += 8;
            span[written++] = (byte)ArbitrumL2MessageKind.SignedTx;
            txBytes.CopyTo(span.Slice(written));
            written += txBytes.Length;
        }

        return result2;
    }

    public static ulong GetCallDataUnits(IWorldState worldState, Transaction tx)
    {
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), LimboLogs.Instance.GetLogger("arbosState"));
        ulong brotliCompressionLevel = arbosState.BrotliCompressionLevel.Get();

        Rlp encodedTx = Rlp.Encode(tx);
        ulong l1Bytes = (ulong)BrotliCompression.Compress(encodedTx.Bytes, brotliCompressionLevel).Length;
        ulong calldataUnits = l1Bytes * GasCostOf.TxDataNonZeroEip2028;

        return calldataUnits;
    }
}
