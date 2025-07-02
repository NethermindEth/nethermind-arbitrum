using FluentAssertions;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Arbitrum.Evm;
using Nethermind.Evm.Test;
using Nethermind.State;
using Nethermind.Core;
using Nethermind.Logging;
using Nethermind.Blockchain;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Int256;
using Nethermind.Core.Crypto;
using Nethermind.Evm.Tracing;
using Nethermind.Arbitrum.Test.Precompiles;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Data.Transactions;

namespace Nethermind.Arbitrum.Test.Execution;

public class ArbitrumTransactionProcessorTests
{
    private static readonly ILogManager _logManager = new TestLogManager();

    [Test]
    public void ProcessArbitrumRetryTransaction_RetryableExists_ReturnsOkTransactionResult()
    {
        (IWorldState worldState, Block genesis) = ArbOSInitialization.Create();

        BlockTree blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();

        ArbitrumVirtualMachine virtualMachine = new(
            new TestBlockhashProvider(fullChainSimulationSpecProvider),
            fullChainSimulationSpecProvider,
            _logManager
        );

        ulong basFeePerGas = 10;
        genesis.Header.BaseFeePerGas = basFeePerGas;
        BlockExecutionContext blCtx = new(genesis.Header, 0);
        virtualMachine.SetBlockExecutionContext(in blCtx);

        ArbitrumTransactionProcessor processor = new(
            fullChainSimulationSpecProvider,
            worldState,
            virtualMachine,
            blockTree,
            _logManager,
            new CodeInfoRepository()
        );

        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, burner, _logManager.GetClassLogger<ArbosState>());

        Hash256 ticketIdHash = ArbRetryableTxTests.Hash256FromUlong(123);
        Address refundTo = Address.MaxValue;
        ulong timeout = genesis.Header.Timestamp + 1; // retryable not expired

        arbosState.RetryableState.CreateRetryable(
            ticketIdHash, Address.Zero, Address.Zero, 0, Address.Zero, timeout, []
        );

        ArbitrumRetryTx retryTxInner = new(
            0,
            0,
            Address.Zero,
            0,
            0,
            Address.Zero,
            0,
            Array.Empty<byte>(),
            ticketIdHash, // important
            refundTo, // important
            UInt256.MaxValue,
            0
        );

        Address sender = new("0x0000000000000000000000000000000000000123");
        UInt256 value = 100;
        long gasLimit = 20;
        var transaction = new ArbitrumTransaction<ArbitrumRetryTx>(retryTxInner)
        {
            SenderAddress = sender,
            Value = value,
            Type = (TxType)ArbitrumTxType.ArbitrumRetry,
            GasLimit = gasLimit
        };

        Address escrowAddress = ArbitrumTransactionProcessor.GetRetryableEscrowAddress(ticketIdHash);
        worldState.AddToBalanceAndCreateIfNotExists(
            escrowAddress,
            transaction.Value,
            fullChainSimulationSpecProvider.GenesisSpec
        );

        TransactionResult result = processor.Execute(transaction, NullTxTracer.Instance);

        result.Should().Be(TransactionResult.Ok);

        ulong prepaid = basFeePerGas * (ulong)gasLimit;
        worldState.GetBalance(sender).Should().Be(value + prepaid);
        worldState.GetBalance(escrowAddress).Should().Be(0);

