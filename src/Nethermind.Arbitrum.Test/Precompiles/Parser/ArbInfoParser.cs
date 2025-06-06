using FluentAssertions;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Evm;
using Nethermind.Logging;
using Nethermind.State;
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
using Nethermind.Arbitrum.Test.Arbos;
using Nethermind.Core.Crypto;
using Nethermind.Arbitrum.TransactionProcessing;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

public class ArbInfoParserTests
{
    private static readonly ILogManager Logger = LimboLogs.Instance;

    [Test]
    public void ArbInfoParser_GetBalance_ReturnsCorrectBalance()
    {
        // Initialize ArbOS state
        WorldState worldState = ArbosGenesisLoaderTests.GenesisLoaderHelper(Logger);

        // Create test accounts
        Address senderAccount = TestItem.AddressA;
        Address testAccount = new("0x0000000000000000000000000000000000000123");

        UInt256 expectedBalance = 456;

        // Create the worldstate with the test account whose balance to get
        worldState.CreateAccount(senderAccount, 1.Ether());
        worldState.CreateAccount(testAccount, 456);
        worldState.Commit(London.Instance);

        // Just making sure balance was correctly set
        UInt256 actualBalanceFromState = worldState.GetBalance(testAccount);
        Assert.That(actualBalanceFromState, Is.EqualTo(expectedBalance), "Balance should be set correctly in world state");

        // Set up the virtual machine and transaction processor
        ISpecProvider specProvider = new TestSpecProvider(London.Instance);
        ArbVirtualMachine virtualMachine = new(
            new TestBlockhashProvider(specProvider),
            specProvider,
            Logger
        );

        // Create the transaction processor (containing precompiles)
        ArbitrumTransactionProcessor transactionProcessor = new(
            specProvider,
            worldState,
            virtualMachine,
            new CodeInfoRepository(),
            Logger
        );

        // Create a block for transaction execution
        Block block = Build.A.Block.WithNumber(0).TestObject;
        BlockExecutionContext blockExecutionContext = new(block.Header, London.Instance);

        string getBalanceMethodId = "0xf8b2cb4f";
        // remove the "0x" and pad with 0s to reach a 32-bytes address
        string rightAlignedAddress = testAccount.ToString().Substring(2).PadLeft(64, '0');
        byte[] inputData = Bytes.FromHexString($"{getBalanceMethodId}{rightAlignedAddress}");

        // Call ArbInfo precompile to get the balance of an account
        Transaction transferTx = Build.A.Transaction
            .WithTo(ArbInfo.Address)
            .WithData(inputData)
            .WithGasLimit(1_000_000)
            .WithGasPrice(20)
            .WithNonce(0)
            .WithSenderAddress(senderAccount)
            .TestObject;

        CallOutputTracer callOutputTracer = new();

        TransactionResult transferResult = transactionProcessor.Execute(transferTx, blockExecutionContext, callOutputTracer);
        transferResult.Success.Should().BeTrue();

        // Test GetBalance directly calling ArbInfo precompile
        ArbInfo arbInfo = new();
        ulong gasSupplied = GasCostOf.BalanceEip1884 + 1;
        Context context = new(Address.Zero, gasSupplied, gasSupplied, NullTxTracer.Instance, false);
        UInt256 balance = arbInfo.GetBalance(context, virtualMachine, testAccount);

        Assert.That(balance, Is.EqualTo(expectedBalance), "ArbInfo.GetBalance should return the correct balance");
        Assert.That(context.GasLeft, Is.EqualTo(1), "ArbInfo.GetBalance should consume the correct amount of gas");

        // Test GetBalance on ArbInfoParser
        byte[]? returnedBalance = callOutputTracer.ReturnValue;
        Assert.That(returnedBalance, Is.EqualTo(expectedBalance.ToBigEndian()), "ArbInfoParser.GetBalance should return the correct balance");
    }

    [Test]
    public void ArbInfoParser_GetCode_ReturnsCorrectCode()
    {
        // Initialize ArbOS state
        WorldState worldState = ArbosGenesisLoaderTests.GenesisLoaderHelper(Logger);

        // Create test accounts
        Address senderAccount = TestItem.AddressA;
        Address someContract = new("0x0000000000000000000000000000000000000123");

        // Create the worldstate with the deployed contract whose code to get
        worldState.CreateAccount(senderAccount, 1.Ether());
        worldState.CreateAccount(someContract, 0);
        byte[] runtimeCode = Bytes.FromHexString("0x0000000000000000000000000000000000000000000000000000000000123456");
        worldState.InsertCode(someContract, new ValueHash256(runtimeCode), runtimeCode, London.Instance, false);
        worldState.Commit(London.Instance);

        // Just making sure the contract code was correctly deployed
        byte[] deployedCode = worldState.GetCode(someContract);
        deployedCode.Should().BeEquivalentTo(runtimeCode, "Contract code should be deployed correctly");

        // Set up the virtual machine and transaction processor
        ISpecProvider specProvider = new TestSpecProvider(London.Instance);
        ArbVirtualMachine virtualMachine = new(
            new TestBlockhashProvider(specProvider),
            specProvider,
            Logger
        );

        // Create the transaction processor (containing precompiles)
        ArbitrumTransactionProcessor transactionProcessor = new(
            specProvider,
            worldState,
            virtualMachine,
            new CodeInfoRepository(),
            Logger
        );

        // Create a block for transaction execution
        Block block = Build.A.Block.WithNumber(0).TestObject;
        BlockExecutionContext blockExecutionContext = new(block.Header, London.Instance);

        string getCodeMethodId = "0x7e105ce2";
        // remove the "0x" and pad with 0s to reach a 32-bytes address
        string rightAlignedAddress = someContract.ToString().Substring(2).PadLeft(64, '0');
        byte[] inputData = Bytes.FromHexString($"{getCodeMethodId}{rightAlignedAddress}");

        // Call ArbInfo precompile to get the code of a contract
        Transaction transferTx = Build.A.Transaction
            .WithTo(ArbInfo.Address)
            .WithData(inputData)
            .WithGasLimit(1_000_000)
            .WithGasPrice(20)
            .WithNonce(0)
            .WithSenderAddress(senderAccount)
            .TestObject;

        CallOutputTracer callOutputTracer = new();

        TransactionResult transferResult = transactionProcessor.Execute(transferTx, blockExecutionContext, callOutputTracer);
        transferResult.Success.Should().BeTrue();

        // Test GetCode directly calling ArbInfo precompile
        ArbInfo arbInfo = new();
        ulong codeLengthInWords = (ulong)(runtimeCode.Length + 31) / 32;
        ulong gasSupplied = GasCostOf.ColdSLoad + GasCostOf.DataCopy * codeLengthInWords + 1;
        Context context = new(Address.Zero, gasSupplied, gasSupplied, NullTxTracer.Instance, false);
        byte[] code = arbInfo.GetCode(context, virtualMachine, someContract);

        code.Should().BeEquivalentTo(runtimeCode, "ArbInfo.GetCode should return the correct code");
        context.GasLeft.Should().Be(1, "ArbInfo.GetCode should consume the correct amount of gas");

        // Test GetCode on ArbInfoParser
        byte[]? returnedCode = callOutputTracer.ReturnValue;
        returnedCode.Should().BeEquivalentTo(runtimeCode, "ArbInfoParser.GetCode should return the correct code");
    }
}