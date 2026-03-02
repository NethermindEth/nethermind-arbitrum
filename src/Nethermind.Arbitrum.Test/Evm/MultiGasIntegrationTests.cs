// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

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
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Evm;

[TestFixture]
public class MultiGasIntegrationTests
{
    [Test]
    public void Sstore_NewSlotColdAccess_TracksCorrectDimensions()
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

        // Contract that writes to a new storage slot
        // PUSH1 0x42, PUSH1 0x00, SSTORE - write value 0x42 to slot 0
        byte[] runtimeCode = Prepare.EvmCode
            .PushData(0x42)     // value
            .PushData(0)        // key (slot 0 - new slot)
            .Op(Instruction.SSTORE)
            .Op(Instruction.STOP)
            .Done;

        Address contractAddress = new("0x0000000000000000000000000000000000000600");
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

        // Disable IsTracingAccess to test normal cold access behavior
        // (otherwise access tracing pre-warms storage cells and cold access isn't charged)
        TestAllTracerWithOutput tracer = new() { IsTracingAccess = false };
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);

        ArbitrumTransactionProcessor processor = (ArbitrumTransactionProcessor)chain.TxProcessor;
        MultiGas gas = processor.TxExecContext.AccumulatedMultiGas;

        ulong gasSpent = (ulong)tracer.GasSpent;
        gas.SingleGas().Should().Be(gasSpent, "SingleGas() must equal gas spent");

        // SSTORE new slot (cold): StorageAccess = 2100, StorageGrowth = 20000
        // ColdSloadCostEIP2929 = 2100, SstoreSetGasEIP2200 = 20000
        gas.Get(ResourceKind.StorageAccess).Should().Be(2100, "SSTORE cold access = ColdSloadCostEIP2929");
        gas.Get(ResourceKind.StorageGrowth).Should().Be(20000, "SSTORE new slot = SstoreSetGasEIP2200");
        gas.Get(ResourceKind.HistoryGrowth).Should().Be(0, "No LOG operations");

        // Computation = Total - StorageAccess - StorageGrowth - HistoryGrowth (invariant)
        ulong expectedComputation = gasSpent - 2100 - 20000 - 0;
        gas.Get(ResourceKind.Computation).Should().Be(expectedComputation, "Computation = SingleGas - storage dimensions");
    }

    [Test]
    public void Sstore_ExistingSlotColdAccess_TracksCorrectDimensions()
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

        Address contractAddress = new("0x0000000000000000000000000000000000000601");

        // Pre-populate storage slot 0 with non-zero value
        worldState.CreateAccount(contractAddress, 0);
        worldState.Set(new StorageCell(contractAddress, UInt256.Zero), [0x01]);

        // Contract that overwrites an existing storage slot
        // PUSH1 0x42, PUSH1 0x00, SSTORE - write value 0x42 to slot 0 (existing)
        byte[] runtimeCode = Prepare.EvmCode
            .PushData(0x42)     // value
            .PushData(0)        // key (slot 0 - existing slot)
            .Op(Instruction.SSTORE)
            .Op(Instruction.STOP)
            .Done;

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

        // Disable IsTracingAccess to test normal cold access behavior
        TestAllTracerWithOutput tracer = new() { IsTracingAccess = false };
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);

        ArbitrumTransactionProcessor processor = (ArbitrumTransactionProcessor)chain.TxProcessor;
        MultiGas gas = processor.TxExecContext.AccumulatedMultiGas;

        ulong gasSpent = (ulong)tracer.GasSpent;
        gas.SingleGas().Should().Be(gasSpent, "SingleGas() must equal gas spent");

        // SSTORE existing slot (cold): StorageAccess = 2100 + (5000 - 2100) = 5000, StorageGrowth = 0
        // ColdSloadCostEIP2929 = 2100, SstoreResetGasEIP2200 = 5000
        gas.Get(ResourceKind.StorageAccess).Should().Be(5000, "SSTORE cold + reset = ColdSloadCost + (ResetGas - ColdSloadCost)");
        gas.Get(ResourceKind.StorageGrowth).Should().Be(0, "SSTORE existing slot has no StorageGrowth");
        gas.Get(ResourceKind.HistoryGrowth).Should().Be(0, "No LOG operations");

        // Computation = Total - StorageAccess - StorageGrowth - HistoryGrowth (invariant)
        ulong expectedComputation = gasSpent - 5000 - 0 - 0;
        gas.Get(ResourceKind.Computation).Should().Be(expectedComputation, "Computation = SingleGas - storage dimensions");
    }

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

        // gasCreate = pureMemoryGascost → Computation only
        // CREATE dynamic gas goes to Computation only, no StorageGrowth/StorageAccess/HistoryGrowth
        gas.Get(ResourceKind.StorageAccess).Should().Be(0, "CREATE has no StorageAccess");
        gas.Get(ResourceKind.StorageGrowth).Should().Be(0, "CREATE has no StorageGrowth in dynamic gas");
        gas.Get(ResourceKind.HistoryGrowth).Should().Be(0, "No LOG operations");

        // Computation = Total (all gas goes to Computation for CREATE)
        gas.Get(ResourceKind.Computation).Should().Be(gasSpent, "CREATE: all gas goes to Computation");
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
        using IDisposable _ = worldState.BeginScope(chain.BlockTree.Head!.Header);

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

        // gasCreate2 → Computation only (memory + keccak)
        // CREATE2 dynamic gas goes to Computation only, no StorageGrowth/StorageAccess/HistoryGrowth
        gas.Get(ResourceKind.StorageAccess).Should().Be(0, "CREATE2 has no StorageAccess");
        gas.Get(ResourceKind.StorageGrowth).Should().Be(0, "CREATE2 has no StorageGrowth in dynamic gas");
        gas.Get(ResourceKind.HistoryGrowth).Should().Be(0, "No LOG operations");

        // Computation = Total (all gas goes to Computation for CREATE2)
        gas.Get(ResourceKind.Computation).Should().Be(gasSpent, "CREATE2: all gas goes to Computation");
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
        using IDisposable _ = worldState.BeginScope(chain.BlockTree.Head!.Header);

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

        // CALL with no storage operations → all gas goes to Computation
        gas.Get(ResourceKind.StorageAccess).Should().Be(0, "CALL has no StorageAccess");
        gas.Get(ResourceKind.StorageGrowth).Should().Be(0, "CALL has no StorageGrowth");
        gas.Get(ResourceKind.HistoryGrowth).Should().Be(0, "No LOG operations");

        // Computation = Total (all gas goes to Computation for simple CALL)
        gas.Get(ResourceKind.Computation).Should().Be(gasSpent, "CALL: all gas goes to Computation");
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
        using IDisposable _ = worldState.BeginScope(chain.BlockTree.Head!.Header);

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

        // DELEGATECALL with no storage operations → all gas goes to Computation
        gas.Get(ResourceKind.StorageAccess).Should().Be(0, "DELEGATECALL has no StorageAccess");
        gas.Get(ResourceKind.StorageGrowth).Should().Be(0, "DELEGATECALL has no StorageGrowth");
        gas.Get(ResourceKind.HistoryGrowth).Should().Be(0, "No LOG operations");

        // Computation = Total (all gas goes to Computation for simple DELEGATECALL)
        gas.Get(ResourceKind.Computation).Should().Be(gasSpent, "DELEGATECALL: all gas goes to Computation");
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
        using IDisposable _ = worldState.BeginScope(chain.BlockTree.Head!.Header);

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

        // STATICCALL with no storage operations → all gas goes to Computation
        gas.Get(ResourceKind.StorageAccess).Should().Be(0, "STATICCALL has no StorageAccess");
        gas.Get(ResourceKind.StorageGrowth).Should().Be(0, "STATICCALL has no StorageGrowth");
        gas.Get(ResourceKind.HistoryGrowth).Should().Be(0, "No LOG operations");

        // Computation = Total (all gas goes to Computation for simple STATICCALL)
        gas.Get(ResourceKind.Computation).Should().Be(gasSpent, "STATICCALL: all gas goes to Computation");
    }
}
