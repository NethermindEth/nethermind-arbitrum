// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Receipts;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain.Tracing;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Execution;

public class ArbitrumBlockProcessorTests
{
    [Test]
    public void FirstUserTransaction_WhenBlockGasLimitExceeded_IsAlwaysIncluded()
    {
        TestContext ctx = new(blockGasLimit: 25_000);

        Transaction tx = ctx.CreateTransaction(gasLimit: 50_000, nonce: 0);

        BlockToProduce block = ctx.ExecuteBlock(tx);

        block.Transactions.Count().Should().Be(1,
            "first user transaction must be included even if it exceeds block gas limit - " +
            "this is a protocol liveness guarantee to prevent empty blocks");
    }

    [Test]
    public void SecondUserTransaction_WhenBlockGasLimitExceeded_IsRejected()
    {
        TestContext ctx = new(blockGasLimit: 50_000);

        Transaction tx1 = ctx.CreateTransaction(gasLimit: 25_000, nonce: 0, to: TestItem.AddressB);
        Transaction tx2 = ctx.CreateTransaction(gasLimit: 40_000, nonce: 1, to: TestItem.AddressC);

        BlockToProduce block = ctx.ExecuteBlock(tx1, tx2);

        block.Transactions.Count().Should().Be(1,
            "only first user transaction should be included when second would exceed block gas limit");
        block.Transactions.First().Nonce.Should().Be(0,
            "the included transaction should be the first one");
    }

    [Test]
    public void FirstUserTransaction_WhenInternalTransactionProcessedFirst_StillGetsFirstUserTxBypass()
    {
        TestContext ctx = new(blockGasLimit: 30_000);

        Transaction userTx = ctx.CreateTransaction(gasLimit: 35_000, nonce: 0);

        BlockToProduce block = ctx.ExecuteBlock(userTx);

        block.Transactions.Count().Should().Be(1,
            "user transaction should be included even though internal block start transaction " +
            "was processed first - internal transactions must not count toward user transaction counter");
    }

    [Test]
    public void UserTransactions_WhenMultipleWithinGasLimit_AreAllIncluded()
    {
        TestContext ctx = new(blockGasLimit: 100_000);

        Transaction tx1 = ctx.CreateTransaction(gasLimit: 30_000, nonce: 0, to: TestItem.AddressB);
        Transaction tx2 = ctx.CreateTransaction(gasLimit: 30_000, nonce: 1, to: TestItem.AddressC);

        BlockToProduce block = ctx.ExecuteBlock(tx1, tx2);

        block.Transactions.Count().Should().BeGreaterThanOrEqualTo(2,
            "both user transactions should be included when there is sufficient block gas");
    }

    [Test]
    public void FirstUserTransaction_WhenZeroBlockGasLimit_IsStillIncluded()
    {
        TestContext ctx = new(blockGasLimit: 0);

        Transaction tx = ctx.CreateTransaction(gasLimit: 25_000, nonce: 0);

        BlockToProduce block = ctx.ExecuteBlock(tx);

        block.Transactions.Count().Should().Be(1,
            "even with zero block gas limit, first user transaction must be included " +
            "to guarantee block liveness and prevent empty blocks");
    }

    [Test]
    public void UserTransactions_WhenBlockGasLimitReached_StopsBlockProduction()
    {
        TestContext ctx = new(blockGasLimit: 50_000);

        Transaction[] transactions = Enumerable.Range(0, 5)
            .Select(i => ctx.CreateTransaction(gasLimit: 25_000, nonce: (UInt256)i))
            .ToArray();

        BlockToProduce block = ctx.ExecuteBlock(transactions);

        int includedCount = block.Transactions.Count();

        includedCount.Should().Be(2,
            "with 50k block gas limit and ~21k gas per tx, exactly 2 transactions should fit");
        includedCount.Should().NotBe(5,
            "block gas limit should prevent all 5 transactions from being included");

        long totalGasUsed = ctx.ReceiptsTracer.TxReceipts.Sum(r => r.GasUsed);
        totalGasUsed.Should().BeLessThanOrEqualTo((long)ctx.BlockGasLimit,
            "total gas used should not exceed block gas limit");

        ArbosState freshArbosState = ArbosState.OpenArbosState(
            ctx.StateProvider,
            new SystemBurner(readOnly: false),
            ctx.Chain.LogManager.GetClassLogger<ArbosState>());

        freshArbosState.L2PricingState.PerBlockGasLimitStorage.Get().Should().Be(ctx.BlockGasLimit,
            "storage value should never be modified during block production");
    }

