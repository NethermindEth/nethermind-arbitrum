using System.Diagnostics;
using Autofac;
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
        Helper_CacheL1PriceData_CacheStartingBlockIs0And1BlockGetsProcessed_OverwritesCache();
    }

    public ArbitrumRpcTestBlockchain Helper_CacheL1PriceData_CacheStartingBlockIs0And1BlockGetsProcessed_OverwritesCache()
    {
        var preConfigurer = (ContainerBuilder cb) =>
        {
            cb.AddScoped(new ArbitrumTestBlockchainBase.Configuration()
            {
                SuggestGenesisOnStart = true,
            });
        };

        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(preConfigurer);

        Transaction transferTx = Build.A.Transaction
            .WithTo(TestItem.AddressB)
            .WithValue(1_000_000)
            .WithGasLimit(22_000)
            .WithGasPrice(1_000)
            .WithNonce(0)
            .WithSenderAddress(TestItem.AddressA)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        transferTx.Hash = transferTx.CalculateHash();

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        worldState.AddToBalanceAndCreateIfNotExists(transferTx.SenderAddress!, UInt256.MaxValue, chain.SpecProvider.GenesisSpec);
        worldState.RecalculateStateRoot();

        BlockToProduce newBlock =
            new BlockToProduce(
                new BlockHeader(chain.BlockTree.HeadHash, null!, ArbosAddresses.BatchPosterAddress, UInt256.Zero, chain.BlockTree.Head!.Number + 1, 100_000, 100,
                    []), [transferTx], Array.Empty<BlockHeader>(), null);

        UInt256 baseFeePerGas = 1000;
        newBlock.Header.BaseFeePerGas = baseFeePerGas;

        IReadOnlyList<TxReceipt> txReceipts = ProcessBlockWithInternalTx(chain, newBlock);

        AssertCachedL1PriceData(chain, newBlock, txReceipts, transferTx, worldState, baseFeePerGas, (ulong)newBlock.Number, []);

        return chain;
    }

    [Test]
    public void CacheL1PriceData_2BlocksGetProcessed_AddsToCache()
    {
        Helper_CacheL1PriceData_2BlocksGetProcessed_AddsToCache();
    }

    public ArbitrumRpcTestBlockchain Helper_CacheL1PriceData_2BlocksGetProcessed_AddsToCache()
    {
        var preConfigurer = (ContainerBuilder cb) =>
        {
            cb.AddScoped(new ArbitrumTestBlockchainBase.Configuration()
            {
                SuggestGenesisOnStart = true,
            });
        };

        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(preConfigurer);

        Transaction transferTx1 = Build.A.Transaction
            .WithTo(TestItem.AddressB)
            .WithValue(1_000_000)
            .WithGasLimit(22_000) // 21_000 + estimated posting cost (~150) + margin
            .WithGasPrice(1_000)
            .WithNonce(0)
            .WithSenderAddress(TestItem.AddressA)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        transferTx1.Hash = transferTx1.CalculateHash();

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        worldState.AddToBalanceAndCreateIfNotExists(transferTx1.SenderAddress!, UInt256.MaxValue, chain.SpecProvider.GenesisSpec);
        worldState.RecalculateStateRoot();

        BlockToProduce block1 =
            new BlockToProduce(
                new BlockHeader(chain.BlockTree.HeadHash, null!, ArbosAddresses.BatchPosterAddress, UInt256.Zero, chain.BlockTree.Head!.Number + 1, 100_000, 100,
                    []), [transferTx1], Array.Empty<BlockHeader>(), null);

        UInt256 baseFeePerGas = 1000;
        block1.Header.BaseFeePerGas = baseFeePerGas;

        IReadOnlyList<TxReceipt> txReceipts = ProcessBlockWithInternalTx(chain, block1);

        L1PriceDataOfMsg[] l1PriceDataAfterBlock1 = AssertCachedL1PriceData(
            chain, block1, txReceipts, transferTx1, worldState, baseFeePerGas, (ulong)block1.Number, []
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

        transferTx2.Hash = transferTx2.CalculateHash();

        worldState.AddToBalanceAndCreateIfNotExists(transferTx2.SenderAddress!, UInt256.MaxValue, chain.SpecProvider.GenesisSpec);
        worldState.RecalculateStateRoot();

        BlockToProduce block2 =
            new BlockToProduce(
                block1.Header.Clone(), [transferTx2], Array.Empty<BlockHeader>(), null);
        block2.Header.Number = block1.Header.Number + 1;

        txReceipts = ProcessBlockWithInternalTx(chain, block2);

        AssertCachedL1PriceData(chain, block2, txReceipts, transferTx2, worldState, baseFeePerGas, (ulong)block1.Number, l1PriceDataAfterBlock1);

        return chain;
    }

    [Test]
    public void MarkFeedStart_FeedStartEqualToEndOfCache_ResetCache()
    {
        // First, process a block to set the cache

        ArbitrumRpcTestBlockchain chain = Helper_CacheL1PriceData_CacheStartingBlockIs0And1BlockGetsProcessed_OverwritesCache();

        // Second, call MarkFeedStart which resets the cache

        CachedL1PriceData cachedL1PriceData = chain.Container.Resolve<CachedL1PriceData>();
        chain.ArbitrumRpcModule.MarkFeedStart(cachedL1PriceData.EndOfL1PriceDataCache);

        cachedL1PriceData.StartOfL1PriceDataCache.Should().Be(0);
        cachedL1PriceData.EndOfL1PriceDataCache.Should().Be(0);
        cachedL1PriceData.MsgToL1PriceData.Should().BeEmpty();
    }

    [Test]
    public void MarkFeedStart_FeedStartIsWithinCacheWindow_CacheTrimmed()
    {
        // First, process 2 blocks to set the cache

        ArbitrumRpcTestBlockchain chain = Helper_CacheL1PriceData_2BlocksGetProcessed_AddsToCache();

        // Second, call MarkFeedStart which trims the cache

        CachedL1PriceData cachedL1PriceData = chain.Container.Resolve<CachedL1PriceData>();
        Debug.Assert(cachedL1PriceData.MsgToL1PriceData.Length == 2);
        Debug.Assert(cachedL1PriceData.StartOfL1PriceDataCache == 1);
        Debug.Assert(cachedL1PriceData.EndOfL1PriceDataCache == 2);

        L1PriceDataOfMsg msgToL1PriceData = cachedL1PriceData.MsgToL1PriceData[1];

        chain.ArbitrumRpcModule.MarkFeedStart(1);

        cachedL1PriceData.StartOfL1PriceDataCache.Should().Be(2);
        cachedL1PriceData.EndOfL1PriceDataCache.Should().Be(2);
        cachedL1PriceData.MsgToL1PriceData.Should().BeEquivalentTo([msgToL1PriceData]);
    }

    private static IReadOnlyList<TxReceipt> ProcessBlockWithInternalTx(ArbitrumRpcTestBlockchain chain, BlockToProduce block)
    {
        L1IncomingMessageHeader l1Header = new(ArbitrumL1MessageKind.Initialize, Address.Zero, 0, 0, Hash256.Zero, 0);
        ArbitrumTransaction<ArbitrumInternalTx> internalTx = CreateInternalTransaction(l1Header, block.Header, block.Header, chain.ChainSpec.ChainId);

        Transaction[] txsIncludingInternal = block.Transactions.Prepend(internalTx).ToArray();
        // block = (BlockToProduce)block.WithReplacedBody(new(txsIncludingInternal, null));
        block.Transactions = txsIncludingInternal;

        var blockReceiptsTracer = new ArbitrumBlockReceiptTracer((chain.TxProcessor as ArbitrumTransactionProcessor)!.TxExecContext);
        blockReceiptsTracer.StartNewBlockTrace(block);

        chain.BlockProcessor.Process(chain.BlockTree.Head?.StateRoot ?? Keccak.EmptyTreeHash,
            [block], ProcessingOptions.ProducingBlock, blockReceiptsTracer);

        blockReceiptsTracer.EndBlockTrace();

        return blockReceiptsTracer.TxReceipts;
    }

    private static L1PriceDataOfMsg[] AssertCachedL1PriceData(
        ArbitrumRpcTestBlockchain chain,
        Block newBlock,
        IReadOnlyList<TxReceipt> txReceipts,
        Transaction transferTx1,
        IWorldState worldState,
        UInt256 baseFeePerGas,
        ulong expectedCacheStart,
        L1PriceDataOfMsg[] expectedExistingL1PriceData
    )
    {
        CachedL1PriceData cachedL1PriceData = chain.Container.Resolve<CachedL1PriceData>();

        (ulong calldataUnits, ulong posterGas) = GetCalldataUnitsAndPosterGas(baseFeePerGas, transferTx1, worldState);

        txReceipts.Count.Should().Be(2);
        txReceipts[1].Should().BeOfType<ArbitrumTxReceipt>();
        ArbitrumTxReceipt receipt = (txReceipts[1] as ArbitrumTxReceipt)!;
        receipt.GasUsedForL1.Should().Be(posterGas);

        cachedL1PriceData.StartOfL1PriceDataCache.Should().Be(expectedCacheStart);
        cachedL1PriceData.EndOfL1PriceDataCache.Should().Be((ulong)newBlock.Number);

        ulong l1GasCharged = receipt.GasUsedForL1 * baseFeePerGas.ToUInt64(null);

        L1PriceDataOfMsg[] expectedPriceData = [
            .. expectedExistingL1PriceData,
            new L1PriceDataOfMsg(
                calldataUnits,
                expectedExistingL1PriceData.Length == 0 ? calldataUnits : expectedExistingL1PriceData[^1].CummulativeCallDataUnits + calldataUnits,
                l1GasCharged,
                expectedExistingL1PriceData.Length == 0 ? l1GasCharged : expectedExistingL1PriceData[^1].CummulativeL1GasCharged + l1GasCharged
            )
        ];
        cachedL1PriceData.MsgToL1PriceData.Should().BeEquivalentTo(expectedPriceData);

        return cachedL1PriceData.MsgToL1PriceData;
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

    private static ArbitrumTransaction<ArbitrumInternalTx> CreateInternalTransaction(
        L1IncomingMessageHeader l1Header, BlockHeader newHeader, BlockHeader parent, ulong chainId
    )
    {
        var timePassed = newHeader.Timestamp - parent.Timestamp;
        var binaryData = AbiMetadata.PackInput(AbiMetadata.StartBlockMethod, l1Header.BaseFeeL1, l1Header.BlockNumber, newHeader.Number, timePassed);

        var newTransaction = new ArbitrumInternalTx(chainId, binaryData);

        return new ArbitrumTransaction<ArbitrumInternalTx>(newTransaction)
        {
            ChainId = chainId,
            Data = binaryData,
            SenderAddress = ArbosAddresses.ArbosAddress,
            To = ArbosAddresses.ArbosAddress,
            Type = (TxType)ArbitrumTxType.ArbitrumInternal
        };
    }
}
