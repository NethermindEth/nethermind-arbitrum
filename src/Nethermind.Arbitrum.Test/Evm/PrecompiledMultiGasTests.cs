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

namespace Nethermind.Arbitrum.Test.Evm;

[TestFixture]
public class PrecompiledMultiGasTests
{
    // Precompile addresses (Ethereum standard)
    private static readonly Address Sha256Address = Address.FromNumber(2);
    private static readonly Address EcrecoverAddress = Address.FromNumber(1);
    private static readonly Address IdentityAddress = Address.FromNumber(4);

    [Test]
    public void Sha256Precompile_WhenCalled_TracksComputationOnly()
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

        // Contract that calls SHA256 precompile (address 0x02) via STATICCALL
        // Input: "hello world" (11 bytes) stored in memory
        // STATICCALL(gas, addr, inOffset, inSize, outOffset, outSize)
        byte[] runtimeCode = Prepare.EvmCode
            // Store "hello world" (0x68656c6c6f20776f726c64) in memory
            .PushData("hello world"u8.ToArray())
            .PushData(0)
            .Op(Instruction.MSTORE)
            // STATICCALL to SHA256 precompile
            .PushData(32)       // outSize (SHA256 output is 32 bytes)
            .PushData(32)       // outOffset
            .PushData(11)       // inSize (length of "hello world")
            .PushData(21)       // inOffset (32-11=21, right-padded in MSTORE)
            .PushData(Sha256Address)
            .PushData(50_000)   // gas
            .Op(Instruction.STATICCALL)
            .Op(Instruction.POP)
            .Op(Instruction.STOP)
            .Done;

        Address contractAddress = new("0x0000000000000000000000000000000000000700");
        worldState.CreateAccount(contractAddress, 0);
        worldState.InsertCode(contractAddress, runtimeCode, chain.SpecProvider.GenesisSpec);
        worldState.Commit(chain.SpecProvider.GenesisSpec);

        Address sender = TestItem.AddressA;
        Transaction tx = Build.A.Transaction
            .WithTo(contractAddress)
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

        // RunPrecompiledContract returns multigas.ComputationGas(gasCost)
        // Precompile gas goes to Computation only
        gas.Get(ResourceKind.StorageAccess).Should().Be(0, "Precompile has no StorageAccess");
        gas.Get(ResourceKind.StorageGrowth).Should().Be(0, "Precompile has no StorageGrowth");
        gas.Get(ResourceKind.HistoryGrowth).Should().Be(0, "No LOG operations");

        // Computation = Total (all gas goes to Computation for precompiles)
        gas.Get(ResourceKind.Computation).Should().Be(gasSpent, "SHA256: all gas goes to Computation");
    }

    [Test]
    public void EcrecoverPrecompile_WhenCalled_TracksComputationOnly()
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

        // Contract that calls ECRECOVER precompile (address 0x01) via STATICCALL
        // ECRECOVER expects 128 bytes input: hash (32) + v (32) + r (32) + s (32)
        // We'll use dummy input - the precompile will still charge gas
        byte[] runtimeCode = Prepare.EvmCode
            // Store 128 bytes of zeros in memory (dummy signature data)
            .PushData(0)
            .PushData(0)
            .Op(Instruction.MSTORE)
            .PushData(0)
            .PushData(32)
            .Op(Instruction.MSTORE)
            .PushData(0)
            .PushData(64)
            .Op(Instruction.MSTORE)
            .PushData(0)
            .PushData(96)
            .Op(Instruction.MSTORE)
            // STATICCALL to ECRECOVER precompile
            .PushData(32)       // outSize (address output is 32 bytes)
            .PushData(128)      // outOffset
            .PushData(128)      // inSize (128 bytes input)
            .PushData(0)        // inOffset
            .PushData(EcrecoverAddress)
            .PushData(50_000)   // gas
            .Op(Instruction.STATICCALL)
            .Op(Instruction.POP)
            .Op(Instruction.STOP)
            .Done;

        Address contractAddress = new("0x0000000000000000000000000000000000000701");
        worldState.CreateAccount(contractAddress, 0);
        worldState.InsertCode(contractAddress, runtimeCode, chain.SpecProvider.GenesisSpec);
        worldState.Commit(chain.SpecProvider.GenesisSpec);

        Address sender = TestItem.AddressA;
        Transaction tx = Build.A.Transaction
            .WithTo(contractAddress)
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

        // RunPrecompiledContract returns multigas.ComputationGas(gasCost)
        // Precompile gas goes to Computation only
        gas.Get(ResourceKind.StorageAccess).Should().Be(0, "Precompile has no StorageAccess");
        gas.Get(ResourceKind.StorageGrowth).Should().Be(0, "Precompile has no StorageGrowth");
        gas.Get(ResourceKind.HistoryGrowth).Should().Be(0, "No LOG operations");

        // Computation = Total (all gas goes to Computation for precompiles)
        gas.Get(ResourceKind.Computation).Should().Be(gasSpent, "ECRECOVER: all gas goes to Computation");
    }

    [Test]
    public void IdentityPrecompile_WhenCalled_TracksComputationOnly()
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

        // Contract that calls IDENTITY precompile (address 0x04) via STATICCALL
        // IDENTITY just copies input to output
        byte[] runtimeCode = Prepare.EvmCode
            // Store some data in memory
            .PushData(0x1234567890abcdefUL)
            .PushData(0)
            .Op(Instruction.MSTORE)
            // STATICCALL to IDENTITY precompile
            .PushData(32)       // outSize
            .PushData(32)       // outOffset
            .PushData(32)       // inSize
            .PushData(0)        // inOffset
            .PushData(IdentityAddress)
            .PushData(50_000)   // gas
            .Op(Instruction.STATICCALL)
            .Op(Instruction.POP)
            .Op(Instruction.STOP)
            .Done;

        Address contractAddress = new("0x0000000000000000000000000000000000000702");
        worldState.CreateAccount(contractAddress, 0);
        worldState.InsertCode(contractAddress, runtimeCode, chain.SpecProvider.GenesisSpec);
        worldState.Commit(chain.SpecProvider.GenesisSpec);

        Address sender = TestItem.AddressA;
        Transaction tx = Build.A.Transaction
            .WithTo(contractAddress)
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

        // RunPrecompiledContract returns multigas.ComputationGas(gasCost)
        // Precompile gas goes to Computation only
        gas.Get(ResourceKind.StorageAccess).Should().Be(0, "Precompile has no StorageAccess");
        gas.Get(ResourceKind.StorageGrowth).Should().Be(0, "Precompile has no StorageGrowth");
        gas.Get(ResourceKind.HistoryGrowth).Should().Be(0, "No LOG operations");

        // Computation = Total (all gas goes to Computation for precompiles)
        gas.Get(ResourceKind.Computation).Should().Be(gasSpent, "IDENTITY: all gas goes to Computation");
    }
}
