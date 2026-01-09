// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Evm.Test;
using Nethermind.Evm.TransactionProcessing;

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
        chain.ArbitrumTxProcessor.SetBlockExecutionContext(in blCtx);

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

        TestAllTracerWithOutput<ArbitrumGas> tracer = new();
        TransactionResult result = chain.ArbitrumTxProcessor.Execute(tx, tracer);

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
        chain.ArbitrumTxProcessor.SetBlockExecutionContext(in blCtx);

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

        TestAllTracerWithOutput<ArbitrumGas> tracer = new();
        TransactionResult result = chain.ArbitrumTxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);

        ArbitrumTransactionProcessor processor = chain.ArbitrumTxProcessor;
        MultiGas gas = processor.TxExecContext.AccumulatedMultiGas;

        gas.Get(ResourceKind.L1Calldata).Should().BeGreaterThan(0UL, "expected L1Calldata > 0");
    }
}
