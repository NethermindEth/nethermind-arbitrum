using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Compression;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Receipts;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Math;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;
using Nethermind.Evm;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;
using Nethermind.State;
using static Nethermind.Arbitrum.Execution.CachedL1PriceData;

namespace Nethermind.Arbitrum.Test.BlockProcessing;

[TestFixture]
internal class CachedL1PriceDataTests
{
    [Test]
    public void CacheL1PriceData_CacheStartingBlockIs0And1BlockGetsProcessed_OverwritesCache()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumTestBlockchainBase.CreateTestBlockchainWithGenesis();

        Transaction transferTx = Build.A.Transaction
            .WithTo(TestItem.AddressB)
            .WithValue(1_000_000)
            .WithGasLimit(22_000)
            .WithGasPrice(1_000)
            .WithNonce(0)
            .WithSenderAddress(TestItem.AddressA)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 baseFeePerGas = 1_000;
        BlockToProduce block = CreateBlockFromTx(chain, transferTx, baseFeePerGas);

        IReadOnlyList<TxReceipt> txReceipts = ProcessBlockWithInternalTx(chain, block);

        AssertCachedL1PriceData(chain, block, txReceipts, transferTx, chain.WorldStateManager.GlobalWorldState, baseFeePerGas, (ulong)block.Number, []);
    }

    [Test]
    public void CacheL1PriceData_2BlocksGetProcessed_AddsToCache()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumTestBlockchainBase.CreateTestBlockchainWithGenesis();

        Transaction transferTx1 = Build.A.Transaction
            .WithTo(TestItem.AddressB)
            .WithValue(1_000_000)
            .WithGasLimit(22_000) // 21_000 + estimated posting cost (~150) + margin
            .WithGasPrice(1_000)
            .WithNonce(0)
            .WithSenderAddress(TestItem.AddressA)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 baseFeePerGas = 1_000;
        BlockToProduce block1 = CreateBlockFromTx(chain, transferTx1, baseFeePerGas);

        IReadOnlyList<TxReceipt> txReceipts = ProcessBlockWithInternalTx(chain, block1);

        List<L1PriceDataOfMsg> l1PriceDataAfterBlock1 = AssertCachedL1PriceData(
            chain, block1, txReceipts, transferTx1, chain.WorldStateManager.GlobalWorldState, baseFeePerGas, (ulong)block1.Number, []
        );

        Transaction transferTx2 = Build.A.Transaction
            .WithTo(TestItem.AddressC)
            .WithValue(1_000_000)
            .WithGasLimit(22_000) // 21_000 + estimated posting cost (~150) + margin
            .WithGasPrice(1_000)
            .WithNonce(0)
            .WithSenderAddress(TestItem.AddressA)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        BlockToProduce block2 = CreateBlockFromTx(chain, transferTx2, baseFeePerGas);
        block2.Header.Number = block1.Header.Number + 1;

        txReceipts = ProcessBlockWithInternalTx(chain, block2);

        AssertCachedL1PriceData(chain, block2, txReceipts, transferTx2, chain.WorldStateManager.GlobalWorldState, baseFeePerGas, (ulong)block1.Number, l1PriceDataAfterBlock1);
    }

    [Test]
    public void MarkFeedStart_FeedStartEqualToEndOfCache_ResetCache()
    {
        // First, process a block to set the cache

        ArbitrumRpcTestBlockchain chain = ArbitrumTestBlockchainBase.CreateTestBlockchainWithGenesis();

        Transaction transferTx = Build.A.Transaction
            .WithTo(TestItem.AddressB)
            .WithValue(1_000_000)
            .WithGasLimit(22_000)
            .WithGasPrice(1_000)
            .WithNonce(0)
            .WithSenderAddress(TestItem.AddressA)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 baseFeePerGas = 1_000;
        BlockToProduce block = CreateBlockFromTx(chain, transferTx, baseFeePerGas);

        ProcessBlockWithInternalTx(chain, block);

        // Second, call MarkFeedStart which resets the cache

        CachedL1PriceData cachedL1PriceData = chain.CachedL1PriceData;
        cachedL1PriceData.StartOfL1PriceDataCache.Should().Be(1);
        cachedL1PriceData.EndOfL1PriceDataCache.Should().Be(1);
        // Just asserting count as content has already been asserted in previous tests
        cachedL1PriceData.MsgToL1PriceData.Count.Should().Be(1);

        chain.ArbitrumRpcModule.MarkFeedStart(cachedL1PriceData.EndOfL1PriceDataCache);

        cachedL1PriceData.StartOfL1PriceDataCache.Should().Be(0);
        cachedL1PriceData.EndOfL1PriceDataCache.Should().Be(0);
        cachedL1PriceData.MsgToL1PriceData.Should().BeEmpty();
    }

    [Test]
    public void MarkFeedStart_FeedStartIsWithinCacheWindow_CacheTrimmed()
    {
        // First, process 2 blocks to set the cache

        ArbitrumRpcTestBlockchain chain = ArbitrumTestBlockchainBase.CreateTestBlockchainWithGenesis();

        Transaction transferTx1 = Build.A.Transaction
            .WithTo(TestItem.AddressB)
            .WithValue(1_000_000)
            .WithGasLimit(22_000) // 21_000 + estimated posting cost (~150) + margin
            .WithGasPrice(1_000)
            .WithNonce(0)
            .WithSenderAddress(TestItem.AddressA)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 baseFeePerGas = 1_000;
        BlockToProduce block1 = CreateBlockFromTx(chain, transferTx1, baseFeePerGas);

        ProcessBlockWithInternalTx(chain, block1);

        Transaction transferTx2 = Build.A.Transaction
            .WithTo(TestItem.AddressC)
            .WithValue(1_000_000)
            .WithGasLimit(22_000) // 21_000 + estimated posting cost (~150) + margin
            .WithGasPrice(1_000)
            .WithNonce(0)
            .WithSenderAddress(TestItem.AddressA)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        BlockToProduce block2 = CreateBlockFromTx(chain, transferTx2, baseFeePerGas);
        block2.Header.Number = block1.Header.Number + 1;

        ProcessBlockWithInternalTx(chain, block2);

        // Second, call MarkFeedStart which trims the cache

        CachedL1PriceData cachedL1PriceData = chain.CachedL1PriceData;

        cachedL1PriceData.StartOfL1PriceDataCache.Should().Be(1);
        cachedL1PriceData.EndOfL1PriceDataCache.Should().Be(2);
        // Just asserting count as content has already been asserted in previous tests
        cachedL1PriceData.MsgToL1PriceData.Count.Should().Be(2);

        L1PriceDataOfMsg msgToL1PriceData = cachedL1PriceData.MsgToL1PriceData[1];

        chain.ArbitrumRpcModule.MarkFeedStart(1);

        cachedL1PriceData.StartOfL1PriceDataCache.Should().Be(2);
        cachedL1PriceData.EndOfL1PriceDataCache.Should().Be(2);
        cachedL1PriceData.MsgToL1PriceData.Should().BeEquivalentTo([msgToL1PriceData]);
    }

    private static BlockToProduce CreateBlockFromTx(ArbitrumRpcTestBlockchain chain, Transaction tx, UInt256 baseFeePerGas)
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

    private static IReadOnlyList<TxReceipt> ProcessBlockWithInternalTx(ArbitrumRpcTestBlockchain chain, BlockToProduce block)
    {
        L1IncomingMessageHeader l1Header = new(ArbitrumL1MessageKind.Initialize, Address.Zero, 0, 0, Hash256.Zero, 0);
        ArbitrumTransaction<ArbitrumInternalTx> internalTx =
            ArbitrumBlockProducer.CreateInternalTransaction(l1Header, block.Header, block.Header, chain.SpecProvider);

        Transaction[] txsIncludingInternal = block.Transactions.Prepend(internalTx).ToArray();
        block.Transactions = txsIncludingInternal;

        var blockReceiptsTracer = new ArbitrumBlockReceiptTracer((chain.TxProcessor as ArbitrumTransactionProcessor)!.TxExecContext);
        blockReceiptsTracer.StartNewBlockTrace(block);

        chain.BlockProcessor.Process(chain.BlockTree.Head?.StateRoot ?? Keccak.EmptyTreeHash,
            [block], ProcessingOptions.ProducingBlock, blockReceiptsTracer);

        blockReceiptsTracer.EndBlockTrace();

        return blockReceiptsTracer.TxReceipts;
    }

    private static List<L1PriceDataOfMsg> AssertCachedL1PriceData(
        ArbitrumRpcTestBlockchain chain,
        BlockToProduce newBlock,
        IReadOnlyList<TxReceipt> txReceipts,
        Transaction transferTx1,
        IWorldState worldState,
        UInt256 baseFeePerGas,
        ulong expectedCacheStart,
        List<L1PriceDataOfMsg> expectedL1PriceData
    )
    {
        CachedL1PriceData cachedL1PriceData = chain.CachedL1PriceData;

        (ulong calldataUnits, ulong posterGas) = GetCalldataUnitsAndPosterGas(baseFeePerGas, transferTx1, worldState);

        txReceipts.Count.Should().Be(2);
        txReceipts[1].Should().BeOfType<ArbitrumTxReceipt>();
        ArbitrumTxReceipt receipt = (txReceipts[1] as ArbitrumTxReceipt)!;
        receipt.GasUsedForL1.Should().Be(posterGas);

        cachedL1PriceData.StartOfL1PriceDataCache.Should().Be(expectedCacheStart);
        cachedL1PriceData.EndOfL1PriceDataCache.Should().Be((ulong)newBlock.Number);

        ulong l1GasCharged = receipt.GasUsedForL1 * baseFeePerGas.ToUInt64(null);

        expectedL1PriceData.Add(
            new L1PriceDataOfMsg(
                calldataUnits,
                expectedL1PriceData.Count == 0 ? calldataUnits : expectedL1PriceData[^1].CummulativeCallDataUnits + calldataUnits,
                l1GasCharged,
                expectedL1PriceData.Count == 0 ? l1GasCharged : expectedL1PriceData[^1].CummulativeL1GasCharged + l1GasCharged
            )
        );
        cachedL1PriceData.MsgToL1PriceData.Should().BeEquivalentTo(expectedL1PriceData);

        return expectedL1PriceData;
    }

    private static (ulong, ulong) GetCalldataUnitsAndPosterGas(UInt256 baseFeePerGas, Transaction tx, IWorldState worldState)
    {
        var arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), LimboLogs.Instance.GetLogger("arbosState"));
        ulong brotliCompressionLevel = arbosState.BrotliCompressionLevel.Get();

        Rlp encodedTx = Rlp.Encode(tx);
        ulong l1Bytes = (ulong)BrotliCompression.Compress(encodedTx.Bytes, brotliCompressionLevel).Length;
        ulong calldataUnits = l1Bytes * GasCostOf.TxDataNonZeroEip2028;

        UInt256 pricePerUnit = arbosState.L1PricingState.PricePerUnitStorage.Get();
        UInt256 posterCost = pricePerUnit * calldataUnits;

        ulong posterGas = (posterCost / baseFeePerGas).ToULongSafe();

        return (calldataUnits, posterGas);
    }
}
