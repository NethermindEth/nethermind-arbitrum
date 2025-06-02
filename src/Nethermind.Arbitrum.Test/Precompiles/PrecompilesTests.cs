using FluentAssertions;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Db;
using Nethermind.Evm;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Trie.Pruning;
using NUnit.Framework;
using Nethermind.Evm.Test;
using Nethermind.Specs;
using Nethermind.Core.Specs;
using Nethermind.Specs.Forks;
using Nethermind.Core;
using Nethermind.Core.Test.Builders;
using Nethermind.Int256;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Core.Extensions;
using Nethermind.Evm.Tracing;
using Nethermind.Arbitrum.Evm;

namespace Nethermind.Arbitrum.Test.Precompiles;

public class PrecompilesTests
{
    private static readonly ILogManager Logger = LimboLogs.Instance;

    [Test]
    public void ArbInfo_GetBalance_ReturnsCorrectBalance()
    {
        // Set up the state and virtual machine
        MemDb stateDb = new();
        TrieStore trieStore = new(stateDb, Logger);
        WorldState worldState = new(trieStore, new MemDb(), Logger);

        // Create test accounts
        Address senderAccount = TestItem.AddressA;
        Address testAccount = new("0x0000000000000000000000000000000000000123");

        UInt256 expectedBalance = 1000;

        // Fund the sender account with enough balance
        worldState.CreateAccount(senderAccount, 1.Ether());
        worldState.CreateAccount(testAccount, 0);
        worldState.Commit(London.Instance);

        // Set up the virtual machine and transaction processor
        ISpecProvider specProvider = new TestSpecProvider(London.Instance);
        CodeInfoRepository codeInfoRepository = new();
        ArbVirtualMachine virtualMachine = new(
            new TestBlockhashProvider(specProvider),
            specProvider,
            Logger
        );

        // Create the transaction processor
        TransactionProcessor transactionProcessor = new(
            specProvider,
            worldState,
            virtualMachine,
            codeInfoRepository,
            Logger
        );

        // Create a block for transaction execution
        Block block = Build.A.Block.WithNumber(0).TestObject;
        BlockExecutionContext blockExecutionContext = new(block.Header, London.Instance);

        // Executing a transaction allows to set the world state in the VM object
        // Create a value transfer transaction to set the balance of the test account
        Transaction transferTx = Build.A.Transaction
            .WithTo(testAccount)
            .WithValue(expectedBalance)
            .WithGasLimit(21000)
            .WithGasPrice(20)
            .WithNonce(0)
            .WithSenderAddress(senderAccount)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;
        TransactionResult transferResult = transactionProcessor.Execute(transferTx, blockExecutionContext, NullTxTracer.Instance);
        transferResult.Success.Should().BeTrue();

        // Just making sure balance was correctly set
        UInt256 actualBalanceFromState = worldState.GetBalance(testAccount);
        Assert.That(actualBalanceFromState, Is.EqualTo(expectedBalance), "Balance should be set correctly in world state");

        // Test GetBalance on ArbInfo precompile
        ArbInfo arbInfo = new();
        ulong gasSupplied = GasCostOf.BalanceEip1884 + 1;
        Context context = new(Address.Zero, gasSupplied, gasSupplied, NullTxTracer.Instance, false);
        UInt256 balance = arbInfo.GetBalance(context, virtualMachine, testAccount);

        Assert.That(balance, Is.EqualTo(expectedBalance), "ArbInfo.GetBalance should return the correct balance");
        Assert.That(context.GasLeft, Is.EqualTo(1), "ArbInfo.GetBalance should consume the correct amount of gas");
    }

    [Test]
    public void ArbInfo_GetCode_ReturnsCorrectContractCode()
    {
        // Set up the state and virtual machine
        MemDb stateDb = new();
        TrieStore trieStore = new(stateDb, Logger);
        WorldState worldState = new(trieStore, new MemDb(), Logger);

        // Fund the sender account with enough balance
        Address senderAccount = TestItem.AddressA;
        worldState.CreateAccount(senderAccount, 1000.Ether());
        worldState.Commit(London.Instance);

        ISpecProvider specProvider = new TestSpecProvider(London.Instance);
        ArbVirtualMachine virtualMachine = new(
            new TestBlockhashProvider(specProvider),
            specProvider,
            Logger
        );

        CodeInfoRepository codeInfoRepository = new();
        TransactionProcessor transactionProcessor = new(
            specProvider,
            worldState,
            virtualMachine,
            codeInfoRepository,
            Logger
        );

        // Create a block for transaction execution
        Block block = Build.A.Block.WithNumber(0).TestObject;
        BlockExecutionContext blockExecutionContext = new(block.Header, London.Instance);

        // Contract runtime code
        byte[] runtimeCode = Bytes.FromHexString("0x123456");
        // Initialization code that returns the runtime code on contract deployment
        byte[] codeToDeploy = Prepare.EvmCode.ForInitOf(runtimeCode).Done;

        // Executing a transaction allows to set the world state in the VM object
        // Deploy the contract
        Transaction deployTx = Build.A.Transaction
            .WithCode(codeToDeploy)
            .WithGasLimit(1000000)
            .WithGasPrice(20.GWei())
            .WithNonce(0)
            .WithSenderAddress(senderAccount)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        BlockReceiptsTracer receiptsTracer = new();
        receiptsTracer.SetOtherTracer(NullBlockTracer.Instance);
        receiptsTracer.StartNewBlockTrace(block);
        receiptsTracer.StartNewTxTrace(deployTx);

        TransactionResult deployResult = transactionProcessor.Execute(deployTx, blockExecutionContext, receiptsTracer);
        deployResult.Success.Should().BeTrue();

        receiptsTracer.EndTxTrace();
        receiptsTracer.EndBlockTrace();

        // Get the contract address from the transaction receipt
        TxReceipt receipt = receiptsTracer.TxReceipts![0];
        Assert.That(receipt.TxHash, Is.EqualTo(deployTx.Hash));
        Address deployedContractAddress = receipt.ContractAddress!;

        // Just making sure the contract code was correctly deployed
        byte[] deployedCode = worldState.GetCode(deployedContractAddress);
        deployedCode.Should().BeEquivalentTo(runtimeCode, "Contract code should be deployed correctly");

        // Test GetCode on ArbInfo precompile
        ArbInfo arbInfo = new();
        ulong codeLengthInWords = (ulong)(runtimeCode.Length + 31) / 32;
        ulong gasSupplied = GasCostOf.ColdSLoad + GasCostOf.DataCopy * codeLengthInWords + 1;
        Context context = new(Address.Zero, gasSupplied, gasSupplied, NullTxTracer.Instance, false);
        byte[] code = arbInfo.GetCode(context, virtualMachine, deployedContractAddress);

        code.Should().BeEquivalentTo(runtimeCode, "ArbInfo.GetCode should return the correct code");
        context.GasLeft.Should().Be(1, "ArbInfo.GetCode should consume the correct amount of gas");
    }
}
