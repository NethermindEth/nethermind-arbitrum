// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Blockchain.Tracing.GethStyle;
using Nethermind.Core;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Evm.TransactionProcessing;

namespace Nethermind.Arbitrum.Test.Tracing;

[TestFixture]
public class GasDimensionTracerIntegrationTests
{
    [Test]
    public void TraceTransaction_SimpleTransfer_CapturesGasDimensions()
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
        using IDisposable _ = worldState.BeginScope(chain.BlockTree.Head!.Header);

        Address sender = TestItem.AddressA;
        Address receiver = TestItem.AddressB;
        Transaction tx = Build.A.Transaction
            .WithTo(receiver)
            .WithValue(1_000_000)
            .WithGasLimit(21_000)
            .WithMaxFeePerGas(1_000_000_000)
            .WithMaxPriorityFeePerGas(100_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        Block block = chain.BlockTree.Head!;
        TxGasDimensionLoggerTracer tracer = new(tx, block, GethTraceOptions.Default with { Tracer = TxGasDimensionLoggerTracer.TracerName });
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);

        GethLikeTxTrace trace = tracer.BuildResult();
        TxGasDimensionResult dimensionResult = (TxGasDimensionResult)trace.CustomTracerResult!.Value;

        dimensionResult.GasUsed.Should().Be(21000);
        dimensionResult.Status.Should().Be(1);
        dimensionResult.Failed.Should().BeFalse();
    }

    [Test]
    public void TraceTransaction_ContractCall_CapturesMultipleOpcodes()
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
        using IDisposable _ = worldState.BeginScope(chain.BlockTree.Head!.Header);

        byte[] runtimeCode = Prepare.EvmCode
            .PushData(1)
            .PushData(2)
            .Op(Instruction.ADD)
            .Op(Instruction.POP)
            .Op(Instruction.STOP)
            .Done;

        Address contractAddress = new("0x0000000000000000000000000000000000000200");
        worldState.CreateAccount(contractAddress, 0);
        worldState.InsertCode(contractAddress, runtimeCode, chain.SpecProvider.GenesisSpec);
        worldState.Commit(chain.SpecProvider.GenesisSpec);

        Address sender = TestItem.AddressA;
        Transaction tx = Build.A.Transaction
            .WithTo(contractAddress)
            .WithValue(0)
            .WithGasLimit(100_000)
            .WithMaxFeePerGas(1_000_000_000)
            .WithMaxPriorityFeePerGas(100_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        Block block = chain.BlockTree.Head!;
        TxGasDimensionLoggerTracer tracer = new(tx, block, GethTraceOptions.Default with { Tracer = TxGasDimensionLoggerTracer.TracerName });
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);

        GethLikeTxTrace trace = tracer.BuildResult();
        TxGasDimensionResult dimensionResult = (TxGasDimensionResult)trace.CustomTracerResult!.Value;

        dimensionResult.DimensionLogs.Should().HaveCount(5);
        dimensionResult.DimensionLogs.Should().Contain(log => log.Op == "ADD");
        dimensionResult.DimensionLogs.Should().Contain(log => log.Op == "POP");
        dimensionResult.DimensionLogs.Should().Contain(log => log.Op == "STOP");
    }

    [Test]
    public void TraceTransaction_ByOpcodeTracer_AggregatesByOpcodeType()
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
        using IDisposable _ = worldState.BeginScope(chain.BlockTree.Head!.Header);

        byte[] runtimeCode = Prepare.EvmCode
            .PushData(1)
            .PushData(2)
            .Op(Instruction.ADD)
            .PushData(3)
            .Op(Instruction.ADD)
            .Op(Instruction.POP)
            .Op(Instruction.STOP)
            .Done;

        Address contractAddress = new("0x0000000000000000000000000000000000000201");
        worldState.CreateAccount(contractAddress, 0);
        worldState.InsertCode(contractAddress, runtimeCode, chain.SpecProvider.GenesisSpec);
        worldState.Commit(chain.SpecProvider.GenesisSpec);

        Address sender = TestItem.AddressA;
        Transaction tx = Build.A.Transaction
            .WithTo(contractAddress)
            .WithValue(0)
            .WithGasLimit(100_000)
            .WithMaxFeePerGas(1_000_000_000)
            .WithMaxPriorityFeePerGas(100_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        Block block = chain.BlockTree.Head!;
        TxGasDimensionByOpcodeTracer tracer = new(tx, block, GethTraceOptions.Default with { Tracer = TxGasDimensionByOpcodeTracer.TracerName });
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);

        GethLikeTxTrace trace = tracer.BuildResult();
        TxGasDimensionByOpcodeResult dimensionResult = (TxGasDimensionByOpcodeResult)trace.CustomTracerResult!.Value;

        dimensionResult.Dimensions.Should().ContainKey("ADD");
        GasDimensionBreakdown addBreakdown = dimensionResult.Dimensions["ADD"];
        addBreakdown.OneDimensionalGasCost.Should().Be(6);
    }

    [Test]
    public void TraceTransaction_FailedTransaction_SetsFailed()
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
        using IDisposable _ = worldState.BeginScope(chain.BlockTree.Head!.Header);

        byte[] runtimeCode = Prepare.EvmCode
            .PushData(0)
            .PushData(0)
            .Op(Instruction.REVERT)
            .Done;

        Address contractAddress = new("0x0000000000000000000000000000000000000202");
        worldState.CreateAccount(contractAddress, 0);
        worldState.InsertCode(contractAddress, runtimeCode, chain.SpecProvider.GenesisSpec);
        worldState.Commit(chain.SpecProvider.GenesisSpec);

        Address sender = TestItem.AddressA;
        Transaction tx = Build.A.Transaction
            .WithTo(contractAddress)
            .WithValue(0)
            .WithGasLimit(100_000)
            .WithMaxFeePerGas(1_000_000_000)
            .WithMaxPriorityFeePerGas(100_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        Block block = chain.BlockTree.Head!;
        TxGasDimensionLoggerTracer tracer = new(tx, block, GethTraceOptions.Default with { Tracer = TxGasDimensionLoggerTracer.TracerName });
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);

        GethLikeTxTrace trace = tracer.BuildResult();
        TxGasDimensionResult dimensionResult = (TxGasDimensionResult)trace.CustomTracerResult!.Value;

        dimensionResult.Failed.Should().BeTrue();
        dimensionResult.Status.Should().Be(0);
    }
}