    private class TestContext : IDisposable
    {
        private readonly ArbitrumRpcTestBlockchain _chain;
        private readonly IDisposable _stateScope;
        private readonly UInt256 _baseFeePerGas = 1.GWei();
        private readonly Address _sender = TestItem.AddressA;

        public IWorldState StateProvider { get; }
        public ArbitrumRpcTestBlockchain Chain => _chain;
        public ulong BlockGasLimit { get; }
        public BlockReceiptsTracer ReceiptsTracer { get; }

        public TestContext(ulong blockGasLimit)
        {
            _chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
            {
                builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
                {
                    SuggestGenesisOnStart = true,
                    FillWithTestDataOnStart = true
                });
            });

            StateProvider = _chain.WorldStateManager.GlobalWorldState;
            _stateScope = StateProvider.BeginScope(_chain.BlockTree.Head!.Header);

            FullChainSimulationSpecProvider specProvider = new();
            SystemBurner burner = new(readOnly: false);
            ArbosState arbosState = ArbosState.OpenArbosState(
                StateProvider,
                burner,
                _chain.LogManager.GetClassLogger<ArbosState>());

            BlockGasLimit = blockGasLimit;
            arbosState.L2PricingState.PerBlockGasLimitStorage.Set(blockGasLimit);

            StateProvider.CreateAccount(_sender, 100.Ether(), 0);
            StateProvider.Commit(specProvider.GenesisSpec);

            ReceiptsTracer = new BlockReceiptsTracer();
            ReceiptsTracer.SetOtherTracer(
                new ArbitrumBlockReceiptTracer(
                    ((ArbitrumTransactionProcessor)_chain.TxProcessor).TxExecContext));
        }

        public Transaction CreateTransaction(long gasLimit, UInt256 nonce, Address? to = null)
        {
            return Build.A.Transaction
                .WithTo(to ?? TestItem.AddressB)
                .WithValue(1.Ether())
                .WithGasLimit(gasLimit)
                .WithGasPrice(_baseFeePerGas)
                .WithNonce(nonce)
                .WithSenderAddress(_sender)
                .SignedAndResolved(TestItem.PrivateKeyA)
                .TestObject;
        }

        public BlockToProduce ExecuteBlock(params Transaction[] transactions)
        {
            Block block = Build.A.Block
                .WithNumber(_chain.BlockTree.Head!.Number + 1)
                .WithParent(_chain.BlockTree.Head!)
                .WithBaseFeePerGas(_baseFeePerGas)
                .WithGasLimit(10_000_000)
                .WithBeneficiary(ArbosAddresses.BatchPosterAddress)
                .WithTransactions(transactions)
                .TestObject;

            BlockToProduce blockToProduce = new(block.Header, block.Transactions, block.Uncles);

            FullChainSimulationSpecProvider specProvider = new();
            ArbitrumChainSpecEngineParameters chainSpecParams = _chain.ChainSpec
                .EngineChainSpecParametersProvider
                .GetChainSpecParameters<ArbitrumChainSpecEngineParameters>();

            ArbitrumBlockProcessor.ArbitrumBlockProductionTransactionsExecutor txExecutor = new(
                _chain.TxProcessor,
                StateProvider,
                new ArbitrumBlockProductionTransactionPicker(specProvider),
                _chain.LogManager,
                specProvider,
                chainSpecParams);

            ReceiptsTracer.StartNewBlockTrace(blockToProduce);
            txExecutor.SetBlockExecutionContext(
                new BlockExecutionContext(block.Header, specProvider.GetSpec(block.Header)));
            txExecutor.ProcessTransactions(blockToProduce, ProcessingOptions.ProducingBlock, ReceiptsTracer);

            return blockToProduce;
        }

        public void Dispose()
        {
            _stateScope?.Dispose();
            _chain?.Dispose();
        }
    }
}
