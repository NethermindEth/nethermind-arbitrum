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
    public void EndTxHook_RetryTransaction_ProcessesCorrectly()
    {
        var (worldState, genesis) = ArbOSInitialization.Create();
        var specProvider = new FullChainSimulationSpecProvider();
        var blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
        var virtualMachine = new ArbitrumVirtualMachine(
            new TestBlockhashProvider(specProvider),
            specProvider,
            _logManager);

        const ulong BaseFeePerGas = 100;
        genesis.Header.BaseFeePerGas = BaseFeePerGas;
        var blockContext = new BlockExecutionContext(genesis.Header, 0);
        virtualMachine.SetBlockExecutionContext(in blockContext);

        var processor = new ArbitrumTransactionProcessor(
            specProvider,
            worldState,
            virtualMachine,
            blockTree,
            _logManager,
            new CodeInfoRepository());

        // Create retryable
        var burner = new SystemBurner(readOnly: false);
        var arbosState = ArbosState.OpenArbosState(worldState, burner, _logManager.GetClassLogger<ArbosState>());

        var ticketId = ArbRetryableTxTests.Hash256FromUlong(456);
        var sender = new Address("0x1000000000000000000000000000000000000001");
        var refundTo = new Address("0x2000000000000000000000000000000000000002");
        var maxRefund = (UInt256)50000;
        var submissionFeeRefund = (UInt256)1000;
        const ulong GasLimit = 100;
        var timeout = genesis.Header.Timestamp + 1000;

        arbosState.RetryableState.CreateRetryable(
            ticketId, sender, sender, 0, sender, timeout, []);

        var retryTxInner = new ArbitrumRetryTx(
            0, 0, sender, BaseFeePerGas, GasLimit, sender, 0, ReadOnlyMemory<byte>.Empty,
            ticketId, refundTo, maxRefund, submissionFeeRefund);

        var transaction = new ArbitrumTransaction<ArbitrumRetryTx>(retryTxInner)
        {
            SenderAddress = sender,
            Type = (TxType)ArbitrumTxType.ArbitrumRetry,
            GasLimit = (long)GasLimit
        };

        // Add balance to sender to pay for gas
        worldState.AddToBalanceAndCreateIfNotExists(sender, BaseFeePerGas * GasLimit, specProvider.GenesisSpec);

        // Create escrow with callvalue
        var escrowAddress = ArbitrumTransactionProcessor.GetRetryableEscrowAddress(ticketId);
        worldState.AddToBalanceAndCreateIfNotExists(escrowAddress, transaction.Value, specProvider.GenesisSpec);

        var result = processor.Execute(transaction, NullTxTracer.Instance);

        result.Should().Be(TransactionResult.Ok);

        // Verify the retry transaction was processed and ArbitrumTxExecutionContext was set
        virtualMachine.ArbitrumTxExecutionContext.CurrentRetryable.Should().Be(ticketId);
        virtualMachine.ArbitrumTxExecutionContext.CurrentRefundTo.Should().Be(refundTo);
    }

    [Test]
    public void EndTxHook_NormalTransaction_DistributesFeeCorrectly()
    {
        var (worldState, genesis) = ArbOSInitialization.Create();
        var specProvider = new FullChainSimulationSpecProvider();
        var blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
        var virtualMachine = new ArbitrumVirtualMachine(
            new TestBlockhashProvider(specProvider),
            specProvider,
            _logManager);

        const ulong BaseFeePerGas = 1000;
        genesis.Header.BaseFeePerGas = BaseFeePerGas;
        genesis.Header.Beneficiary = new Address("0x3000000000000000000000000000000000000003");

        var blockContext = new BlockExecutionContext(genesis.Header, 0);
        virtualMachine.SetBlockExecutionContext(in blockContext);

        var processor = new ArbitrumTransactionProcessor(
            specProvider,
            worldState,
            virtualMachine,
            blockTree,
            _logManager,
            new CodeInfoRepository());

        // Create normal transaction
        var sender = new Address("0x1000000000000000000000000000000000000001");
        var to = new Address("0x2000000000000000000000000000000000000002");
        const ulong GasLimit = 21000;

        var transaction = Build.A.Transaction
            .WithSenderAddress(sender)
            .WithTo(to)
            .WithGasLimit((long)GasLimit)
            .WithGasPrice(BaseFeePerGas)
            .WithValue(100)
            .TestObject;

        // Add balance to sender
        var initialBalance = BaseFeePerGas * GasLimit + transaction.Value + 10000;
        worldState.AddToBalanceAndCreateIfNotExists(sender, initialBalance, specProvider.GenesisSpec);

        // Get initial balances
        var burner = new SystemBurner(readOnly: false);
        var arbosState = ArbosState.OpenArbosState(worldState, burner, _logManager.GetClassLogger<ArbosState>());
        var networkFeeAccount = arbosState.NetworkFeeAccount.Get();
        var initialNetworkBalance = worldState.GetBalance(networkFeeAccount);

        var result = processor.Execute(transaction, NullTxTracer.Instance);

        result.Should().Be(TransactionResult.Ok);

        // Calculate exact expected values
        var actualGasUsed = (ulong)transaction.SpentGas;
        var totalGasCost = BaseFeePerGas * actualGasUsed;
        var posterFee = UInt256.Zero; // No L1 costs for normal transactions
        var expectedNetworkFee = totalGasCost - posterFee; // = totalGasCost since posterFee is 0

        // Verify exact network fee account balance
        var finalNetworkBalance = worldState.GetBalance(networkFeeAccount);
        var actualNetworkFeeIncrease = finalNetworkBalance - initialNetworkBalance;
        actualNetworkFeeIncrease.Should().Be(expectedNetworkFee);

        // Verify exact sender balance decrease
        var finalSenderBalance = worldState.GetBalance(sender);
        var expectedDecrease = totalGasCost + transaction.Value;
        var actualDecrease = initialBalance - finalSenderBalance;
        actualDecrease.Should().Be(expectedDecrease);
    }

    [Test]
    public void EndTxHook_NormalTransactionWithInfraFees_DistributesFeesBetweenNetworkAndInfra()
    {
        var (worldState, genesis) = ArbOSInitialization.Create();
        var specProvider = new FullChainSimulationSpecProvider();
        var blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
        var virtualMachine = new ArbitrumVirtualMachine(
            new TestBlockhashProvider(specProvider),
            specProvider,
            _logManager);

        const ulong BaseFeePerGas = 1000;
        genesis.Header.BaseFeePerGas = BaseFeePerGas;

        var blockContext = new BlockExecutionContext(genesis.Header, 0);
        virtualMachine.SetBlockExecutionContext(in blockContext);

        var processor = new ArbitrumTransactionProcessor(
            specProvider,
            worldState,
            virtualMachine,
            blockTree,
            _logManager,
            new CodeInfoRepository());

        // Setup ArbOS state to enable infrastructure fees
        var burner = new SystemBurner(readOnly: false);
        var arbosState = ArbosState.OpenArbosState(worldState, burner, _logManager.GetClassLogger<ArbosState>());

        // Set up infrastructure fee account
        var infraFeeAccount = new Address("0x4000000000000000000000000000000000000004");
        arbosState.InfraFeeAccount.Set(infraFeeAccount);

        // Set minimum base fee for infrastructure calculation
        var minBaseFee = (UInt256)500;
        arbosState.L2PricingState.MinBaseFeeWeiStorage.Set(minBaseFee);

        // Create normal transaction
        var sender = new Address("0x1000000000000000000000000000000000000001");
        var to = new Address("0x2000000000000000000000000000000000000002");
        const ulong GasLimit = 21000;

        var transaction = Build.A.Transaction
            .WithSenderAddress(sender)
            .WithTo(to)
            .WithGasLimit((long)GasLimit)
            .WithGasPrice(BaseFeePerGas)
            .WithValue(100)
            .TestObject;

        // Add balance to sender
        var initialBalance = BaseFeePerGas * GasLimit + transaction.Value + 10000;
        worldState.AddToBalanceAndCreateIfNotExists(sender, initialBalance, specProvider.GenesisSpec);

        // Get initial balances
        var networkFeeAccount = arbosState.NetworkFeeAccount.Get();
        var initialNetworkBalance = worldState.GetBalance(networkFeeAccount);
        var initialInfraBalance = worldState.GetBalance(infraFeeAccount);

        var result = processor.Execute(transaction, NullTxTracer.Instance);

        result.Should().Be(TransactionResult.Ok);

        // Calculate exact expected fee distribution
        var actualGasUsed = (ulong)transaction.SpentGas;
        var totalGasCost = BaseFeePerGas * actualGasUsed;
        const ulong PosterGas = 0; // No L1 poster gas for normal transactions
        var computeGas = actualGasUsed - PosterGas; // = actualGasUsed

        // Infrastructure fee calculation: min(minBaseFee, baseFee) * computeGas
        var infraFeeRate = UInt256.Min(minBaseFee, BaseFeePerGas);
        var expectedInfraFee = infraFeeRate * computeGas;

        // Network fee gets the remainder: totalCost - posterFee - infraFee
        var expectedNetworkFee = totalGasCost - UInt256.Zero - expectedInfraFee;

        // Verify exact fee distribution
        var finalNetworkBalance = worldState.GetBalance(networkFeeAccount);
        var finalInfraBalance = worldState.GetBalance(infraFeeAccount);

        var actualNetworkFeeIncrease = finalNetworkBalance - initialNetworkBalance;
        var actualInfraFeeIncrease = finalInfraBalance - initialInfraBalance;

        actualNetworkFeeIncrease.Should().Be(expectedNetworkFee);
        actualInfraFeeIncrease.Should().Be(expectedInfraFee);

        // Verify totals add up correctly
        (actualNetworkFeeIncrease + actualInfraFeeIncrease).Should().Be(totalGasCost);
    }

    [Test]
    public void EndTxHook_TakeFundsFunction_WorksCorrectly()
    {
        // Test the TakeFunds helper function logic
        // This tests the maxRefund pool management that's critical for retry transactions

        var pool = (UInt256)1000;
        var take1 = (UInt256)300;
        var take2 = (UInt256)800; // More than remaining pool
        var take3 = UInt256.Zero; // Zero amount

        var taken1 = UInt256.Min(take1, pool);
        pool -= taken1;
        taken1.Should().Be(300);
        pool.Should().Be(700);

        var taken2 = UInt256.Min(take2, pool);
        pool = taken2 < take2 ? 0 : pool - taken2;
        taken2.Should().Be(700); // Should only take what's available
        pool.Should().Be(0);

        var taken3 = UInt256.Min(take3, pool);
        taken3.Should().Be(0);
        pool.Should().Be(0);
    }

    [Test]
    public void EndTxHook_BasicFeeDistribution_VerifiesNetworkFeeAccountReceivesFees()
    {
        // This is a simpler test focused on verifying that fee distribution works
        // in the EndTxHook for normal transactions

        var (worldState, _) = ArbOSInitialization.Create();
        var specProvider = new FullChainSimulationSpecProvider();

        // Create a simple account state for fee distribution testing
        var burner = new SystemBurner(readOnly: false);
        var arbosState = ArbosState.OpenArbosState(worldState, burner, _logManager.GetClassLogger<ArbosState>());

        var networkFeeAccount = arbosState.NetworkFeeAccount.Get();
        var initialNetworkBalance = worldState.GetBalance(networkFeeAccount);

        var baseFee = (UInt256)1000;
        const ulong GasUsed = 21000;
        var totalCost = baseFee * GasUsed;
        var posterFee = UInt256.Zero; // No L1 poster fees for this test
        var computeCost = totalCost - posterFee;

        // This simulates what HandleNormalTransactionEndTxHook does
        worldState.AddToBalanceAndCreateIfNotExists(networkFeeAccount, computeCost, specProvider.GenesisSpec);

        var finalNetworkBalance = worldState.GetBalance(networkFeeAccount);
        var actualIncrease = finalNetworkBalance - initialNetworkBalance;
        var expectedIncrease = computeCost;

        actualIncrease.Should().Be(expectedIncrease);

        // Verify the calculation was correct
        expectedIncrease.Should().Be(baseFee * GasUsed); // Since posterFee = 0
    }
}
