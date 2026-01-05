// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Evm.Test;
using Nethermind.Evm.TransactionProcessing;

namespace Nethermind.Arbitrum.Test.Evm;

[TestFixture]
public class MultiGasIntegrationTests
{
    [Test]
    public void Execute_Create_TracksMultiGas()
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

        // Factory contract that deploys a simple contract via CREATE
        // CREATE(value=0, offset=0, size=1) - deploys minimal code
        byte[] runtimeCode = Prepare.EvmCode
            .PushData(0x60)     // Minimal bytecode: PUSH1 0x00
            .PushData(0)        // Store at memory[0]
            .Op(Instruction.MSTORE8)
            .PushData(1)        // size = 1
            .PushData(0)        // offset = 0
            .PushData(0)        // value = 0
            .Op(Instruction.CREATE)
            .Op(Instruction.POP)
            .Op(Instruction.STOP)
            .Done;

        Address factoryAddress = new("0x0000000000000000000000000000000000000200");
        worldState.CreateAccount(factoryAddress, 0);
        worldState.InsertCode(factoryAddress, runtimeCode, chain.SpecProvider.GenesisSpec);
        worldState.Commit(chain.SpecProvider.GenesisSpec);

        Address sender = TestItem.AddressA;
        Transaction tx = Build.A.Transaction
            .WithTo(factoryAddress)
            .WithValue(0)
            .WithGasLimit(200_000)
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

