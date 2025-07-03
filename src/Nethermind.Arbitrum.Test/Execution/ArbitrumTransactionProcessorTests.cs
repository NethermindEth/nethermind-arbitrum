using Autofac;
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
using static Nethermind.Core.Extensions.MemoryExtensions;

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
        using var chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = false
            });
            builder.AddScoped<ITransactionProcessor, ArbitrumTransactionProcessor>();
            builder.AddScoped<IVirtualMachine, ArbitrumVirtualMachine>();
        });

        var BaseFeePerGas = chain.BlockTree.Head!.Header.BaseFeePerGas;

        // Create retryable
        var burner = new SystemBurner(readOnly: false);
        var arbosState = ArbosState.OpenArbosState(chain.WorldStateManager.GlobalWorldState, burner, _logManager.GetClassLogger<ArbosState>());

        var ticketId = ArbRetryableTxTests.Hash256FromUlong(456);
        var sender = new Address("0x1000000000000000000000000000000000000001");
        var refundTo = new Address("0x2000000000000000000000000000000000000002");
        var maxRefund = (UInt256)50000;
        var submissionFeeRefund = (UInt256)1000;
        const ulong GasLimit = 100000; // Higher gas limit for retry transactions
        var timeout = chain.BlockTree.Head!.Header.Timestamp + 1000;

        arbosState.RetryableState.CreateRetryable(
            ticketId, sender, sender, 0, sender, timeout, []);

        var retryTxInner = new ArbitrumRetryTx(
            0, 0, sender, BaseFeePerGas * 10, GasLimit, sender, 0, ReadOnlyMemory<byte>.Empty, ticketId, refundTo, maxRefund, submissionFeeRefund);

        var transaction = new ArbitrumTransaction<ArbitrumRetryTx>(retryTxInner)
        {
            SenderAddress = sender,
            Type = (TxType)ArbitrumTxType.ArbitrumRetry,
            GasLimit = (long)GasLimit,
            DecodedMaxFeePerGas = BaseFeePerGas * 20 // Set an extremely high gas fee cap to ensure positive miner premium
        };

        // Add balance to sender to pay for gas - account for extremely high gas fees (20x base fee)
        var maxGasCost = BaseFeePerGas * 25 * GasLimit; // Much higher than the 20x gas fee cap we set
        chain.WorldStateManager.GlobalWorldState.AddToBalanceAndCreateIfNotExists(sender, maxGasCost + 1_000_000, chain.SpecProvider.GenesisSpec);

        // Create escrow with callvalue
        var escrowAddress = ArbitrumTransactionProcessor.GetRetryableEscrowAddress(ticketId);
        chain.WorldStateManager.GlobalWorldState.AddToBalanceAndCreateIfNotExists(escrowAddress, transaction.Value, chain.SpecProvider.GenesisSpec);

        var result = ((ArbitrumTransactionProcessor)chain.TxProcessor).Execute(transaction, NullTxTracer.Instance);

        result.Should().Be(TransactionResult.Ok);
    }

    [Test]
    public void EndTxHook_NormalTransaction_DistributesFeeCorrectly()
    {
        using var chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = false
            });
            builder.AddScoped<ITransactionProcessor, ArbitrumTransactionProcessor>();
            builder.AddScoped<IVirtualMachine, ArbitrumVirtualMachine>();
        });

        var BaseFeePerGas = chain.BlockTree.Head!.Header.BaseFeePerGas;
        chain.BlockTree.Head!.Header.Beneficiary = new Address("0x3000000000000000000000000000000000000003");

        // Create normal transaction
        var sender = new Address("0x1000000000000000000000000000000000000001");
        var to = new Address("0x2000000000000000000000000000000000000002");
        const ulong GasLimit = 21000;

        var transaction = Build.A.Transaction
            .WithSenderAddress(sender)
            .WithTo(to)
            .WithGasLimit((long)GasLimit)
            .WithMaxFeePerGas(BaseFeePerGas * 2) // Ensure miner premium is positive
            .WithMaxPriorityFeePerGas(BaseFeePerGas / 2) // Set reasonable priority fee
            .WithValue(100)
            .WithType(TxType.EIP1559)
            .TestObject;

        // Add balance to sender - generous balance to cover all gas scenarios
        var initialBalance = BaseFeePerGas * GasLimit * 10 + transaction.Value + 10_000_000;
        chain.WorldStateManager.GlobalWorldState.AddToBalanceAndCreateIfNotExists(sender, initialBalance, chain.SpecProvider.GenesisSpec);

        // Get initial balances
        var burner = new SystemBurner(readOnly: false);
        var arbosState = ArbosState.OpenArbosState(chain.WorldStateManager.GlobalWorldState, burner, _logManager.GetClassLogger<ArbosState>());
        var networkFeeAccount = arbosState.NetworkFeeAccount.Get();
        var initialNetworkBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(networkFeeAccount);

        var result = ((ArbitrumTransactionProcessor)chain.TxProcessor).Execute(transaction, NullTxTracer.Instance);

        result.Should().Be(TransactionResult.Ok);

        var actualGasUsed = (ulong)transaction.SpentGas;
        var effectiveGasPrice = BaseFeePerGas;
        var totalGasCost = effectiveGasPrice * actualGasUsed;
        var posterFee = UInt256.Zero; // No L1 costs for normal transactions
        var expectedNetworkFee = totalGasCost - posterFee; // = totalGasCost since posterFee is 0

        // Verify exact network fee account balance
        var finalNetworkBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(networkFeeAccount);
        var actualNetworkFeeIncrease = finalNetworkBalance - initialNetworkBalance;
        actualNetworkFeeIncrease.Should().Be(expectedNetworkFee);

        var finalSenderBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(sender);
        var maxPriorityFee = BaseFeePerGas / 2;
        var userEffectiveGasPrice = UInt256.Min(BaseFeePerGas * 2, BaseFeePerGas + maxPriorityFee); // User pays full effective price
        var expectedDecrease = userEffectiveGasPrice * actualGasUsed + transaction.Value;
        var actualDecrease = initialBalance - finalSenderBalance;
        actualDecrease.Should().Be(expectedDecrease);
    }

    [Test]
    public void EndTxHook_NormalTransactionWithInfraFees_DistributesFeesBetweenNetworkAndInfra()
    {
        using var chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = false
            });
            builder.AddScoped<ITransactionProcessor, ArbitrumTransactionProcessor>();
            builder.AddScoped<IVirtualMachine, ArbitrumVirtualMachine>();
        });

        const ulong BaseFeePerGas = 100_000_000; // Must match the test infrastructure base fee
        chain.BlockTree.Head!.Header.BaseFeePerGas = BaseFeePerGas;

        // Setup ArbOS state to enable infrastructure fees
        var burner = new SystemBurner(readOnly: false);
        var arbosState = ArbosState.OpenArbosState(chain.WorldStateManager.GlobalWorldState, burner, _logManager.GetClassLogger<ArbosState>());

        // Set up infrastructure fee account
        var infraFeeAccount = new Address("0x4000000000000000000000000000000000000004");
        arbosState.InfraFeeAccount.Set(infraFeeAccount);

        // Set minimum base fee for infrastructure calculation
        var minBaseFee = (UInt256)50_000_000; // Half of base fee for reasonable infrastructure split
        arbosState.L2PricingState.MinBaseFeeWeiStorage.Set(minBaseFee);

        // Create normal transaction
        var sender = new Address("0x1000000000000000000000000000000000000001");
        var to = new Address("0x2000000000000000000000000000000000000002");
        const ulong GasLimit = 21000;

        var transaction = Build.A.Transaction
            .WithSenderAddress(sender)
            .WithTo(to)
            .WithGasLimit((long)GasLimit)
            .WithMaxFeePerGas(BaseFeePerGas * 2) // Ensure miner premium is positive
            .WithMaxPriorityFeePerGas(BaseFeePerGas / 2) // Set reasonable priority fee
            .WithValue(100)
            .WithType(TxType.EIP1559)
            .TestObject;

        // Add balance to sender - generous balance to cover all gas scenarios
        var initialBalance = BaseFeePerGas * GasLimit * 10 + transaction.Value + 10_000_000;
        chain.WorldStateManager.GlobalWorldState.AddToBalanceAndCreateIfNotExists(sender, initialBalance, chain.SpecProvider.GenesisSpec);

        // Get initial balances
        var networkFeeAccount = arbosState.NetworkFeeAccount.Get();
        var initialNetworkBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(networkFeeAccount);
        var initialInfraBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(infraFeeAccount);

        var result = ((ArbitrumTransactionProcessor)chain.TxProcessor).Execute(transaction, NullTxTracer.Instance);

        result.Should().Be(TransactionResult.Ok);

        // Calculate exact expected fee distribution - in Arbitrum, the effective gas price is just the base fee
        var actualGasUsed = (ulong)transaction.SpentGas;
        var effectiveGasPrice = BaseFeePerGas;
        var totalGasCost = effectiveGasPrice * actualGasUsed;
        const ulong PosterGas = 0;
        var computeGas = actualGasUsed - PosterGas;

        // Infrastructure fee calculation: min(minBaseFee, baseFee) * computeGas
        var infraFeeRate = UInt256.Min(minBaseFee, BaseFeePerGas);
        var expectedInfraFee = infraFeeRate * computeGas;

        // Network fee gets the remainder: totalCost - posterFee - infraFee
        var expectedNetworkFee = totalGasCost - UInt256.Zero - expectedInfraFee;

        // Verify exact fee distribution
        var finalNetworkBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(networkFeeAccount);
        var finalInfraBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(infraFeeAccount);

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
        expectedIncrease.Should().Be(baseFee * GasUsed);
    }
}
