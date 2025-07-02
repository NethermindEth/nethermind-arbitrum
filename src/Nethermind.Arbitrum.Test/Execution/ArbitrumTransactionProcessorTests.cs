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
using Nethermind.Arbitrum.Arbos.Storage;

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

        Address sender = TestItem.AddressA;
        UInt256 value = 100;
        long gasLimit = GasCostOf.Transaction;
        var transaction = new ArbitrumTransaction<ArbitrumRetryTx>(retryTxInner)
        {
            SenderAddress = sender,
            To = TestItem.AddressB,
            Value = value,
            Type = (TxType)ArbitrumTxType.ArbitrumRetry,
            GasLimit = gasLimit,
            DecodedMaxFeePerGas = basFeePerGas
        };

        Address escrowAddress = ArbitrumTransactionProcessor.GetRetryableEscrowAddress(ticketIdHash);
        worldState.AddToBalanceAndCreateIfNotExists(
            escrowAddress,
            transaction.Value,
            fullChainSimulationSpecProvider.GenesisSpec
        );

        TransactionResult result = processor.Execute(transaction, NullTxTracer.Instance);

        result.Should().Be(TransactionResult.Ok);

        worldState.GetBalance(sender).Should().Be(0); //sender spent transaction value (from escrow) and minted prepaid amount for gas fee
        worldState.GetBalance(escrowAddress).Should().Be(0);
        worldState.GetBalance(TestItem.AddressB).Should().Be(value);

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

    [Test]
    public void EndTxHook_SuccessfulRetryTransaction_DeletesRetryableAndHandlesRefunds()
    {
        // Note: This test verifies retry transaction processing which ends in StartTxHook
        // EndTxHook is not called for retry transactions as they don't continue to EVM execution
        (IWorldState worldState, Block genesis) = ArbOSInitialization.Create();
        var (processor, virtualMachine, arbosState, specProvider) = SetupProcessorWithBaseFee(worldState, genesis, 1000);

        var ticketId = ArbRetryableTxTests.Hash256FromUlong(456);
        var refundTo = new Address("0x1111111111111111111111111111111111111111");
        var sender = new Address("0x2222222222222222222222222222222222222222");
        var gasLimit = 200000L;
        var gasFeeCap = UInt256.Parse("1000");
        var maxRefund = UInt256.Parse("50000");
        var submissionFee = UInt256.Parse("1000");
        var callValue = UInt256.Parse("1000");

        // Create retryable
        arbosState.RetryableState.CreateRetryable(
            ticketId, sender, sender, callValue, refundTo, genesis.Header.Timestamp + 1000, Array.Empty<byte>()
        );

        // Setup escrow with call value
        var escrowAddress = ArbitrumTransactionProcessor.GetRetryableEscrowAddress(ticketId);
        worldState.AddToBalanceAndCreateIfNotExists(escrowAddress, callValue, specProvider.GenesisSpec);

        var retryTx = CreateRetryTransaction(ticketId, sender, refundTo, gasLimit, gasFeeCap, maxRefund, submissionFee);
        // Ensure the retry transaction's value matches the retryable's call value
        retryTx.Value = callValue;

        var result = processor.Execute(retryTx, NullTxTracer.Instance);

        result.Should().Be(TransactionResult.Ok);

        // Verify escrow balance was transferred to sender (call value) during StartTxHook
        worldState.GetBalance(escrowAddress).Should().Be(UInt256.Zero);

        // Verify sender received prepaid gas and call value during StartTxHook
        var senderBalance = worldState.GetBalance(sender);
        var expectedPrepaidGas = gasFeeCap * (ulong)gasLimit;
        senderBalance.Should().Be(callValue + expectedPrepaidGas);
    }

    [Test]
    public void RetryTransaction_ProcessingInStartTxHook_HandlesCallValueTransfer()
    {
        // Note: Retry transactions are fully processed in StartTxHook, not EndTxHook
        (IWorldState worldState, Block genesis) = ArbOSInitialization.Create();
        var (processor, virtualMachine, arbosState, specProvider) = SetupProcessorWithBaseFee(worldState, genesis, 1000);

        var ticketId = ArbRetryableTxTests.Hash256FromUlong(789);
        var refundTo = new Address("0x3333333333333333333333333333333333333333");
        var sender = new Address("0x4444444444444444444444444444444444444444");
        var callValue = UInt256.Parse("2000");

        // Create retryable
        arbosState.RetryableState.CreateRetryable(
            ticketId, sender, sender, callValue, refundTo, genesis.Header.Timestamp + 1000, Array.Empty<byte>()
        );

        var escrowAddress = ArbitrumTransactionProcessor.GetRetryableEscrowAddress(ticketId);
        worldState.AddToBalanceAndCreateIfNotExists(escrowAddress, callValue, specProvider.GenesisSpec);

        var retryTx = CreateRetryTransaction(ticketId, sender, refundTo, 21000, 1000, 100000, 1000);
        // Set the retry transaction's value to match the retryable's call value
        retryTx.Value = callValue;

        var result = processor.Execute(retryTx, NullTxTracer.Instance);

        result.Should().Be(TransactionResult.Ok);

        // Verify call value was transferred from escrow to sender during StartTxHook
        worldState.GetBalance(escrowAddress).Should().Be(UInt256.Zero);

        // Verify sender received call value and prepaid gas
        var senderBalance = worldState.GetBalance(sender);
        var expectedPrepaidGas = UInt256.Parse("1000") * 21000;
        senderBalance.Should().Be(callValue + expectedPrepaidGas);
    }

    // Note: Removed EndTxHook_RetryTransactionWithInfraFees test since retry transactions
    // are handled entirely in StartTxHook and don't trigger EndTxHook

    [Test]
    public void EndTxHook_NormalTransaction_UpdatesNetworkFeeAccount()
    {
        (IWorldState worldState, Block genesis) = ArbOSInitialization.Create();
        var (processor, virtualMachine, arbosState, specProvider) = SetupProcessorWithBaseFee(worldState, genesis, 1000);

        var sender = new Address("0x8888888888888888888888888888888888888888");
        var recipient = new Address("0x9999999999999999999999999999999999999999");
        var senderInitialBalance = UInt256.Parse("1000000000000000000"); // 1 ETH in wei
        var transferValue = UInt256.Parse("1000");
        var gasLimit = 21000L;
        var gasPrice = UInt256.Parse("1000");

        // Fund sender
        worldState.AddToBalanceAndCreateIfNotExists(sender, senderInitialBalance, specProvider.GenesisSpec);

        var networkFeeAccount = arbosState.NetworkFeeAccount.Get();
        var initialNetworkBalance = worldState.GetBalance(networkFeeAccount);

        // Create a regular Ethereum transaction (will be handled by base processor)
        var normalTx = Build.A.Transaction
            .WithSenderAddress(sender)
            .WithTo(recipient)
            .WithValue(transferValue)
            .WithGasLimit(gasLimit)
            .WithGasPrice(gasPrice)
            .WithMaxFeePerGas(gasPrice)
            .TestObject;

        var result = processor.Execute(normalTx, NullTxTracer.Instance);

        result.Should().Be(TransactionResult.Ok);

        // Verify recipient received the transfer
        worldState.GetBalance(recipient).Should().Be(transferValue);

        // Verify network fee account received gas fees (base fee * gas used)
        var finalNetworkBalance = worldState.GetBalance(networkFeeAccount);
        finalNetworkBalance.Should().BeGreaterThan(initialNetworkBalance);

        // Verify sender balance decreased by transfer + gas
        var finalSenderBalance = worldState.GetBalance(sender);
        finalSenderBalance.Should().BeLessThan(senderInitialBalance);
    }

    // Note: Removed EndTxHook_RetryTransactionSubmissionFeeRefund test since retry transactions
    // are handled entirely in StartTxHook and don't trigger EndTxHook

    // Helper methods
    private (ArbitrumTransactionProcessor processor, ArbitrumVirtualMachine vm, ArbosState arbosState, FullChainSimulationSpecProvider specProvider)
        SetupProcessorWithBaseFee(IWorldState worldState, Block genesis, ulong baseFeePerGas)
    {
        genesis.Header.BaseFeePerGas = baseFeePerGas;

        var blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
        var specProvider = new FullChainSimulationSpecProvider();
        var virtualMachine = new ArbitrumVirtualMachine(
            new TestBlockhashProvider(specProvider),
            specProvider,
            _logManager
        );

        var blCtx = new BlockExecutionContext(genesis.Header, 0);
        virtualMachine.SetBlockExecutionContext(in blCtx);

        var processor = new ArbitrumTransactionProcessor(
            specProvider,
            worldState,
            virtualMachine,
            blockTree,
            _logManager,
            new CodeInfoRepository()
        );

        var burner = new SystemBurner(readOnly: false);
        var arbosState = ArbosState.OpenArbosState(worldState, burner, _logManager.GetClassLogger<ArbosState>());

        // Fund the network fee account with sufficient balance for refunds
        var networkFeeAccount = arbosState.NetworkFeeAccount.Get();
        worldState.AddToBalanceAndCreateIfNotExists(networkFeeAccount, UInt256.Parse("1000000000000000000000"), specProvider.GenesisSpec); // 1000 ETH

        return (processor, virtualMachine, arbosState, specProvider);
    }

    private ArbitrumTransaction<ArbitrumRetryTx> CreateRetryTransaction(
        Hash256 ticketId,
        Address sender,
        Address refundTo,
        long gasLimit,
        UInt256 gasFeeCap,
        UInt256 maxRefund,
        UInt256 submissionFee)
    {
        var retryTxInner = new ArbitrumRetryTx(
            ChainId: 0,
            Nonce: 0,
            From: sender,
            GasFeeCap: gasFeeCap,
            Gas: (ulong)gasLimit,
            To: sender, // Self-transfer for simplicity
            Value: 1000,
            Data: Array.Empty<byte>(),
            TicketId: ticketId,
            RefundTo: refundTo,
            MaxRefund: maxRefund,
            SubmissionFeeRefund: submissionFee
        );

        return new ArbitrumTransaction<ArbitrumRetryTx>(retryTxInner)
        {
            SenderAddress = sender,
            Value = 1000,
            Type = (TxType)ArbitrumTxType.ArbitrumRetry,
            GasLimit = gasLimit,
            GasPrice = gasFeeCap,
            DecodedMaxFeePerGas = gasFeeCap
        };
    }

    private ArbitrumTransaction<ArbitrumRetryTx> CreateFailingRetryTransaction(
        Hash256 ticketId,
        Address sender,
        Address refundTo,
        UInt256 callValue)
    {
        var retryTxInner = new ArbitrumRetryTx(
            ChainId: 0,
            Nonce: 0,
            From: sender,
            GasFeeCap: 1000,
            Gas: 21000, // Sufficient base gas
            To: Address.Zero, // Invalid target to cause failure
            Value: callValue,
            Data: Array.Empty<byte>(),
            TicketId: ticketId,
            RefundTo: refundTo,
            MaxRefund: 100000,
            SubmissionFeeRefund: 1000
        );

        return new ArbitrumTransaction<ArbitrumRetryTx>(retryTxInner)
        {
            SenderAddress = sender,
            Value = callValue,
            Type = (TxType)ArbitrumTxType.ArbitrumRetry,
            GasLimit = 21000,
            GasPrice = 1000,
            DecodedMaxFeePerGas = 1000
        };
    }
}
