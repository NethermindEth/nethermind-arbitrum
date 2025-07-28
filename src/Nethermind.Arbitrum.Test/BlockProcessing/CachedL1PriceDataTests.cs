using FluentAssertions;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Receipts;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Test.Builders;
using Nethermind.Int256;
using static Nethermind.Arbitrum.Execution.CachedL1PriceData;

namespace Nethermind.Arbitrum.Test.BlockProcessing;

public class CachedL1PriceDataTests
{
    private static readonly UInt256 _baseFeePerGas = 1_000;

    [Test]
    public void CacheL1PriceDataOfMsg_CacheStartingBlockIs0And1BlockGetsProcessed_OverwritesCache()
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

        BlockToProduce block = BlockProcessingUtilities.CreateBlockFromTx(chain, transferTx, _baseFeePerGas);
        ArbitrumTxReceipt receipt = (ArbitrumTxReceipt)BlockProcessingUtilities.ProcessBlockWithInternalTx(chain, block)[1];

        ulong l1GasCharged = receipt.GasUsedForL1 * _baseFeePerGas.ToUInt64(null);
        ulong callDataUnits = BlockProcessingUtilities.GetCallDataUnits(chain.WorldStateManager.GlobalWorldState, transferTx);
        L1PriceDataOfMsg[] expectedL1PriceData = [new(callDataUnits, callDataUnits, l1GasCharged, l1GasCharged)];

        CachedL1PriceData cachedL1PriceData = chain.CachedL1PriceData;
        cachedL1PriceData.StartOfL1PriceDataCache.Should().Be((ulong)block.Number);
        cachedL1PriceData.EndOfL1PriceDataCache.Should().Be((ulong)block.Number);
        cachedL1PriceData.MsgToL1PriceData.Should().BeEquivalentTo(expectedL1PriceData);
    }

    [Test]
    public void CacheL1PriceDataOfMsg_2BlocksGetProcessed_AddsToCache()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumTestBlockchainBase.CreateTestBlockchainWithGenesis();

        // Process first block
        Transaction transferTx1 = Build.A.Transaction
            .WithTo(TestItem.AddressB)
            .WithValue(1_000_000)
            .WithGasLimit(22_000) // 21_000 + estimated posting cost (~150) + margin
            .WithGasPrice(1_000)
            .WithNonce(0)
            .WithSenderAddress(TestItem.AddressA)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        BlockToProduce block1 = BlockProcessingUtilities.CreateBlockFromTx(chain, transferTx1, _baseFeePerGas);
        ArbitrumTxReceipt receipt1 = (ArbitrumTxReceipt)BlockProcessingUtilities.ProcessBlockWithInternalTx(chain, block1)[1];

        ulong l1GasCharged1 = receipt1.GasUsedForL1 * _baseFeePerGas.ToUInt64(null);
        ulong callDataUnits1 = BlockProcessingUtilities.GetCallDataUnits(chain.WorldStateManager.GlobalWorldState, transferTx1);

        // Process second block
        Transaction transferTx2 = Build.A.Transaction
            .WithTo(TestItem.AddressC)
            .WithValue(1_000_000)
            .WithGasLimit(22_000)
            .WithGasPrice(1_000)
            .WithNonce(0)
            .WithSenderAddress(TestItem.AddressA)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        BlockToProduce block2 = BlockProcessingUtilities.CreateBlockFromTx(chain, transferTx2, _baseFeePerGas);
        block2.Header.Number = block1.Header.Number + 1;
        ArbitrumTxReceipt receipt2 = (ArbitrumTxReceipt)BlockProcessingUtilities.ProcessBlockWithInternalTx(chain, block2)[1];

        ulong l1GasCharged2 = receipt2.GasUsedForL1 * _baseFeePerGas.ToUInt64(null);
        ulong callDataUnits2 = BlockProcessingUtilities.GetCallDataUnits(chain.WorldStateManager.GlobalWorldState, transferTx2);

        L1PriceDataOfMsg[] expectedL1PriceData = [
            new(callDataUnits1, callDataUnits1, l1GasCharged1, l1GasCharged1),
            new(callDataUnits2, callDataUnits1 + callDataUnits2, l1GasCharged1, l1GasCharged1 + l1GasCharged2)
        ];

        CachedL1PriceData cachedL1PriceData = chain.CachedL1PriceData;
        cachedL1PriceData.StartOfL1PriceDataCache.Should().Be((ulong)block1.Number);
        cachedL1PriceData.EndOfL1PriceDataCache.Should().Be((ulong)block2.Number);
        cachedL1PriceData.MsgToL1PriceData.Should().BeEquivalentTo(expectedL1PriceData);
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
        BlockToProduce block = BlockProcessingUtilities.CreateBlockFromTx(chain, transferTx, baseFeePerGas);

        BlockProcessingUtilities.ProcessBlockWithInternalTx(chain, block);

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
        BlockToProduce block1 = BlockProcessingUtilities.CreateBlockFromTx(chain, transferTx1, baseFeePerGas);

        BlockProcessingUtilities.ProcessBlockWithInternalTx(chain, block1);

        Transaction transferTx2 = Build.A.Transaction
            .WithTo(TestItem.AddressC)
            .WithValue(1_000_000)
            .WithGasLimit(22_000) // 21_000 + estimated posting cost (~150) + margin
            .WithGasPrice(1_000)
            .WithNonce(0)
            .WithSenderAddress(TestItem.AddressA)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        BlockToProduce block2 = BlockProcessingUtilities.CreateBlockFromTx(chain, transferTx2, baseFeePerGas);
        block2.Header.Number = block1.Header.Number + 1;

        BlockProcessingUtilities.ProcessBlockWithInternalTx(chain, block2);

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
}
