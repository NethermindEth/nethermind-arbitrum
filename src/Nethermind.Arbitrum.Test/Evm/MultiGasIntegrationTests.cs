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
    #region SSTORE Tests

    [Test]
    public void Sstore_NewSlotColdAccess_TracksCorrectDimensions()
    {
        ArbitrumRpcTestBlockchain chain = CreateTestBlockchain();
        using IDisposable _ = SetupBlockContext(chain, out IWorldState worldState);

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

        Transaction tx = CreateTransaction(worldState, contractAddress, gasLimit: 100_000);

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
        ArbitrumRpcTestBlockchain chain = CreateTestBlockchain();
        using IDisposable _ = SetupBlockContext(chain, out IWorldState worldState);

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

        Transaction tx = CreateTransaction(worldState, contractAddress, gasLimit: 100_000);

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

    #endregion

    #region CALL Opcode Tests

    [Test]
    public void Execute_Call_TracksMultiGas()
    {
        ArbitrumRpcTestBlockchain chain = CreateTestBlockchain();
        using IDisposable _ = SetupBlockContext(chain, out IWorldState worldState);

        Address targetAddress = new("0x0000000000000000000000000000000000000300");
        byte[] targetCode = Prepare.EvmCode.Op(Instruction.STOP).Done;
        worldState.CreateAccount(targetAddress, 0);
        worldState.InsertCode(targetAddress, targetCode, chain.SpecProvider.GenesisSpec);

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

        Transaction tx = CreateTransaction(worldState, callerAddress, gasLimit: 200_000);

        TestAllTracerWithOutput tracer = new() { IsTracingAccess = false };
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);

        ArbitrumTransactionProcessor processor = (ArbitrumTransactionProcessor)chain.TxProcessor;
        MultiGas gas = processor.TxExecContext.AccumulatedMultiGas;

        ulong gasSpent = (ulong)tracer.GasSpent;
        gas.SingleGas().Should().Be(gasSpent, "SingleGas() must equal gas spent");

        // Cold accesses: the only target contract is cold (2600)
        // The caller contract (tx.to) is pre-warmed by EIP-2929 transaction processing
        AssertMultiGasBreakdown(gas, gasSpent,
            expectedStorageAccess: GasCostOf.ColdAccountAccess,
            expectedStorageGrowth: 0);
    }

    [Test]
    public void Execute_CallCode_TracksMultiGas()
    {
        ArbitrumRpcTestBlockchain chain = CreateTestBlockchain();
        using IDisposable _ = SetupBlockContext(chain, out IWorldState worldState);

        Address targetAddress = new("0x0000000000000000000000000000000000000602");
        byte[] targetCode = Prepare.EvmCode.Op(Instruction.STOP).Done;
        worldState.CreateAccount(targetAddress, 0);
        worldState.InsertCode(targetAddress, targetCode, chain.SpecProvider.GenesisSpec);

        byte[] callerCode = Prepare.EvmCode
            .PushData(0)        // outSize
            .PushData(0)        // outOffset
            .PushData(0)        // inSize
            .PushData(0)        // inOffset
            .PushData(0)        // value
            .PushData(targetAddress)
            .PushData(50_000)   // gas
            .Op(Instruction.CALLCODE)
            .Op(Instruction.POP)
            .Op(Instruction.STOP)
            .Done;

        Address callerAddress = new("0x0000000000000000000000000000000000000603");
        worldState.CreateAccount(callerAddress, 0);
        worldState.InsertCode(callerAddress, callerCode, chain.SpecProvider.GenesisSpec);
        worldState.Commit(chain.SpecProvider.GenesisSpec);

        Transaction tx = CreateTransaction(worldState, callerAddress, gasLimit: 200_000);

        TestAllTracerWithOutput tracer = new() { IsTracingAccess = false };
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);

        ArbitrumTransactionProcessor processor = (ArbitrumTransactionProcessor)chain.TxProcessor;
        MultiGas gas = processor.TxExecContext.AccumulatedMultiGas;

        ulong gasSpent = (ulong)tracer.GasSpent;
        gas.SingleGas().Should().Be(gasSpent, "SingleGas() must equal gas spent");

        // Cold accesses: the only target contract is cold (2600)
        // The caller contract (tx.to) is pre-warmed by EIP-2929 transaction processing
        AssertMultiGasBreakdown(gas, gasSpent,
            expectedStorageAccess: GasCostOf.ColdAccountAccess,
            expectedStorageGrowth: 0);
    }

    [Test]
    public void Execute_DelegateCall_TracksMultiGas()
    {
        ArbitrumRpcTestBlockchain chain = CreateTestBlockchain();
        using IDisposable _ = SetupBlockContext(chain, out IWorldState worldState);

        Address targetAddress = new("0x0000000000000000000000000000000000000400");
        byte[] targetCode = Prepare.EvmCode.Op(Instruction.STOP).Done;
        worldState.CreateAccount(targetAddress, 0);
        worldState.InsertCode(targetAddress, targetCode, chain.SpecProvider.GenesisSpec);

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

        Transaction tx = CreateTransaction(worldState, callerAddress, gasLimit: 200_000);

        TestAllTracerWithOutput tracer = new() { IsTracingAccess = false };
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);

        ArbitrumTransactionProcessor processor = (ArbitrumTransactionProcessor)chain.TxProcessor;
        MultiGas gas = processor.TxExecContext.AccumulatedMultiGas;

        ulong gasSpent = (ulong)tracer.GasSpent;
        gas.SingleGas().Should().Be(gasSpent, "SingleGas() must equal gas spent");

        // Cold accesses: the only target contract is cold (2600)
        // The caller contract (tx.to) is pre-warmed by EIP-2929 transaction processing
        AssertMultiGasBreakdown(gas, gasSpent,
            expectedStorageAccess: GasCostOf.ColdAccountAccess,
            expectedStorageGrowth: 0);
    }

    [Test]
    public void Execute_StaticCall_TracksMultiGas()
    {
        ArbitrumRpcTestBlockchain chain = CreateTestBlockchain();
        using IDisposable _ = SetupBlockContext(chain, out IWorldState worldState);

        Address targetAddress = new("0x0000000000000000000000000000000000000500");
        byte[] targetCode = Prepare.EvmCode.Op(Instruction.STOP).Done;
        worldState.CreateAccount(targetAddress, 0);
        worldState.InsertCode(targetAddress, targetCode, chain.SpecProvider.GenesisSpec);

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

        Transaction tx = CreateTransaction(worldState, callerAddress, gasLimit: 200_000);

        TestAllTracerWithOutput tracer = new() { IsTracingAccess = false };
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);

        ArbitrumTransactionProcessor processor = (ArbitrumTransactionProcessor)chain.TxProcessor;
        MultiGas gas = processor.TxExecContext.AccumulatedMultiGas;

        ulong gasSpent = (ulong)tracer.GasSpent;
        gas.SingleGas().Should().Be(gasSpent, "SingleGas() must equal gas spent");

        // Cold accesses: the only target contract is cold (2600)
        // The caller contract (tx.to) is pre-warmed by EIP-2929 transaction processing
        AssertMultiGasBreakdown(gas, gasSpent,
            expectedStorageAccess: GasCostOf.ColdAccountAccess,
            expectedStorageGrowth: 0);
    }

    [Test]
    public void CallWithValue_ExistingAccount_ChargesValueTransferToComputation()
    {
        ArbitrumRpcTestBlockchain chain = CreateTestBlockchain();
        using IDisposable _ = SetupBlockContext(chain, out IWorldState worldState);

        Address targetAddress = new("0x0000000000000000000000000000000000000700");
        byte[] targetCode = Prepare.EvmCode.Op(Instruction.STOP).Done;
        worldState.CreateAccount(targetAddress, 1); // Has balance, not empty
        worldState.InsertCode(targetAddress, targetCode, chain.SpecProvider.GenesisSpec);

        byte[] callerCode = Prepare.EvmCode
            .PushData(0)        // outSize
            .PushData(0)        // outOffset
            .PushData(0)        // inSize
            .PushData(0)        // inOffset
            .PushData(1)        // value = 1 wei (triggers CallValueTransferGas)
            .PushData(targetAddress)
            .PushData(50_000)   // gas
            .Op(Instruction.CALL)
            .Op(Instruction.POP)
            .Op(Instruction.STOP)
            .Done;

        Address callerAddress = new("0x0000000000000000000000000000000000000701");
        worldState.CreateAccount(callerAddress, 1_000_000);
        worldState.InsertCode(callerAddress, callerCode, chain.SpecProvider.GenesisSpec);
        worldState.Commit(chain.SpecProvider.GenesisSpec);

        Transaction tx = CreateTransaction(worldState, callerAddress, gasLimit: 200_000);

        TestAllTracerWithOutput tracer = new() { IsTracingAccess = false };
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);

        ArbitrumTransactionProcessor processor = (ArbitrumTransactionProcessor)chain.TxProcessor;
        MultiGas gas = processor.TxExecContext.AccumulatedMultiGas;

        ulong gasSpent = (ulong)tracer.GasSpent;
        gas.SingleGas().Should().Be(gasSpent, "SingleGas() must equal gas spent");

        // Cold accesses: only target (2600) - caller (tx.to) is pre-warmed by EIP-2929
        // Value transfer: 9000 → Computation (per gas_table.go:440)
        // No new account (target exists with balance)
        AssertMultiGasBreakdown(gas, gasSpent,
            expectedStorageAccess: GasCostOf.ColdAccountAccess,
            expectedStorageGrowth: 0); // Target exists, not empty
    }

    [Test]
    public void CallToEmptyAccount_NewAccount_ChargesStorageGrowth()
    {
        ArbitrumRpcTestBlockchain chain = CreateTestBlockchain();
        using IDisposable _ = SetupBlockContext(chain, out IWorldState worldState);

        // Empty target address (does not exist)
        Address targetAddress = new("0x0000000000000000000000000000000000000800");
        // Do NOT create the account - it's "empty" per EIP-158

        byte[] callerCode = Prepare.EvmCode
            .PushData(0)        // outSize
            .PushData(0)        // outOffset
            .PushData(0)        // inSize
            .PushData(0)        // inOffset
            .PushData(1)        // value = 1 wei (triggers new account creation)
            .PushData(targetAddress)
            .PushData(50_000)   // gas
            .Op(Instruction.CALL)
            .Op(Instruction.POP)
            .Op(Instruction.STOP)
            .Done;

        Address callerAddress = new("0x0000000000000000000000000000000000000801");
        worldState.CreateAccount(callerAddress, 1_000_000);
        worldState.InsertCode(callerAddress, callerCode, chain.SpecProvider.GenesisSpec);
        worldState.Commit(chain.SpecProvider.GenesisSpec);

        Transaction tx = CreateTransaction(worldState, callerAddress, gasLimit: 200_000);

        TestAllTracerWithOutput tracer = new() { IsTracingAccess = false };
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);

        ArbitrumTransactionProcessor processor = (ArbitrumTransactionProcessor)chain.TxProcessor;
        MultiGas gas = processor.TxExecContext.AccumulatedMultiGas;

        ulong gasSpent = (ulong)tracer.GasSpent;
        gas.SingleGas().Should().Be(gasSpent, "SingleGas() must equal gas spent");

        // Cold accesses: only target (2600) - caller (tx.to) is pre-warmed by EIP-2929
        // New account creation: 25000 → StorageGrowth (per gas_table.go:431)
        // Value transfer: 9000 → Computation (per gas_table.go:440)
        AssertMultiGasBreakdown(gas, gasSpent,
            expectedStorageAccess: GasCostOf.ColdAccountAccess,
            expectedStorageGrowth: GasCostOf.NewAccount); // 25,000 for a new account
    }

    [Test]
    public void CallWarmAccount_SecondAccess_OnlyOneColdCharge()
    {
        ArbitrumRpcTestBlockchain chain = CreateTestBlockchain();
        using IDisposable _ = SetupBlockContext(chain, out IWorldState worldState);

        Address targetAddress = new("0x0000000000000000000000000000000000000900");
        byte[] targetCode = Prepare.EvmCode.Op(Instruction.STOP).Done;
        worldState.CreateAccount(targetAddress, 0);
        worldState.InsertCode(targetAddress, targetCode, chain.SpecProvider.GenesisSpec);

        byte[] callerCode = Prepare.EvmCode
            // First CALL (target is cold)
            .PushData(0)        // outSize
            .PushData(0)        // outOffset
            .PushData(0)        // inSize
            .PushData(0)        // inOffset
            .PushData(0)        // value
            .PushData(targetAddress)
            .PushData(10_000)   // gas
            .Op(Instruction.CALL)
            .Op(Instruction.POP)
            // Second CALL (target is now warm)
            .PushData(0)        // outSize
            .PushData(0)        // outOffset
            .PushData(0)        // inSize
            .PushData(0)        // inOffset
            .PushData(0)        // value
            .PushData(targetAddress)
            .PushData(10_000)   // gas
            .Op(Instruction.CALL)
            .Op(Instruction.POP)
            .Op(Instruction.STOP)
            .Done;

        Address callerAddress = new("0x0000000000000000000000000000000000000901");
        worldState.CreateAccount(callerAddress, 0);
        worldState.InsertCode(callerAddress, callerCode, chain.SpecProvider.GenesisSpec);
        worldState.Commit(chain.SpecProvider.GenesisSpec);

        Transaction tx = CreateTransaction(worldState, callerAddress, gasLimit: 200_000);

        TestAllTracerWithOutput tracer = new() { IsTracingAccess = false };
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);

        ArbitrumTransactionProcessor processor = (ArbitrumTransactionProcessor)chain.TxProcessor;
        MultiGas gas = processor.TxExecContext.AccumulatedMultiGas;

        ulong gasSpent = (ulong)tracer.GasSpent;
        gas.SingleGas().Should().Be(gasSpent, "SingleGas() must equal gas spent");

        // Cold accesses: only target first access (2600) - caller (tx.to) is pre-warmed by EIP-2929
        // Second CALL to target is warm (100) → goes to Computation, not StorageAccess
        AssertMultiGasBreakdown(gas, gasSpent,
            expectedStorageAccess: GasCostOf.ColdAccountAccess, // Only 1 cold access (target)
            expectedStorageGrowth: 0);
    }

    #endregion

    #region CREATE Opcode Tests

    [Test]
    public void Execute_Create_TracksCodeDepositAsStorageGrowth()
    {
        ArbitrumRpcTestBlockchain chain = CreateTestBlockchain();
        using IDisposable _ = SetupBlockContext(chain, out IWorldState worldState);

        // Init code that deploys a 1-byte STOP opcode (0x00):
        // PUSH1 0x00   ; push 0 (STOP opcode value)
        // PUSH1 0x00   ; push 0 (memory offset)
        // MSTORE8      ; store 1 byte at memory[0]
        // PUSH1 0x01   ; push 1 (return size)
        // PUSH1 0x00   ; push 0 (return offset)
        // RETURN       ; return memory[0:1] as deployed code
        // Bytecode: 0x6000600053600160006003f3 (10 bytes)
        byte[] initCode = [0x60, 0x00, 0x60, 0x00, 0x53, 0x60, 0x01, 0x60, 0x00, 0xf3];
        const int deployedCodeLength = 1; // Deploys 1 byte (STOP)

        // Caller code that:
        // 1. Stores init code to memory
        // 2. Calls CREATE with (value=0, offset, size)
        byte[] callerCode = Prepare.EvmCode
            .StoreDataInMemory(0, initCode)
            .PushData(initCode.Length) // size
            .PushData(0)               // offset
            .PushData(0)               // value
            .Op(Instruction.CREATE)
            .Op(Instruction.POP)
            .Op(Instruction.STOP)
            .Done;

        Address callerAddress = new("0x0000000000000000000000000000000000001001");
        worldState.CreateAccount(callerAddress, 1_000_000);
        worldState.InsertCode(callerAddress, callerCode, chain.SpecProvider.GenesisSpec);
        worldState.Commit(chain.SpecProvider.GenesisSpec);

        Transaction tx = CreateTransaction(worldState, callerAddress, gasLimit: 500_000);

        TestAllTracerWithOutput tracer = new() { IsTracingAccess = false };
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);

        ArbitrumTransactionProcessor processor = (ArbitrumTransactionProcessor)chain.TxProcessor;
        MultiGas gas = processor.TxExecContext.AccumulatedMultiGas;

        ulong gasSpent = (ulong)tracer.GasSpent;
        gas.SingleGas().Should().Be(gasSpent, "SingleGas() must equal gas spent");

        // CREATE charges:
        // - NO cold access for created contract (EIP-2929: address added to access list without charge)
        // - CodeDeposit: 200 * deployedCodeLength → StorageGrowth (per Nitro gas_table.go)
        const ulong expectedStorageAccess = 0; // Created address is warm immediately
        const ulong expectedStorageGrowth = GasCostOf.CodeDeposit * deployedCodeLength;

        AssertMultiGasBreakdown(gas, gasSpent,
            expectedStorageAccess: expectedStorageAccess,
            expectedStorageGrowth: expectedStorageGrowth);

        // Additional verification: StorageGrowth includes CodeDeposit
        gas.Get(ResourceKind.StorageGrowth).Should().BeGreaterThanOrEqualTo(expectedStorageGrowth,
            "StorageGrowth must include CodeDeposit (200 gas per deployed byte)");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Asserts multi-gas breakdown matches expected values and verifies dimension sum invariant.
    /// </summary>
    private static void AssertMultiGasBreakdown(
        MultiGas gas,
        ulong gasSpent,
        ulong expectedStorageAccess,
        ulong expectedStorageGrowth,
        ulong expectedHistoryGrowth = 0,
        ulong expectedL2Calldata = 0)
    {
        ulong expectedComputation = gasSpent - expectedStorageAccess - expectedStorageGrowth - expectedHistoryGrowth - expectedL2Calldata;

        gas.Get(ResourceKind.Computation).Should().Be(expectedComputation, "Computation mismatch");
        gas.Get(ResourceKind.StorageAccess).Should().Be(expectedStorageAccess, "StorageAccess mismatch");
        gas.Get(ResourceKind.StorageGrowth).Should().Be(expectedStorageGrowth, "StorageGrowth mismatch");
        gas.Get(ResourceKind.HistoryGrowth).Should().Be(expectedHistoryGrowth, "HistoryGrowth mismatch");
        gas.Get(ResourceKind.L2Calldata).Should().Be(expectedL2Calldata, "L2Calldata mismatch");

        // Verify dimension sum invariant
        ulong sum = expectedComputation + expectedStorageAccess + expectedStorageGrowth + expectedHistoryGrowth + expectedL2Calldata;
        sum.Should().Be(gasSpent, "Sum of all dimensions must equal gasSpent");
    }

    private static ArbitrumRpcTestBlockchain CreateTestBlockchain()
    {
        return ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true,
            });
        });
    }

    private static IDisposable SetupBlockContext(ArbitrumRpcTestBlockchain chain, out IWorldState worldState)
    {
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, chain.SpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        worldState = chain.MainWorldState;
        return worldState.BeginScope(chain.BlockTree.Head!.Header);
    }

    private static Transaction CreateTransaction(
        IWorldState worldState,
        Address to,
        long gasLimit,
        UInt256? value = null)
    {
        Address sender = TestItem.AddressA;
        return Build.A.Transaction
            .WithTo(to)
            .WithValue(value ?? UInt256.Zero)
            .WithGasLimit(gasLimit)
            .WithMaxFeePerGas(1_000_000_000)
            .WithMaxPriorityFeePerGas(100_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;
    }

    #endregion
}
