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
    public void BlockProduction_TracksGasConsumptionInMemory_AndStopsWhenLimitReached()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        IWorldState stateProvider = chain.WorldStateManager.GlobalWorldState;
        using IDisposable dispose = stateProvider.BeginScope(chain.BlockTree.Head!.Header);

        FullChainSimulationSpecProvider specProvider = new();
        UInt256 baseFeePerGas = 1.GWei();

        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(
            stateProvider,
            burner,
            chain.LogManager.GetClassLogger<ArbosState>()
        );

        // Set block gas limit that should allow exactly 2 transactions
        // Simple transfers use ~21k gas each
        ulong blockGasLimit = 50_000;
        arbosState.L2PricingState.PerBlockGasLimitStorage.Set(blockGasLimit);

        // Setup sender with enough balance
        Address sender = TestItem.AddressA;
        stateProvider.CreateAccount(sender, 100.Ether(), 0);
        stateProvider.Commit(specProvider.GenesisSpec);

        // Create 5 transactions, each using ~21k gas for simple transfer
        // With 50k limit and proper tracking: only 2 should fit
        // Without proper tracking all 5 would fit
        Transaction[] transactions = new Transaction[5];
        for (int i = 0; i < 5; i++)
        {
            transactions[i] = Build.A.Transaction
                .WithTo(TestItem.AddressB)
                .WithValue(1.Ether())
                .WithGasLimit(25_000)
                .WithGasPrice(baseFeePerGas)
                .WithNonce((UInt256)i)
                .WithSenderAddress(sender)
                .SignedAndResolved(TestItem.PrivateKeyA)
                .TestObject;
        }

        Block block = Build.A.Block
            .WithNumber(chain.BlockTree.Head!.Number + 1)
            .WithParent(chain.BlockTree.Head!)
            .WithBaseFeePerGas(baseFeePerGas)
            .WithGasLimit(10_000_000)
            .WithBeneficiary(ArbosAddresses.BatchPosterAddress)
            .WithTransactions(transactions)
            .TestObject;

        BlockToProduce blockToProduce = new(block.Header, block.Transactions, block.Uncles);

        ArbitrumChainSpecEngineParameters chainSpecParams = chain.ChainSpec.EngineChainSpecParametersProvider.GetChainSpecParameters<ArbitrumChainSpecEngineParameters>();

        ArbitrumBlockProcessor.ArbitrumBlockProductionTransactionsExecutor txExecutor = new(
            chain.TxProcessor,
            stateProvider,
            new ArbitrumBlockProductionTransactionPicker(specProvider),
            chain.LogManager,
            specProvider,
            chainSpecParams
        );

        BlockReceiptsTracer receiptsTracer = new();
        receiptsTracer.SetOtherTracer(new ArbitrumBlockReceiptTracer(((ArbitrumTransactionProcessor)chain.TxProcessor).TxExecContext));
        receiptsTracer.StartNewBlockTrace(blockToProduce);

        txExecutor.SetBlockExecutionContext(new BlockExecutionContext(block.Header, specProvider.GetSpec(block.Header)));
        txExecutor.ProcessTransactions(blockToProduce, ProcessingOptions.ProducingBlock, receiptsTracer);

        int includedCount = blockToProduce.Transactions.Count();

        includedCount.Should().Be(2,
            "with 50k block gas limit and ~21k gas per tx, exactly 2 transactions should fit");

        includedCount.Should().NotBe(5,
            "block gas limit should prevent all 5 transactions from being included - " +
            "if all 5 are included, blockGasLeft is not being decremented properly");

        // Verify the actual gas consumption
        long totalGasUsed = 0;
        foreach (TxReceipt receipt in receiptsTracer.TxReceipts)
        {
            totalGasUsed += receipt.GasUsed;
        }

        totalGasUsed.Should().BeLessThanOrEqualTo((long)blockGasLimit,
            "total gas used should not exceed block gas limit");

        // Verify storage was never modified, which confirms that the block gas limit was properly tracked in memory
        ArbosState freshArbosState = ArbosState.OpenArbosState(
            stateProvider,
            new SystemBurner(readOnly: false),
            chain.LogManager.GetClassLogger<ArbosState>()
        );

        freshArbosState.L2PricingState.PerBlockGasLimitStorage.Get().Should().Be(blockGasLimit,
            "storage value should never be modified during block production");
    }
}
