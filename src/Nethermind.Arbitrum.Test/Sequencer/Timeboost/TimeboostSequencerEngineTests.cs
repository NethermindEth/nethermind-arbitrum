// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Threading.Channels;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Modules;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.JsonRpc;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Test.Sequencer.Timeboost;

[TestFixture]
public class TimeboostSequencerEngineTests
{
    [Test]
    public async Task AuctionResolutionTx_WithRegularTxPending_SequencedFirst()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(
            configureArbitrum: c => c.TimeboostEnabled = true);

        ArbitrumExecutionEngine engine = SequencerTestHelpers.CreateEngineWithTimeboost(
            chain,
            out DelayedMessageQueue _,
            out TransactionQueue _,
            out ArbitrumEthRpcModule ethRpcModule,
            out Channel<TxQueueItem> auctionResolutionQueue);

        engine.DigestInitMessage(FullChainSimulationInitMessage.CreateDigestInitMessage(92));
        await SequencerTestHelpers.FundAccountAsync(chain, engine, FullChainSimulationAccounts.AccountA.Address);
        await SequencerTestHelpers.FundAccountAsync(chain, engine, FullChainSimulationAccounts.AccountB.Address);

        // Regular tx from AccountA — enqueued in the normal transaction queue
        Transaction regularTx = SequencerTestHelpers.CreateUserTx(0, TestItem.AddressC, 1.Wei());
        Task<ResultWrapper<Hash256>> regularSendTask = Task.Run(() =>
            ethRpcModule.eth_sendRawTransaction(Rlp.Encode(regularTx).Bytes));

        // Auction resolution tx from AccountB — written directly to the priority queue
        Transaction auctionTx = Build.A.Transaction
            .WithNonce(0).WithGasLimit(21000).WithGasPrice(1.GWei())
            .WithTo(TestItem.AddressC).WithValue(1.Wei()).WithChainId(412346)
            .SignedAndResolved(FullChainSimulationAccounts.AccountB)
            .TestObject;
        await auctionResolutionQueue.Writer.WriteAsync(new TxQueueItem(auctionTx, CancellationToken.None));

        await Task.Delay(50); // allow regular tx to land in the channel

        // First sequencing: auction queue is drained before the regular queue
        ResultWrapper<StartSequencingResult> result1 = await engine.StartSequencingAsync();
        result1.Data.SequencedMsg.Should().NotBeNull("auction resolution tx should produce a block");
        engine.EndSequencing(null);

        regularSendTask.IsCompleted.Should().BeFalse("regular tx must still be pending after the auction block");

        // Second sequencing: regular tx is now picked up
        ResultWrapper<StartSequencingResult> result2 = await engine.StartSequencingAsync();
        result2.Data.SequencedMsg.Should().NotBeNull("regular tx should produce a second block");
        engine.EndSequencing(null);

        ResultWrapper<Hash256> regularResult = await regularSendTask.WaitAsync(TimeSpan.FromSeconds(5));
        regularResult.Result.Should().Be(Result.Success, "regular tx should be included in the second block");
    }

    [Test]
    public async Task TimeboostedTx_Sequenced_IsMarkedInBlockMetadata()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(
            configureArbitrum: c => c.TimeboostEnabled = true);

        ArbitrumExecutionEngine engine = SequencerTestHelpers.CreateEngineWithTimeboost(
            chain,
            out DelayedMessageQueue _,
            out TransactionQueue transactionQueue,
            out ArbitrumEthRpcModule _,
            out Channel<TxQueueItem> _);

        engine.DigestInitMessage(FullChainSimulationInitMessage.CreateDigestInitMessage(92));
        await SequencerTestHelpers.FundAccountAsync(chain, engine, FullChainSimulationAccounts.AccountA.Address);

        // Regular tx: no timeboost bits should be set
        Transaction regularTx = SequencerTestHelpers.CreateUserTx(0, TestItem.AddressB, 1.Wei());
        Task<Exception?> regularEnqueue = transactionQueue.EnqueueAsync(regularTx, CancellationToken.None);
        await Task.Delay(50);

        ResultWrapper<StartSequencingResult> regularResult = await engine.StartSequencingAsync();
        regularResult.Data.SequencedMsg.Should().NotBeNull();
        engine.EndSequencing(null);

        await regularEnqueue.WaitAsync(TimeSpan.FromSeconds(5));
        byte[] regularMeta = regularResult.Data.SequencedMsg!.BlockMetadata;
        regularMeta.Length.Should().BeGreaterThan(1, "bitmap is present when there are transactions");
        regularMeta.Skip(1).Should().OnlyContain(b => b == 0, "regular tx must not set any timeboost bits");

        // Timeboosted tx: at least one bitmap bit should be set
        ulong headBlock = (ulong)chain.BlockTree.Head!.Number;
        Transaction timeboostedTx = SequencerTestHelpers.CreateUserTx(1, TestItem.AddressB, 1.Wei());
        TxQueueItem timeboostedItem = TxQueueItem.CreateTimeboosted(timeboostedTx, CancellationToken.None, blockStamp: headBlock);
        Task<Exception?> boostEnqueue = transactionQueue.EnqueueAsync(timeboostedItem);
        await Task.Delay(50);

        ResultWrapper<StartSequencingResult> boostResult = await engine.StartSequencingAsync();
        boostResult.Data.SequencedMsg.Should().NotBeNull();
        engine.EndSequencing(null);

        await boostEnqueue.WaitAsync(TimeSpan.FromSeconds(5));
        byte[] boostMeta = boostResult.Data.SequencedMsg!.BlockMetadata;
        boostMeta.Length.Should().BeGreaterThan(1);
        boostMeta.Skip(1).Should().Contain(b => b != 0, "timeboosted tx must have at least one bitmap bit set");
    }

    [Test]
    public async Task TimeboostedTx_ExpiredByBlockCount_IsEvictedFromQueue()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(
            configureArbitrum: c =>
            {
                c.TimeboostEnabled = true;
                c.TimeboostQueueTimeoutInBlocks = 0;
            });

        ArbitrumExecutionEngine engine = SequencerTestHelpers.CreateEngineWithTimeboost(
            chain,
            out DelayedMessageQueue _,
            out TransactionQueue transactionQueue,
            out ArbitrumEthRpcModule _,
            out Channel<TxQueueItem> _);

        engine.DigestInitMessage(FullChainSimulationInitMessage.CreateDigestInitMessage(92));
        await SequencerTestHelpers.FundAccountAsync(chain, engine, FullChainSimulationAccounts.AccountA.Address);

        // Stamp the tx at the current head block; with timeout=0 it expires immediately
        ulong currentHead = (ulong)chain.BlockTree.Head!.Number;
        Transaction tx = SequencerTestHelpers.CreateUserTx(0, TestItem.AddressB, 1.Wei());
        TxQueueItem expiredItem = TxQueueItem.CreateTimeboosted(tx, CancellationToken.None, blockStamp: currentHead);

        Task<Exception?> enqueueTask = transactionQueue.EnqueueAsync(expiredItem);
        await Task.Delay(50);

        ResultWrapper<StartSequencingResult> result = await engine.StartSequencingAsync();

        result.Data.SequencedMsg.Should().BeNull("expired timeboosted tx should be evicted, leaving nothing to sequence");

        Exception? error = await enqueueTask.WaitAsync(TimeSpan.FromSeconds(5));
        error.Should().BeOfType<InvalidOperationException>();
        error!.Message.Should().Contain("expired");
    }
}
