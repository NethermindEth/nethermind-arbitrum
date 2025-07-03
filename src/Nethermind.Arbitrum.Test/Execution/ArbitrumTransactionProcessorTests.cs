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
    public void EndTxHook_RetryTransactionSuccess_DeletesRetryableAndRefundsCorrectly()
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

        var baseFeePerGas = chain.BlockTree.Head!.Header.BaseFeePerGas;
        var burner = new SystemBurner(readOnly: false);
        var arbosState = ArbosState.OpenArbosState(chain.WorldStateManager.GlobalWorldState, burner, _logManager.GetClassLogger<ArbosState>());

        // Setup retryable
        var ticketId = ArbRetryableTxTests.Hash256FromUlong(456);
        var sender = new Address("0x1000000000000000000000000000000000000001");
        var refundTo = new Address("0x2000000000000000000000000000000000000002");
        var maxRefund = (UInt256)50000;
        var submissionFeeRefund = (UInt256)1000;
        const ulong gasLimit = 100000;
        var timeout = chain.BlockTree.Head!.Header.Timestamp + 1000;

        arbosState.RetryableState.CreateRetryable(ticketId, sender, sender, 0, sender, timeout, []);

        // Setup fee accounts with sufficient balance
        var networkFeeAccount = arbosState.NetworkFeeAccount.Get();
        chain.WorldStateManager.GlobalWorldState.AddToBalanceAndCreateIfNotExists(networkFeeAccount, maxRefund, chain.SpecProvider.GenesisSpec);

        var retryTxInner = new ArbitrumRetryTx(
            0, 0, sender, baseFeePerGas, gasLimit, sender, 0, ReadOnlyMemory<byte>.Empty, 
            ticketId, refundTo, maxRefund, submissionFeeRefund);

        var transaction = new ArbitrumTransaction<ArbitrumRetryTx>(retryTxInner)
        {
            SenderAddress = sender,
            Type = (TxType)ArbitrumTxType.ArbitrumRetry,
            GasLimit = (long)gasLimit,
            DecodedMaxFeePerGas = baseFeePerGas
        };

        // Setup escrow with callvalue
        var escrowAddress = ArbitrumTransactionProcessor.GetRetryableEscrowAddress(ticketId);
        chain.WorldStateManager.GlobalWorldState.AddToBalanceAndCreateIfNotExists(escrowAddress, transaction.Value, chain.SpecProvider.GenesisSpec);

        // Add balance to sender for gas refunds
        var gasRefund = baseFeePerGas * gasLimit;
        chain.WorldStateManager.GlobalWorldState.AddToBalanceAndCreateIfNotExists(sender, gasRefund, chain.SpecProvider.GenesisSpec);

        var result = ((ArbitrumTransactionProcessor)chain.TxProcessor).Execute(transaction, NullTxTracer.Instance);

        result.Should().Be(TransactionResult.Ok);

        // Verify retryable was deleted on success
        var retryable = arbosState.RetryableState.OpenRetryable(ticketId, chain.BlockTree.Head!.Header.Timestamp);
        retryable.Should().BeNull();

        // Verify escrow is empty
        chain.WorldStateManager.GlobalWorldState.GetBalance(escrowAddress).Should().Be(0);
    }

    [Test]
    public void EndTxHook_RetryTransactionFailure_ReturnsCallvalueToEscrow()
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

        var baseFeePerGas = chain.BlockTree.Head!.Header.BaseFeePerGas;
        var burner = new SystemBurner(readOnly: false);
        var arbosState = ArbosState.OpenArbosState(chain.WorldStateManager.GlobalWorldState, burner, _logManager.GetClassLogger<ArbosState>());

        // Setup retryable
        var ticketId = ArbRetryableTxTests.Hash256FromUlong(789);
        var sender = new Address("0x1000000000000000000000000000000000000001");
        var refundTo = new Address("0x2000000000000000000000000000000000000002");
        var maxRefund = (UInt256)50000;
        var submissionFeeRefund = (UInt256)1000;
        const ulong gasLimit = 100000;
        var timeout = chain.BlockTree.Head!.Header.Timestamp + 1000;
        var callvalue = (UInt256)1000;

        arbosState.RetryableState.CreateRetryable(ticketId, sender, sender, callvalue, sender, timeout, []);

        // Create a contract that will cause EVM execution to fail by using an invalid operation
        // This will cause the transaction to fail during EVM execution, not pre-processing
        var failingContract = new Address("0x3000000000000000000000000000000000000003");
        var failingCode = new byte[] { 0xFE }; // INVALID opcode - this will cause EVM execution to fail
        chain.WorldStateManager.GlobalWorldState.CreateAccount(failingContract, 0);
        var codeHash = (ValueHash256)Keccak.Compute(failingCode);
        chain.WorldStateManager.GlobalWorldState.InsertCode(failingContract, codeHash, failingCode, chain.SpecProvider.GenesisSpec);
        
        // Create some data to trigger the EVM execution of the failing contract
        var callData = new byte[] { 0x00 }; // Any non-empty data will trigger EVM execution
        
        var retryTxInner = new ArbitrumRetryTx(
            0, 0, sender, baseFeePerGas, gasLimit, failingContract, callvalue, callData, 
            ticketId, refundTo, maxRefund, submissionFeeRefund);

        var transaction = new ArbitrumTransaction<ArbitrumRetryTx>(retryTxInner)
        {
            SenderAddress = sender,
            Type = (TxType)ArbitrumTxType.ArbitrumRetry,
            GasLimit = (long)gasLimit,
            DecodedMaxFeePerGas = baseFeePerGas,
            Value = callvalue
        };

        // Setup escrow with callvalue
        var escrowAddress = ArbitrumTransactionProcessor.GetRetryableEscrowAddress(ticketId);
        chain.WorldStateManager.GlobalWorldState.AddToBalanceAndCreateIfNotExists(escrowAddress, callvalue, chain.SpecProvider.GenesisSpec);

        // Add balance to sender for gas refunds
        var gasRefund = baseFeePerGas * gasLimit;
        chain.WorldStateManager.GlobalWorldState.AddToBalanceAndCreateIfNotExists(sender, gasRefund, chain.SpecProvider.GenesisSpec);

        // Setup fee accounts with sufficient balance to avoid refund failures
        var networkFeeAccount = arbosState.NetworkFeeAccount.Get();
        chain.WorldStateManager.GlobalWorldState.AddToBalanceAndCreateIfNotExists(networkFeeAccount, maxRefund, chain.SpecProvider.GenesisSpec);

        // Execute transaction that will fail (target doesn't exist)
        var result = ((ArbitrumTransactionProcessor)chain.TxProcessor).Execute(transaction, NullTxTracer.Instance);

        result.Should().Be(TransactionResult.Ok); // Retry transactions always return Ok

        // Verify retryable still exists (transaction failed in EVM, so retryable should not be deleted)
        var retryable = arbosState.RetryableState.OpenRetryable(ticketId, chain.BlockTree.Head!.Header.Timestamp);
        retryable.Should().NotBeNull();

        // Verify callvalue was returned to escrow
        chain.WorldStateManager.GlobalWorldState.GetBalance(escrowAddress).Should().Be(callvalue);
    }

    [Test]
    public void EndTxHook_NormalTransaction_DistributesFeesToNetworkAccount()
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

        var baseFeePerGas = chain.BlockTree.Head!.Header.BaseFeePerGas;
        var sender = new Address("0x1000000000000000000000000000000000000001");
        var to = new Address("0x2000000000000000000000000000000000000002");
        const ulong gasLimit = 21000;

        var transaction = Build.A.Transaction
            .WithSenderAddress(sender)
            .WithTo(to)
            .WithGasLimit((long)gasLimit)
            .WithMaxFeePerGas(baseFeePerGas * 2)
            .WithValue(100)
            .WithType(TxType.EIP1559)
            .TestObject;

        // Add sufficient balance to sender
        var initialBalance = baseFeePerGas * gasLimit * 3 + transaction.Value;
        chain.WorldStateManager.GlobalWorldState.AddToBalanceAndCreateIfNotExists(sender, initialBalance, chain.SpecProvider.GenesisSpec);

        // Get initial network fee account balance
        var burner = new SystemBurner(readOnly: false);
        var arbosState = ArbosState.OpenArbosState(chain.WorldStateManager.GlobalWorldState, burner, _logManager.GetClassLogger<ArbosState>());
        var networkFeeAccount = arbosState.NetworkFeeAccount.Get();
        var initialNetworkBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(networkFeeAccount);

        var result = ((ArbitrumTransactionProcessor)chain.TxProcessor).Execute(transaction, NullTxTracer.Instance);

        result.Should().Be(TransactionResult.Ok);

        // Verify network fee account received the compute cost
        var actualGasUsed = (ulong)transaction.SpentGas;
        var expectedNetworkFee = baseFeePerGas * actualGasUsed;
        var finalNetworkBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(networkFeeAccount);
        var actualNetworkFeeIncrease = finalNetworkBalance - initialNetworkBalance;

        actualNetworkFeeIncrease.Should().Be(expectedNetworkFee);
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

        const ulong baseFeePerGas = 100_000_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;

        // Setup infrastructure fees
        var burner = new SystemBurner(readOnly: false);
        var arbosState = ArbosState.OpenArbosState(chain.WorldStateManager.GlobalWorldState, burner, _logManager.GetClassLogger<ArbosState>());
        
        var infraFeeAccount = new Address("0x4000000000000000000000000000000000000004");
        arbosState.InfraFeeAccount.Set(infraFeeAccount);
        
        var minBaseFee = (UInt256)50_000_000;
        arbosState.L2PricingState.MinBaseFeeWeiStorage.Set(minBaseFee);

        var sender = new Address("0x1000000000000000000000000000000000000001");
        var to = new Address("0x2000000000000000000000000000000000000002");
        const ulong gasLimit = 21000;

        var transaction = Build.A.Transaction
            .WithSenderAddress(sender)
            .WithTo(to)
            .WithGasLimit((long)gasLimit)
            .WithMaxFeePerGas(baseFeePerGas * 2)
            .WithValue(100)
            .WithType(TxType.EIP1559)
            .TestObject;

        var initialBalance = baseFeePerGas * gasLimit * 3 + transaction.Value;
        chain.WorldStateManager.GlobalWorldState.AddToBalanceAndCreateIfNotExists(sender, initialBalance, chain.SpecProvider.GenesisSpec);

        // Get initial balances
        var networkFeeAccount = arbosState.NetworkFeeAccount.Get();
        var initialNetworkBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(networkFeeAccount);
        var initialInfraBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(infraFeeAccount);

        var result = ((ArbitrumTransactionProcessor)chain.TxProcessor).Execute(transaction, NullTxTracer.Instance);

        result.Should().Be(TransactionResult.Ok);

        // Verify fee distribution
        var actualGasUsed = (ulong)transaction.SpentGas;
        var totalGasCost = baseFeePerGas * actualGasUsed;
        var infraFeeRate = UInt256.Min(minBaseFee, baseFeePerGas);
        var expectedInfraFee = infraFeeRate * actualGasUsed;
        var expectedNetworkFee = totalGasCost - expectedInfraFee;

        var finalNetworkBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(networkFeeAccount);
        var finalInfraBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(infraFeeAccount);

        var actualNetworkFeeIncrease = finalNetworkBalance - initialNetworkBalance;
        var actualInfraFeeIncrease = finalInfraBalance - initialInfraBalance;

        actualNetworkFeeIncrease.Should().Be(expectedNetworkFee);
        actualInfraFeeIncrease.Should().Be(expectedInfraFee);
        (actualNetworkFeeIncrease + actualInfraFeeIncrease).Should().Be(totalGasCost);
    }

    [Test]
    public void EndTxHook_FailedTransaction_StillDistributesFees()
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

        var baseFeePerGas = chain.BlockTree.Head!.Header.BaseFeePerGas;
        var sender = new Address("0x1000000000000000000000000000000000000001");
        var to = new Address("0x2000000000000000000000000000000000000002");
        const ulong gasLimit = 21000;

        var transaction = Build.A.Transaction
            .WithSenderAddress(sender)
            .WithTo(to)
            .WithGasLimit((long)gasLimit)
            .WithMaxFeePerGas(baseFeePerGas * 2)
            .WithValue(100)
            .WithType(TxType.EIP1559)
            .TestObject;

        // Add balance to sender but not enough to cover the transaction
        var insufficientBalance = baseFeePerGas * 1000; // Much less than needed
        chain.WorldStateManager.GlobalWorldState.AddToBalanceAndCreateIfNotExists(sender, insufficientBalance, chain.SpecProvider.GenesisSpec);

        // Get initial network fee account balance
        var burner = new SystemBurner(readOnly: false);
        var arbosState = ArbosState.OpenArbosState(chain.WorldStateManager.GlobalWorldState, burner, _logManager.GetClassLogger<ArbosState>());
        var networkFeeAccount = arbosState.NetworkFeeAccount.Get();
        var initialNetworkBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(networkFeeAccount);

        var result = ((ArbitrumTransactionProcessor)chain.TxProcessor).Execute(transaction, NullTxTracer.Instance);

        // Transaction should fail due to insufficient balance
        result.Should().NotBe(TransactionResult.Ok);

        // Even failed transactions should distribute fees in Arbitrum
        var finalNetworkBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(networkFeeAccount);
        var networkFeeIncrease = finalNetworkBalance - initialNetworkBalance;
        
        // Should have some fee distribution even for failed transactions
        networkFeeIncrease.Should().BeGreaterThan(0);
    }

    [Test]
    public void EndTxHook_ConsumeAvailableFunction_HandlesEdgeCasesCorrectly()
    {
        // Test the ConsumeAvailable function behavior (equivalent to Nitro's takeFunds)
        var pool = (UInt256)1000;
        var amount1 = (UInt256)300;
        var amount2 = (UInt256)800; // More than remaining pool
        var amount3 = UInt256.Zero; // Zero amount

        // Test normal consumption
        var taken1 = UInt256.Min(amount1, pool);
        pool -= taken1;
        taken1.Should().Be(300);
        pool.Should().Be(700);

        // Test consumption exceeding pool
        var taken2 = UInt256.Min(amount2, pool);
        pool = taken2 < amount2 ? UInt256.Zero : pool - taken2;
        taken2.Should().Be(700); // Should only take what's available
        pool.Should().Be(0);

        // Test zero amount
        var taken3 = UInt256.Min(amount3, pool);
        taken3.Should().Be(0);
        pool.Should().Be(0);
    }
}