        virtualMachine.ArbitrumTxExecutionContext.CurrentRetryable.Should().Be(ticketIdHash);
        virtualMachine.ArbitrumTxExecutionContext.CurrentRefundTo.Should().Be(refundTo);
    }

    [Test]
    public void ProcessArbitrumRetryTransaction_RetryableDoesNotExist_ReturnsTransactionResultError()
    {
        (IWorldState worldState, Block genesis) = ArbOSInitialization.Create();

        BlockTree blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();
        ArbitrumVirtualMachine virtualMachine = new(
            new TestBlockhashProvider(fullChainSimulationSpecProvider),
            fullChainSimulationSpecProvider,
            _logManager
        );

        ulong basFeePerGas = 10;
        genesis.Header.BaseFeePerGas = basFeePerGas;
        BlockExecutionContext blCtx = new(genesis.Header, 0);
        virtualMachine.SetBlockExecutionContext(in blCtx);

        ArbitrumTransactionProcessor processor = new(
            fullChainSimulationSpecProvider,
            worldState,
            virtualMachine,
            blockTree,
            _logManager,
            new CodeInfoRepository()
        );

        Hash256 ticketIdHash = ArbRetryableTxTests.Hash256FromUlong(123);
        ArbitrumRetryTx retryTxInner = new(
            0,
            0,
            Address.Zero,
            0,
            0,
            Address.Zero,
            0,
            Array.Empty<byte>(),
            ticketIdHash,
            Address.Zero,
            UInt256.MaxValue,
            0
        );

        var transaction = new ArbitrumTransaction<ArbitrumRetryTx>(retryTxInner)
        {
            Type = (TxType)ArbitrumTxType.ArbitrumRetry,
        };

        TransactionResult result = processor.Execute(transaction, NullTxTracer.Instance);

        result.Should().BeEquivalentTo(new TransactionResult($"Retryable with ticketId: {ticketIdHash} not found"));
    }

    [Test]
    public void ProcessArbitrumDepositTransaction_ValidTransaction_ReturnsOkTransactionResult()
    {
        (IWorldState worldState, Block genesis) = ArbOSInitialization.Create();

        BlockTree blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();

        ArbitrumVirtualMachine virtualMachine = new(
            new TestBlockhashProvider(fullChainSimulationSpecProvider),
            fullChainSimulationSpecProvider,
            _logManager
        );

        BlockExecutionContext blCtx = new(genesis.Header, 0);
        virtualMachine.SetBlockExecutionContext(in blCtx);

        ArbitrumTransactionProcessor processor = new(
            fullChainSimulationSpecProvider,
            worldState,
            virtualMachine,
            blockTree,
            _logManager,
            new CodeInfoRepository()
        );

        Address from = new("0x0000000000000000000000000000000000000123");
        Address to = new("0x0000000000000000000000000000000000000456");
        UInt256 value = 100;
        ArbitrumDepositTx depositTx = new(0, Hash256.Zero, from, to, value);

        var transaction = NitroL2MessageParser.ConvertParsedDataToTransaction(depositTx);

        UInt256 initialFromBalance = 10;
        worldState.AddToBalanceAndCreateIfNotExists(
            from,
            initialFromBalance,
            fullChainSimulationSpecProvider.GenesisSpec
        );

        TransactionResult result = processor.Execute(transaction, NullTxTracer.Instance);

        result.Should().Be(TransactionResult.Ok);
        worldState.GetBalance(from).Should().Be(initialFromBalance);
        worldState.GetBalance(to).Should().Be(value);
    }

    [Test]
    public void ProcessArbitrumDepositTransaction_MalformedTx_ReturnsErroneousTransactionResult()
    {
        (IWorldState worldState, Block genesis) = ArbOSInitialization.Create();

        BlockTree blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();

        ArbitrumVirtualMachine virtualMachine = new(
            new TestBlockhashProvider(fullChainSimulationSpecProvider),
            fullChainSimulationSpecProvider,
            _logManager
        );

        BlockExecutionContext blCtx = new(genesis.Header, 0);
        virtualMachine.SetBlockExecutionContext(in blCtx);

        ArbitrumTransactionProcessor processor = new(
            fullChainSimulationSpecProvider,
            worldState,
            virtualMachine,
            blockTree,
            _logManager,
            new CodeInfoRepository()
        );

        ArbitrumDepositTx depositTx = new(0, Hash256.Zero, Address.Zero, Address.Zero, 0);

        var transaction = new ArbitrumTransaction<ArbitrumDepositTx>(depositTx)
        {
            Type = (TxType)ArbitrumTxType.ArbitrumDeposit,
            To = null, // malformed tx
        };

        TransactionResult result = processor.Execute(transaction, NullTxTracer.Instance);
        result.Should().Be(TransactionResult.MalformedTransaction);
    }
}
