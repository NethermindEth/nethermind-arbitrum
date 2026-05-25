// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Crypto;
using Nethermind.Serialization.Rlp;
using Nethermind.Core.Test;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Evm.Test;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Execution;

[TestFixture]
public class TransactionProcessorMultiGasTests
{
    [Test]
    public void Execute_InsufficientGasForIntrinsic_FailsTransaction()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, chain.SpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.MainWorldState;
        using System.IDisposable _ = worldState.BeginScope(chain.BlockTree.Head!.Header);

        Address sender = TestItem.AddressA;
        Address receiver = TestItem.AddressB;

        // Transaction with gas limit = 1 (way below intrinsic gas of 21000)
        Transaction tx = Build.A.Transaction
            .WithTo(receiver)
            .WithValue(0)
            .WithGasLimit(1) // Insufficient gas
            .WithMaxFeePerGas(1_000_000_000)
            .WithMaxPriorityFeePerGas(100_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        // Transaction should fail due to insufficient gas
        result.Should().NotBe(TransactionResult.Ok);
    }

    [Test]
    public void Execute_SufficientGas_ChargesPosterGasAsL1Calldata()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
                // Use default L1BaseFee (92) to keep poster gas reasonable
            });
        });

        // Clone header and set BaseFeePerGas > 0 to enable L1 charging branch in GasChargingHook
        // Set coinbase to BatchPosterAddress - required for PosterDataCost to return non-zero
        // Note: GasBeneficiary = Author ?? Beneficiary, so we set both to ensure Coinbase is correct
        // BaseFeePerGas must be > 0 for L1 charging, but not too small (posterGas = posterCost / baseFee)
        BlockHeader testHeader = chain.BlockTree.Head!.Header.Clone();
        testHeader.BaseFeePerGas = 1; // Must be > 0 for L1 charging
        testHeader.Author = ArbosAddresses.BatchPosterAddress; // GasBeneficiary uses Author first
        testHeader.Beneficiary = ArbosAddresses.BatchPosterAddress; // Fallback if Author is null
        BlockExecutionContext blCtx = new(testHeader, chain.SpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.MainWorldState;
        using System.IDisposable _ = worldState.BeginScope(chain.BlockTree.Head!.Header);

        Address sender = TestItem.AddressA;
        Address receiver = TestItem.AddressB;

        // Transaction with plenty of gas and some calldata to ensure L1 costs
        byte[] calldata = new byte[100];
        for (int i = 0; i < calldata.Length; i++)
            calldata[i] = (byte)(i % 256);

        Transaction tx = Build.A.Transaction
            .WithTo(receiver)
            .WithValue(0)
            .WithData(calldata)
            .WithGasLimit(500_000) // Plenty of gas
            .WithMaxFeePerGas(1_000_000_000)
            .WithMaxPriorityFeePerGas(100_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);

        ArbitrumTransactionProcessor processor = (ArbitrumTransactionProcessor)chain.TxProcessor;
        MultiGas gas = processor.TxExecContext.AccumulatedMultiGas;

        gas.Get(ResourceKind.SingleDim).Should().BeGreaterThan(0UL, "expected SingleDim > 0");
    }

    /// <summary>
    /// Tests multi-gas refund calculation for normal transactions.
    /// Validates that refunds are computed per-resource and subtracted from the weighted backlog.
    ///
    /// When multi-gas constraints make certain resources expensive (e.g., StorageGrowth weight=10),
    /// transactions that don't use those expensive resources should have multiDimensionalCost less than
    /// singleGasCost, resulting in a positive refund.
    /// </summary>
    [Test]
    public void MultiGasRefund_WhenExpensiveResourceNotUsed_ProducesPositiveRefund()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Set up constraint that makes StorageGrowth expensive (weight=10) vs Computation (weight=1)
        // StorageGrowth is heavily constrained relative to Computation
        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 1 },
            { ResourceKind.StorageGrowth, 10 },
        };

        // target=100000, window=10, backlog=200_000_000_000 (high backlog to elevate prices)
        l2Pricing.AddMultiGasConstraint(100000, 10, 200_000_000_000, weights);

        // Update pricing model to produce different per-resource fees
        l2Pricing.UpdatePricingModel(100);
        l2Pricing.CommitMultiGasFees();

        UInt256 baseFee = l2Pricing.BaseFeeWeiStorage.Get();
        baseFee.Should().BeGreaterThan(UInt256.Zero, "baseFee should be elevated due to high backlog");

        // Simulate transaction that uses only Computation and StorageAccess (NOT StorageGrowth)
        // This means the expensive resource (StorageGrowth) is not used
        const ulong gasUsed = 1_000_000;
        MultiGas usedMultiGas = default;
        usedMultiGas.Increment(ResourceKind.Computation, gasUsed / 2);
        usedMultiGas.Increment(ResourceKind.StorageAccessRead, gasUsed / 2);

        // Calculate costs
        UInt256 singleGasCost = baseFee * gasUsed;
        UInt256 multiDimensionalCost = l2Pricing.MultiDimensionalPriceForRefund(usedMultiGas);

        // Since StorageGrowth (the expensive resource) wasn't used, multiDimensionalCost should be less
        multiDimensionalCost.Should().BeLessThan(singleGasCost,
            "multiDimensionalCost should be less than singleGasCost when expensive resources aren't used");

        // Calculate refund
        UInt256 expectedRefund = singleGasCost - multiDimensionalCost;
        expectedRefund.Should().BeGreaterThan(UInt256.Zero,
            "refund should be positive when multiDimensionalCost < singleGasCost");
    }

    /// <summary>
    /// Tests multi-gas refund calculation for retryable transactions.
    /// Validates that retryable transactions apply the same per-resource refund logic.
    ///
    /// For retryable transactions, the refund goes to RefundTo address (up to MaxRefund),
    /// with any excess going to the sender.
    /// </summary>
    [Test]
    public void MultiGasRefund_ForRetryableTx_RefundToAddressReceivesRefund()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Set up constraint that makes StorageGrowth expensive
        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 1 },
            { ResourceKind.StorageGrowth, 10 },
        };
        l2Pricing.AddMultiGasConstraint(100000, 10, 200_000_000_000, weights);

        // Update pricing model
        l2Pricing.UpdatePricingModel(100);
        l2Pricing.CommitMultiGasFees();

        UInt256 baseFee = l2Pricing.BaseFeeWeiStorage.Get();

        // Simulate retryable transaction gas usage (similar to normal tx)
        const ulong gasUsed = 1_000_000;
        MultiGas usedMultiGas = default;
        usedMultiGas.Increment(ResourceKind.Computation, gasUsed / 2);
        usedMultiGas.Increment(ResourceKind.StorageAccessRead, gasUsed / 2);

        // For retryables, gasFeeCap is used as the simple gas price
        UInt256 gasFeeCap = baseFee;
        UInt256 simpleGasCost = gasFeeCap * gasUsed;
        UInt256 multiDimensionalCost = l2Pricing.MultiDimensionalPriceForRefund(usedMultiGas);

        UInt256 expectedRefund = simpleGasCost - multiDimensionalCost;
        expectedRefund.Should().BeGreaterThan(UInt256.Zero,
            "retryable tx should have positive refund when expensive resources aren't used");

        // Verify refund distribution logic:
        // If MaxRefund >= expectedRefund: entire refund goes to RefundTo
        // If MaxRefund < expectedRefund: MaxRefund to RefundTo, remainder to From
        UInt256 maxRefund = expectedRefund * 10; // Large MaxRefund
        UInt256 toRefundTo = UInt256.Min(expectedRefund, maxRefund);
        UInt256 toFrom = expectedRefund - toRefundTo;

        toRefundTo.Should().Be(expectedRefund, "with large MaxRefund, entire refund goes to RefundTo");
        toFrom.Should().Be(UInt256.Zero, "with large MaxRefund, nothing goes to From");

        // Test with small MaxRefund
        UInt256 smallMaxRefund = expectedRefund / 2;
        UInt256 toRefundToSmall = UInt256.Min(expectedRefund, smallMaxRefund);
        UInt256 toFromSmall = expectedRefund - toRefundToSmall;

        toRefundToSmall.Should().Be(smallMaxRefund, "with small MaxRefund, only MaxRefund goes to RefundTo");
        toFromSmall.Should().Be(expectedRefund - smallMaxRefund, "remainder goes to From");
    }

    /// <summary>
    /// Tests that when all gas goes to computation only, there's no refund
    /// (multiDimensionalCost equals singleGasCost).
    /// </summary>
    [Test]
    public void MultiGasRefund_WhenAllGasIsComputation_NoRefund()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Set up constraint with only Computation (weight=1)
        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 1 },
        };
        l2Pricing.AddMultiGasConstraint(7_000_000, 60, 0, weights);

        // Use the initialized base fee from state (0.1 gwei = 100_000_000 wei)
        UInt256 baseFee = l2Pricing.BaseFeeWeiStorage.Get();

        // Set multi-gas base fee for computation to match
        l2Pricing.SetNextBlockMultiGasBaseFee(ResourceKind.Computation, baseFee);
        l2Pricing.CommitMultiGasFees();

        // All gas goes to Computation
        const ulong gasUsed = 100_000;
        MultiGas usedMultiGas = default;
        usedMultiGas.Increment(ResourceKind.Computation, gasUsed);

        UInt256 singleGasCost = baseFee * gasUsed;
        UInt256 multiDimensionalCost = l2Pricing.MultiDimensionalPriceForRefund(usedMultiGas);

        // When all gas is computation at same base fee, costs should be equal
        multiDimensionalCost.Should().Be(singleGasCost,
            "when all gas is computation at base fee, no refund should occur");
    }

    [Test]
    public void Execute_SubmitRetryableTransaction_BurnsCorrectMultiGas()
    {
        // ArbitrumSubmitRetryableTransaction skips EVM execution. Its gas must be attributed
        // to ResourceKind.SingleDim so that the retryable
        // submission cost doesn't feed back into the multi-gas backlog as a resource-specific signal.
        UInt256 l1BaseFee = 39;
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(cb =>
        {
            cb.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                L1BaseFee = l1BaseFee,
                FillWithTestDataOnStart = false
            });
        });

        Hash256 ticketId = new(new UInt256(42).ToBigEndian());
        UInt256 gasFeeCap = 1_000_000_000;
        ulong gasLimit = 21_000;
        UInt256 deposit = 10_021_000_000_054_600;

        ArbitrumSubmitRetryableTransaction tx = new()
        {
            ChainId = chain.ChainSpec.ChainId,
            RequestId = ticketId,
            SenderAddress = TestItem.AddressA,
            L1BaseFee = l1BaseFee,
            DepositValue = deposit,
            DecodedMaxFeePerGas = gasFeeCap,
            GasFeeCap = gasFeeCap,
            GasLimit = (long)gasLimit,
            Gas = gasLimit,
            RetryTo = TestItem.AddressB,
            RetryValue = 10_000_000_000_000_000,
            Beneficiary = TestItem.AddressC,
            MaxSubmissionFee = 54_600,
            FeeRefundAddr = TestItem.AddressD,
            RetryData = ReadOnlyMemory<byte>.Empty,
            Data = Array.Empty<byte>(),
            Nonce = 0,
            Mint = deposit,
            Type = (TxType)ArbitrumTxType.ArbitrumSubmitRetryable,
            To = ArbitrumConstants.ArbRetryableTxAddress,
            SourceHash = ticketId
        };
        tx.Hash = tx.CalculateHash();

        IWorldState worldState = chain.MainWorldState;
        using IDisposable dispose = worldState.BeginScope(chain.BlockTree.Head!.Header);
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), Logging.LimboLogs.Instance.GetLogger("arbosState"));

        BlockHeader header = new(chain.BlockTree.HeadHash, null!, TestItem.AddressF, UInt256.Zero,
            0, GasCostOf.Transaction, 100, [])
        {
            BaseFeePerGas = arbosState.L2PricingState.BaseFeeWeiStorage.Get()
        };

        TransactionResult result = chain.TxProcessor.Execute(tx,
            new BlockExecutionContext(header, chain.SpecProvider.GenesisSpec),
            new TestAllTracerWithOutput());
        result.Should().Be(TransactionResult.Ok);

        MultiGas accumulated = ((ArbitrumTransactionProcessor)chain.TxProcessor).TxExecContext.AccumulatedMultiGas;

        accumulated.Get(ResourceKind.SingleDim).Should().BeGreaterThan(0, "submit retryable gas must be attributed to SingleDim");
        accumulated.Total.Should().Be(accumulated.Get(ResourceKind.SingleDim), "SingleDim resource type should be the only gas for submit retryable transactions");
    }
}
