
// using FluentAssertions;
// using Nethermind.Arbitrum.Precompiles;
// using Nethermind.Evm;
// using Nethermind.Logging;
// using Nethermind.State;
// using NUnit.Framework;
// using Nethermind.Evm.Test;
// using Nethermind.Specs;
// using Nethermind.Core.Specs;
// using Nethermind.Specs.Forks;
// using Nethermind.Core;
// using Nethermind.Core.Test.Builders;
// using Nethermind.Int256;
// using Nethermind.Evm.TransactionProcessing;
// using Nethermind.Core.Extensions;
// using Nethermind.Evm.Tracing;
// using Nethermind.Arbitrum.Evm;
// using Nethermind.Arbitrum.Test.Arbos;
// using Nethermind.Core.Crypto;
// using Nethermind.Blockchain;


// namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

// public class ArbRetryableTxParserTests
// {
//     private static readonly ILogManager Logger = LimboLogs.Instance;

//     [Test]
//     public void TicketCreated_EmitTicketCreatedEvent()
//     {
//         // Initialize ArbOS state
//         IWorldState worldState = ArbosGenesisLoaderTests.GenesisLoaderHelper();

//         // Test TicketCreated event directly calling ArbRetryableTx precompile
//         ArbRetryableTx arbRetryableTx = new();
//         ulong gasSupplied = GasCostOf.BalanceEip1884 + 1;
//         ArbitrumPrecompileExecutionContext context = new(Address.Zero, gasSupplied, gasSupplied, NullTxTracer.Instance, false);

//         // Set up the virtual machine
//         ISpecProvider specProvider = new TestSpecProvider(London.Instance);
//         ArbVirtualMachine virtualMachine = new(
//             new TestBlockhashProvider(specProvider),
//             specProvider,
//             Logger
//         );

//         UInt256 ticketId = 123;
//         ArbRetryableTx.TicketCreated(context, virtualMachine, new Hash256(ticketId.ToBigEndian()));

//         string eventSignature = "TicketCreated(bytes32)";
//         Hash256[] expectedEventTopics = new Hash256[] { Keccak.Compute(eventSignature), new Hash256(ticketId.ToBigEndian()) };
//         LogEntry expectedLogEntry = new(ArbRetryableTx.Address, [], expectedEventTopics);

//         // Test TicketCreated event parsing
//         // Assert.That(ticketCreatedEvent.ticketId, Is.EqualTo(ticketId.ToBigEndian()));
//     }

//     [Test]
//     public void ArbRetryableTxParser_GetRedeem_ReturnsCorrectRedeem()
//     {
//         // Initialize ArbOS state
//         IWorldState worldState = ArbosGenesisLoaderTests.GenesisLoaderHelper();

//         // Create test accounts
//         Address senderAccount = TestItem.AddressA;
//         Address testAccount = new("0x0000000000000000000000000000000000000123");

//         UInt256 expectedBalance = 456;

//         // Create the worldstate with the test account whose balance to get
//         worldState.CreateAccount(senderAccount, 1.Ether());
//         worldState.CreateAccount(testAccount, 456);
//         worldState.Commit(London.Instance);

//         // Just making sure balance was correctly set
//         UInt256 actualBalanceFromState = worldState.GetBalance(testAccount);
//         Assert.That(actualBalanceFromState, Is.EqualTo(expectedBalance), "Balance should be set correctly in world state");

//         // Set up the virtual machine and transaction processor
//         ISpecProvider specProvider = new TestSpecProvider(London.Instance);
//         ArbVirtualMachine virtualMachine = new(
//             new TestBlockhashProvider(specProvider),
//             specProvider,
//             Logger
//         );

//         // Create the transaction processor (containing precompiles)
//         SystemTransactionProcessor transactionProcessor = new(
//             specProvider,
//             worldState,
//             virtualMachine,
//             new ArbitrumCodeInfoRepository(new CodeInfoRepository()),
//             Logger
//         );

//         // Create a block for transaction execution
//         Block block = Build.A.Block.WithNumber(0).TestObject;
//         BlockExecutionContext blockExecutionContext = new(block.Header, London.Instance);
//         transactionProcessor.SetBlockExecutionContext(in blockExecutionContext);

//         string getBalanceMethodId = "0xf8b2cb4f";
//         // remove the "0x" and pad with 0s to reach a 32-bytes address
//         string rightAlignedAddress = testAccount.ToString().Substring(2).PadLeft(64, '0');
//         byte[] inputData = Bytes.FromHexString($"{getBalanceMethodId}{rightAlignedAddress}");

//         // Call ArbInfo precompile to get the balance of an account
//         Transaction transferTx = Build.A.Transaction
//             .WithTo(ArbInfo.Address)
//             .WithData(inputData)
//             .WithGasLimit(1_000_000)
//             .WithGasPrice(20)
//             .WithNonce(0)
//             .WithSenderAddress(senderAccount)
//             .TestObject;

//         CallOutputTracer callOutputTracer = new();

//         TransactionResult transferResult = transactionProcessor.Execute(transferTx, callOutputTracer);
//         transferResult.Success.Should().BeTrue();

//         // Test GetBalance directly calling ArbInfo precompile
//         ArbInfo arbInfo = new();
//         ulong gasSupplied = GasCostOf.BalanceEip1884 + 1;
//         ArbitrumPrecompileExecutionContext context = new(Address.Zero, gasSupplied, gasSupplied, NullTxTracer.Instance, false);
//         UInt256 balance = arbInfo.GetBalance(context, virtualMachine, testAccount);

//         Assert.That(balance, Is.EqualTo(expectedBalance), "ArbInfo.GetBalance should return the correct balance");
//         Assert.That(context.GasLeft, Is.EqualTo(1), "ArbInfo.GetBalance should consume the correct amount of gas");

//         // Test GetBalance on ArbInfoParser
//         byte[]? returnedBalance = callOutputTracer.ReturnValue;
//         Assert.That(returnedBalance, Is.EqualTo(expectedBalance.ToBigEndian()), "ArbInfoParser.GetBalance should return the correct balance");
//     }

// }