        ulong gasSpent = (ulong)tracer.GasSpent;
        gas.SingleGas().Should().Be(gasSpent, "SingleGas() must equal gas spent");
    }

    [Test]
    public void Execute_Create2_TracksMultiGas()
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

        // Factory contract that deploys via CREATE2
        // CREATE2(value=0, offset=0, size=1, salt=0)
        byte[] runtimeCode = Prepare.EvmCode
            .PushData(0x60)     // Minimal bytecode: PUSH1 0x00
            .PushData(0)        // Store at memory[0]
            .Op(Instruction.MSTORE8)
            .PushData(0)        // salt = 0
            .PushData(1)        // size = 1
            .PushData(0)        // offset = 0
            .PushData(0)        // value = 0
            .Op(Instruction.CREATE2)
            .Op(Instruction.POP)
            .Op(Instruction.STOP)
            .Done;

        Address factoryAddress = new("0x0000000000000000000000000000000000000201");
        worldState.CreateAccount(factoryAddress, 0);
        worldState.InsertCode(factoryAddress, runtimeCode, chain.SpecProvider.GenesisSpec);
        worldState.Commit(chain.SpecProvider.GenesisSpec);

        Address sender = TestItem.AddressA;
        Transaction tx = Build.A.Transaction
            .WithTo(factoryAddress)
            .WithValue(0)
            .WithGasLimit(200_000)
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

        ulong gasSpent = (ulong)tracer.GasSpent;
        gas.SingleGas().Should().Be(gasSpent, "SingleGas() must equal gas spent");
    }

    [Test]
    public void Execute_Call_TracksMultiGas()
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

        // Target contract that just returns
        Address targetAddress = new("0x0000000000000000000000000000000000000300");
        byte[] targetCode = Prepare.EvmCode.Op(Instruction.STOP).Done;
        worldState.CreateAccount(targetAddress, 0);
        worldState.InsertCode(targetAddress, targetCode, chain.SpecProvider.GenesisSpec);

        // Caller contract that calls target via CALL
        // CALL(gas, addr, value, inOffset, inSize, outOffset, outSize)
        byte[] callerCode = Prepare.EvmCode
            .PushData(0)        // outSize
            .PushData(0)        // outOffset
            .PushData(0)        // inSize
            .PushData(0)        // inOffset
            .PushData(0)        // value
            .PushData(targetAddress)
            .PushData(50_000)   // gas
            .Op(Instruction.CALL)
            .Op(Instruction.POP)
            .Op(Instruction.STOP)
            .Done;

        Address callerAddress = new("0x0000000000000000000000000000000000000301");
        worldState.CreateAccount(callerAddress, 0);
        worldState.InsertCode(callerAddress, callerCode, chain.SpecProvider.GenesisSpec);
        worldState.Commit(chain.SpecProvider.GenesisSpec);

        Address sender = TestItem.AddressA;
        Transaction tx = Build.A.Transaction
            .WithTo(callerAddress)
            .WithValue(0)
            .WithGasLimit(200_000)
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

        ulong gasSpent = (ulong)tracer.GasSpent;
        gas.SingleGas().Should().Be(gasSpent, "SingleGas() must equal gas spent");
    }

    [Test]
    public void Execute_DelegateCall_TracksMultiGas()
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

        // Target contract
        Address targetAddress = new("0x0000000000000000000000000000000000000400");
        byte[] targetCode = Prepare.EvmCode.Op(Instruction.STOP).Done;
        worldState.CreateAccount(targetAddress, 0);
        worldState.InsertCode(targetAddress, targetCode, chain.SpecProvider.GenesisSpec);

        // Caller contract that calls target via DELEGATECALL
        // DELEGATECALL(gas, addr, inOffset, inSize, outOffset, outSize)
        byte[] callerCode = Prepare.EvmCode
            .PushData(0)        // outSize
            .PushData(0)        // outOffset
            .PushData(0)        // inSize
            .PushData(0)        // inOffset
            .PushData(targetAddress)
            .PushData(50_000)   // gas
            .Op(Instruction.DELEGATECALL)
            .Op(Instruction.POP)
            .Op(Instruction.STOP)
            .Done;

        Address callerAddress = new("0x0000000000000000000000000000000000000401");
        worldState.CreateAccount(callerAddress, 0);
        worldState.InsertCode(callerAddress, callerCode, chain.SpecProvider.GenesisSpec);
        worldState.Commit(chain.SpecProvider.GenesisSpec);

        Address sender = TestItem.AddressA;
        Transaction tx = Build.A.Transaction
            .WithTo(callerAddress)
            .WithValue(0)
            .WithGasLimit(200_000)
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

        ulong gasSpent = (ulong)tracer.GasSpent;
        gas.SingleGas().Should().Be(gasSpent, "SingleGas() must equal gas spent");
    }

    [Test]
    public void Execute_StaticCall_TracksMultiGas()
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

        // Target contract
        Address targetAddress = new("0x0000000000000000000000000000000000000500");
        byte[] targetCode = Prepare.EvmCode.Op(Instruction.STOP).Done;
        worldState.CreateAccount(targetAddress, 0);
        worldState.InsertCode(targetAddress, targetCode, chain.SpecProvider.GenesisSpec);

        // Caller contract that calls target via STATICCALL
        // STATICCALL(gas, addr, inOffset, inSize, outOffset, outSize)
        byte[] callerCode = Prepare.EvmCode
            .PushData(0)        // outSize
            .PushData(0)        // outOffset
            .PushData(0)        // inSize
            .PushData(0)        // inOffset
            .PushData(targetAddress)
            .PushData(50_000)   // gas
            .Op(Instruction.STATICCALL)
            .Op(Instruction.POP)
            .Op(Instruction.STOP)
            .Done;

        Address callerAddress = new("0x0000000000000000000000000000000000000501");
        worldState.CreateAccount(callerAddress, 0);
        worldState.InsertCode(callerAddress, callerCode, chain.SpecProvider.GenesisSpec);
        worldState.Commit(chain.SpecProvider.GenesisSpec);

        Address sender = TestItem.AddressA;
        Transaction tx = Build.A.Transaction
            .WithTo(callerAddress)
            .WithValue(0)
            .WithGasLimit(200_000)
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

        ulong gasSpent = (ulong)tracer.GasSpent;
        gas.SingleGas().Should().Be(gasSpent, "SingleGas() must equal gas spent");
    }
}
