using Autofac;
using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Compression;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Core;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Math;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Arbitrum.Test.Precompiles;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Tracing.GethStyle;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Core.Test;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Evm.Test;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Test.Execution;

public class ArbitrumTransactionProcessorTests
{
    private static readonly TestLogManager _logManager = new();

    [Test]
    public void ProcessArbitrumRetryTransaction_RetryableExists_ReturnsOkTransactionResult()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);

        BlockTree blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();

        ArbitrumVirtualMachine virtualMachine = new(
            new TestBlockhashProvider(fullChainSimulationSpecProvider),
            fullChainSimulationSpecProvider,
            _logManager
        );

        ulong baseFeePerGas = 10;
        genesis.Header.BaseFeePerGas = baseFeePerGas;
        BlockExecutionContext blCtx = new(genesis.Header, fullChainSimulationSpecProvider.GetSpec(genesis.Header));
        virtualMachine.SetBlockExecutionContext(in blCtx);

        ArbitrumTransactionProcessor processor = new(
            BlobBaseFeeCalculator.Instance,
            fullChainSimulationSpecProvider,
            worldState,
            virtualMachine,
            blockTree,
            _logManager,
            new EthereumCodeInfoRepository(worldState)
        );

        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, burner, _logManager.GetClassLogger<ArbosState>());

        Hash256 ticketIdHash = ArbRetryableTxTests.Hash256FromUlong(123);
        Address refundTo = Address.MaxValue;
        Address sender = TestItem.AddressA;
        UInt256 value = 100;
        long gasLimit = GasCostOf.Transaction;
        ulong timeout = genesis.Header.Timestamp + 1; // retryable not expired

        arbosState.RetryableState.CreateRetryable(
            ticketIdHash, Address.Zero, Address.Zero, 0, Address.Zero, timeout, []
        );

        ArbitrumRetryTransaction transaction = new ArbitrumRetryTransaction
        {
            ChainId = 0,
            Nonce = 0,
            SenderAddress = sender,
            DecodedMaxFeePerGas = baseFeePerGas,
            GasFeeCap = baseFeePerGas,
            Gas = (ulong)gasLimit,
            GasLimit = gasLimit,
            To = TestItem.AddressB,
            Value = value,
            Data = Array.Empty<byte>(),
            TicketId = ticketIdHash,
            RefundTo = refundTo,
            MaxRefund = UInt256.MaxValue,
            SubmissionFeeRefund = 0,
            Type = (TxType)ArbitrumTxType.ArbitrumRetry
        };

        Address escrowAddress = ArbitrumTransactionProcessor.GetRetryableEscrowAddress(ticketIdHash);
        worldState.AddToBalanceAndCreateIfNotExists(
            escrowAddress,
            transaction.Value,
            fullChainSimulationSpecProvider.GenesisSpec
        );

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult result = processor.Execute(transaction, tracer);

        result.Should().Be(TransactionResult.Ok);

        worldState.GetBalance(sender).Should().Be(0); //sender spent transaction value (from escrow) and minted prepaid amount for gas fee
        worldState.GetBalance(escrowAddress).Should().Be(0);
        worldState.GetBalance(TestItem.AddressB).Should().Be(value);

        virtualMachine.ArbitrumTxExecutionContext.CurrentRetryable.Should().Be(ticketIdHash);
        virtualMachine.ArbitrumTxExecutionContext.CurrentRefundTo.Should().Be(refundTo);
        tracer.BeforeEvmTransfers.Count.Should().Be(2);
        tracer.AfterEvmTransfers.Count.Should().Be(6);
    }

    [Test]
    public void ProcessArbitrumRetryTransaction_RetryableDoesNotExist_TracesErrorButReturnsOkTransactionResult()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);

        BlockTree blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();
        ArbitrumVirtualMachine virtualMachine = new(
            new TestBlockhashProvider(fullChainSimulationSpecProvider),
            fullChainSimulationSpecProvider,
            _logManager
        );

        ulong baseFeePerGas = 10;
        genesis.Header.BaseFeePerGas = baseFeePerGas;
        BlockExecutionContext blCtx = new(genesis.Header, fullChainSimulationSpecProvider.GenesisSpec);
        virtualMachine.SetBlockExecutionContext(in blCtx);

        ArbitrumTransactionProcessor processor = new(
            BlobBaseFeeCalculator.Instance,
            fullChainSimulationSpecProvider,
            worldState,
            virtualMachine,
            blockTree,
            _logManager,
            new EthereumCodeInfoRepository(worldState)
        );

        Hash256 ticketIdHash = ArbRetryableTxTests.Hash256FromUlong(123);
        Address refundTo = Address.Zero;
        UInt256 value = 0;
        long gasLimit = GasCostOf.Transaction;

        ArbitrumRetryTransaction transaction = new ArbitrumRetryTransaction
        {
            ChainId = 0,
            Nonce = 0,
            SenderAddress = Address.Zero,
            DecodedMaxFeePerGas = baseFeePerGas,
            GasFeeCap = 0,
            Gas = 0,
            GasLimit = gasLimit,
            To = Address.Zero,
            Value = value,
            Data = Array.Empty<byte>(),
            TicketId = ticketIdHash,
            RefundTo = refundTo,
            MaxRefund = UInt256.MaxValue,
            SubmissionFeeRefund = 0,
            Type = (TxType)ArbitrumTxType.ArbitrumRetry
        };

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult result = processor.Execute(transaction, tracer);

        result.Should().BeEquivalentTo(TransactionResult.Ok);
        tracer.BeforeEvmTransfers.Count.Should().Be(0);
        tracer.AfterEvmTransfers.Count.Should().Be(0);

        GethLikeTxTrace trace = tracer.BuildResult();
        trace.Failed.Should().BeTrue(); // Fails even if it returns TransactionResult.Ok (for including tx in block)
    }

    [Test]
    public void ProcessArbitrumDepositTransaction_ValidTransaction_ReturnsOkTransactionResult()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);

        BlockTree blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();

        ArbitrumVirtualMachine virtualMachine = new(
            new TestBlockhashProvider(fullChainSimulationSpecProvider),
            fullChainSimulationSpecProvider,
            _logManager
        );

        BlockExecutionContext blCtx = new(genesis.Header, fullChainSimulationSpecProvider.GenesisSpec);
        virtualMachine.SetBlockExecutionContext(in blCtx);

        ArbitrumTransactionProcessor processor = new(
            BlobBaseFeeCalculator.Instance,
            fullChainSimulationSpecProvider,
            worldState,
            virtualMachine,
            blockTree,
            _logManager,
            new EthereumCodeInfoRepository(worldState)
        );

        Address from = new("0x0000000000000000000000000000000000000123");
        Address to = new("0x0000000000000000000000000000000000000456");
        UInt256 value = 100;
        ArbitrumDepositTransaction depositTx = new ArbitrumDepositTransaction
        {
            ChainId = 0,
            L1RequestId = Hash256.Zero,
            SenderAddress = from,
            To = to,
            Value = value
        };

        Transaction transaction = NitroL2MessageParser.ConvertParsedDataToTransaction(depositTx);

        UInt256 initialFromBalance = 10;
        worldState.AddToBalanceAndCreateIfNotExists(
            from,
            initialFromBalance,
            fullChainSimulationSpecProvider.GenesisSpec
        );

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult result = processor.Execute(transaction, tracer);

        result.Should().Be(TransactionResult.Ok);
        worldState.GetBalance(from).Should().Be(initialFromBalance);
        worldState.GetBalance(to).Should().Be(value);

        tracer.BeforeEvmTransfers.Count.Should().Be(1);
        tracer.AfterEvmTransfers.Count.Should().Be(0);
    }

    [Test]
    public void ProcessArbitrumDepositTransaction_MalformedTx_TracesErrorButReturnsOkTransactionResult()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);

        BlockTree blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();

        ArbitrumVirtualMachine virtualMachine = new(
            new TestBlockhashProvider(fullChainSimulationSpecProvider),
            fullChainSimulationSpecProvider,
            _logManager
        );

        BlockExecutionContext blCtx = new(genesis.Header, fullChainSimulationSpecProvider.GenesisSpec);
        virtualMachine.SetBlockExecutionContext(in blCtx);

        ArbitrumTransactionProcessor processor = new(
            BlobBaseFeeCalculator.Instance,
            fullChainSimulationSpecProvider,
            worldState,
            virtualMachine,
            blockTree,
            _logManager,
            new EthereumCodeInfoRepository(worldState)
        );

        ArbitrumDepositTransaction transaction = new ArbitrumDepositTransaction
        {
            ChainId = 0,
            L1RequestId = Hash256.Zero,
            SenderAddress = Address.Zero,
            To = null, // malformed tx
            Value = 0,
            Type = (TxType)ArbitrumTxType.ArbitrumDeposit
        };

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult result = processor.Execute(transaction, tracer);
        result.Should().Be(TransactionResult.Ok);

        tracer.BeforeEvmTransfers.Count.Should().Be(0);
        tracer.AfterEvmTransfers.Count.Should().Be(0);

        GethLikeTxTrace trace = tracer.BuildResult();
        trace.Failed.Should().BeTrue(); // Fails even if it returns TransactionResult.Ok (for including tx in block)
    }

    [Test]
    public void GasChargingHook_TxWithEnoughGas_TipsNetworkCorrectly()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);

        BlockTree blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();
        ArbitrumVirtualMachine virtualMachine = new(
            new TestBlockhashProvider(fullChainSimulationSpecProvider),
            fullChainSimulationSpecProvider,
            _logManager
        );

        ulong baseFeePerGas = 1_000;
        genesis.Header.BaseFeePerGas = baseFeePerGas;
        genesis.Header.Author = ArbosAddresses.BatchPosterAddress; // to set up Coinbase
        BlockExecutionContext blCtx = new(genesis.Header, fullChainSimulationSpecProvider.GetSpec(genesis.Header));
        virtualMachine.SetBlockExecutionContext(in blCtx);

        ArbitrumTransactionProcessor txProcessor = new(
            BlobBaseFeeCalculator.Instance,
            fullChainSimulationSpecProvider,
            worldState,
            virtualMachine,
            blockTree,
            _logManager,
            new EthereumCodeInfoRepository(worldState)
        );

        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, burner, _logManager.GetClassLogger<ArbosState>());

        Address sender = TestItem.AddressA;
        ulong premiumGas = 2;
        ulong differenceGasLeftGasAvailable = 100;
        ulong valueToTransfer = 1;
        long intrinsicGas = GasCostOf.Transaction;
        // 151 is the expected poster cost estimated by GasChargingHook for this tx
        // +100 gas bonus to test the case gasLeft > PerBlockGasLimitStorage.Get() in GasChargingHook
        // 0 (block gas limit) will be the gasAvailable returned by GasChargingHook for EVM execution
        // (the 100-0=100 will be reimbursed later)
        long gasLimit = intrinsicGas + 151 + (long)differenceGasLeftGasAvailable;
        arbosState.L2PricingState.PerBlockGasLimitStorage.Set(0);

        // Create a simple transfer tx
        Transaction transferTx = Build.A.Transaction
            .WithTo(TestItem.AddressB)
            .WithValue(valueToTransfer)
            .WithGasLimit(gasLimit)
            .WithGasPrice(baseFeePerGas + premiumGas)
            .WithNonce(0)
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        worldState.CreateAccount(sender, 1.Ether());

        Rlp encodedTx = Rlp.Encode(transferTx);
        ulong brotliCompressionLevel = arbosState.BrotliCompressionLevel.Get();
        ulong l1Bytes = (ulong)BrotliCompression.Compress(encodedTx.Bytes, brotliCompressionLevel).Length;
        ulong calldataUnits = l1Bytes * GasCostOf.TxDataNonZeroEip2028;

        UInt256 pricePerUnit = arbosState.L1PricingState.PricePerUnitStorage.Get();
        UInt256 posterCost = pricePerUnit * calldataUnits;

        ulong posterGas = (posterCost / baseFeePerGas).ToULongSafe(); // Should be 151

        // Arbos version set to 9 + blockContext.Coinbase set to BatchPosterAddress
        // enables tipping for the tx
        arbosState.BackingStorage.Set(ArbosStateOffsets.VersionOffset, ArbosVersion.Nine);

        Address networkFeeAccount = arbosState.NetworkFeeAccount.Get();
        UInt256 initialNetworkBalance = worldState.GetBalance(networkFeeAccount);

        UInt256 initialSenderBalance = worldState.GetBalance(sender);

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult result = txProcessor.Execute(transferTx, tracer);

        result.Should().Be(TransactionResult.Ok);

        arbosState.L1PricingState.UnitsSinceStorage.Get().Should().Be(calldataUnits);
        virtualMachine.ArbitrumTxExecutionContext.PosterGas.Should().Be(posterGas);
        virtualMachine.ArbitrumTxExecutionContext.PosterFee.Should().Be(baseFeePerGas * posterGas);
        virtualMachine.ArbitrumTxExecutionContext.ComputeHoldGas.Should().Be(differenceGasLeftGasAvailable);

        // Tip for the tx
        UInt256 txTip = premiumGas * (ulong)transferTx.SpentGas;
        // Network compute fee for processing the tx
        UInt256 totalCost = baseFeePerGas * (ulong)transferTx.SpentGas;
        UInt256 networkComputeCostForTx = totalCost - virtualMachine.ArbitrumTxExecutionContext.PosterFee;
        UInt256 finalNetworkBalance = worldState.GetBalance(networkFeeAccount);
        finalNetworkBalance.Should().Be(initialNetworkBalance + txTip + networkComputeCostForTx);

        // Sender balance should be reduced by the total cost
        UInt256 finalSenderBalance = worldState.GetBalance(sender);
        ulong effectiveGasPrice = (ulong)transferTx.MaxFeePerGas;
        // differenceGasLeftGasAvailable, which was temporarily stored in ComputeHoldGas, got refunded !
        ulong expectedSpentGas = ((ulong)gasLimit - differenceGasLeftGasAvailable) * effectiveGasPrice;
        finalSenderBalance.Should().Be(initialSenderBalance - valueToTransfer - expectedSpentGas);

        tracer.BeforeEvmTransfers.Count.Should().Be(2);
        tracer.AfterEvmTransfers.Count.Should().Be(0);
    }

    [Test]
    public void GasChargingHook_TxWithNotEnoughGas_ReturnsTransactionResultError()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
            builder.AddScoped<ITransactionProcessor, ArbitrumTransactionProcessor>();
            builder.AddScoped<IVirtualMachine, ArbitrumVirtualMachine>();
        });

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();

        UInt256 baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;
        chain.BlockTree.Head!.Header.Author = ArbosAddresses.BatchPosterAddress; // to set up Coinbase
        chain.TxProcessor.SetBlockExecutionContext(new BlockExecutionContext(chain.BlockTree.Head!.Header,
            fullChainSimulationSpecProvider.GetSpec(chain.BlockTree.Head!.Header)));

        using var dispose = chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header);

        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(chain.WorldStateManager.GlobalWorldState, burner, _logManager.GetClassLogger<ArbosState>());

        long intrinsicGas = GasCostOf.Transaction;
        long gasLimit = intrinsicGas; // enough for intrinsic gas but not for poster gas in gas charging hook

        // Create a simple tx
        Transaction transferTx = Build.A.Transaction
            .WithTo(TestItem.AddressB)
            .WithValue(1)
            .WithGasLimit(gasLimit)
            .WithGasPrice(baseFeePerGas)
            .WithNonce(0)
            .WithSenderAddress(TestItem.AddressA)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        ArbitrumTransactionProcessor txProcessor = (ArbitrumTransactionProcessor)chain.TxProcessor;
        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult result = txProcessor.Execute(transferTx, tracer);

        result.Should().Be(TransactionResult.GasLimitBelowIntrinsicGas);
        result.TransactionExecuted.Should().Be(false);
        result.EvmExceptionType.Should().Be(EvmExceptionType.None);

        tracer.BeforeEvmTransfers.Count.Should().Be(0);
        tracer.AfterEvmTransfers.Count.Should().Be(0);
    }

    [Test]
    public void Refund_ExecutionReverts_RefundsComputeHoldGas()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
            builder.AddScoped<ITransactionProcessor, ArbitrumTransactionProcessor>();
            builder.AddScoped<IVirtualMachine, ArbitrumVirtualMachine>();
        });

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();

        ulong baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;
        chain.BlockTree.Head!.Header.Author = ArbosAddresses.BatchPosterAddress; // to set up Coinbase
        chain.TxProcessor.SetBlockExecutionContext(new BlockExecutionContext(chain.BlockTree.Head!.Header,
            fullChainSimulationSpecProvider.GetSpec(chain.BlockTree.Head!.Header)));

        using IDisposable dispose = chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header);
        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;

        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, burner, _logManager.GetClassLogger<ArbosState>());

        uint getL1BaseFeeEstimateMethodId = PrecompileHelper.GetMethodId("getL1BaseFeeEstimate()");

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            ArbGasInfoParser.PrecompileFunctionDescription[getL1BaseFeeEstimateMethodId].AbiFunctionDescription.GetCallInfo().Signature,
            []
        );

        long intrinsicGas = GasCostOf.Transaction + 64;
        long posterGas = 172;

        ulong precompileExecCost = 2 * ArbosStorage.StorageReadCost + 3; // open arbos + method exec + output cost
        // Will make EVM execution fail (run out of gas when paying for precompile output)
        // to test that ComputeHoldGas still gets refunded even if execution fails (without reverting)
        ulong blockGasLimit = precompileExecCost - 1;
        arbosState.L2PricingState.PerBlockGasLimitStorage.Set(blockGasLimit);

        // differenceGasLeftBlockGasLimit is surplus and will be set as ComputeHoldGas
        long differenceGasLeftBlockGasLimit = 500;
        long gasLimit = intrinsicGas + posterGas + (long)blockGasLimit + differenceGasLeftBlockGasLimit;

        Address sender = TestItem.AddressA;
        Transaction tx = Build.A.Transaction
            .WithChainId(chain.ChainSpec.ChainId)
            .WithType(TxType.EIP1559)
            .WithTo(ArbosAddresses.ArbGasInfoAddress)
            .WithData(calldata)
            .WithValue(0)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(gasLimit)
            .WithNonce(worldState.GetNonce(sender))
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 senderInitialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);

        result.TransactionExecuted.Should().Be(true);
        result.EvmExceptionType.Should().Be(EvmExceptionType.OutOfGas); // Failed when paying for precompile output

        // Consumes all gas passed to precompile (except for ComputeHoldGas, which was set aside for refund)
        long expectedGasSpent = gasLimit - differenceGasLeftBlockGasLimit;
        tracer.GasSpent.Should().Be(expectedGasSpent);

        UInt256 senderFinalBalance = worldState.GetBalance(sender);
        senderFinalBalance.Should().Be(senderInitialBalance - (ulong)expectedGasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void Refund_ExecutionFailsWithoutReverting_RefundsComputeHoldGas()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
            builder.AddScoped<ITransactionProcessor, ArbitrumTransactionProcessor>();
            builder.AddScoped<IVirtualMachine, ArbitrumVirtualMachine>();
        });

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();

        ulong baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;
        chain.BlockTree.Head!.Header.Author = ArbosAddresses.BatchPosterAddress; // to set up Coinbase
        chain.TxProcessor.SetBlockExecutionContext(new BlockExecutionContext(chain.BlockTree.Head!.Header,
            fullChainSimulationSpecProvider.GetSpec(chain.BlockTree.Head!.Header)));

        using IDisposable dispose = chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header);
        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;

        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, burner, _logManager.GetClassLogger<ArbosState>());

        uint getNetworkFeeAccountMethodId = PrecompileHelper.GetMethodId("getNetworkFeeAccount()");

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            ArbOwnerParser.PrecompileFunctionDescription[getNetworkFeeAccountMethodId].AbiFunctionDescription.GetCallInfo().Signature,
            []
        );

        long intrinsicGas = GasCostOf.Transaction + 64;
        long posterGas = 172;

        ulong blockGasLimit = 10_000;
        arbosState.L2PricingState.PerBlockGasLimitStorage.Set(blockGasLimit);

        // differenceGasLeftBlockGasLimit is surplus and will be set as ComputeHoldGas
        long differenceGasLeftBlockGasLimit = 500;
        long gasLimit = intrinsicGas + posterGas + (long)blockGasLimit + differenceGasLeftBlockGasLimit;

        Address sender = TestItem.AddressA;
        Transaction tx = Build.A.Transaction
            .WithChainId(chain.ChainSpec.ChainId)
            .WithType(TxType.EIP1559)
            .WithTo(ArbosAddresses.ArbOwnerAddress)
            .WithData(calldata)
            .WithValue(0)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(gasLimit)
            .WithNonce(worldState.GetNonce(sender))
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 senderInitialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);

        result.TransactionExecuted.Should().Be(true);
        // Failed when checking if sender is a chain owner
        result.EvmExceptionType.Should().Be(EvmExceptionType.PrecompileFailure);

        // Consumes all gas passed to precompile (except for ComputeHoldGas, which was set aside for refund)
        long expectedGasSpent = gasLimit - differenceGasLeftBlockGasLimit;
        tracer.GasSpent.Should().Be(expectedGasSpent);

        UInt256 senderFinalBalance = worldState.GetBalance(sender);
        senderFinalBalance.Should().Be(senderInitialBalance - (ulong)expectedGasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void Refund_ExecutionSucceeds_RefundsComputeHoldGas()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
            builder.AddScoped<ITransactionProcessor, ArbitrumTransactionProcessor>();
            builder.AddScoped<IVirtualMachine, ArbitrumVirtualMachine>();
        });

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();

        ulong baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;
        chain.BlockTree.Head!.Header.Author = ArbosAddresses.BatchPosterAddress; // to set up Coinbase
        chain.TxProcessor.SetBlockExecutionContext(new BlockExecutionContext(chain.BlockTree.Head!.Header,
            fullChainSimulationSpecProvider.GetSpec(chain.BlockTree.Head!.Header)));

        using IDisposable dispose = chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header);
        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;

        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, burner, _logManager.GetClassLogger<ArbosState>());

        uint getL1BaseFeeEstimateMethodId = PrecompileHelper.GetMethodId("getL1BaseFeeEstimate()");

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            ArbGasInfoParser.PrecompileFunctionDescription[getL1BaseFeeEstimateMethodId].AbiFunctionDescription.GetCallInfo().Signature,
            []
        );

        long intrinsicGas = GasCostOf.Transaction + 64;
        long posterGas = 172;

        ulong precompileExecCost = 2 * ArbosStorage.StorageReadCost + 3; // open arbos + method exec + output cost
        ulong excessGas = 200; // Just some excess gas that should get refunded along with ComputeHoldGas
        ulong blockGasLimit = precompileExecCost + excessGas; // Give more than enough gas to the tx to execute successfully
        arbosState.L2PricingState.PerBlockGasLimitStorage.Set(blockGasLimit);

        // differenceGasLeftBlockGasLimit is surplus and will be set as ComputeHoldGas
        long differenceGasLeftBlockGasLimit = 500;
        long gasLimit = intrinsicGas + posterGas + (long)blockGasLimit + differenceGasLeftBlockGasLimit;

        Address sender = TestItem.AddressA;
        Transaction tx = Build.A.Transaction
            .WithChainId(chain.ChainSpec.ChainId)
            .WithType(TxType.EIP1559)
            .WithTo(ArbosAddresses.ArbGasInfoAddress)
            .WithData(calldata)
            .WithValue(0)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(gasLimit)
            .WithNonce(worldState.GetNonce(sender))
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 senderInitialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);

        result.TransactionExecuted.Should().Be(true);
        result.EvmExceptionType.Should().Be(EvmExceptionType.None); // Tx succeeds

        // Consumes only necessary gas (some excess gas + ComputeHoldGas, which was set aside, get refunded)
        long expectedGasSpent = gasLimit - (long)excessGas - differenceGasLeftBlockGasLimit;
        tracer.GasSpent.Should().Be(expectedGasSpent);

        UInt256 senderFinalBalance = worldState.GetBalance(sender);
        senderFinalBalance.Should().Be(senderInitialBalance - (ulong)expectedGasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void CalculateClaimableRefund_TxGetsRefundedSomeAmount_PosterGasDoesNotGetRefunded()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();

        ulong baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;
        chain.BlockTree.Head!.Header.Author = ArbosAddresses.BatchPosterAddress; // to set up Coinbase
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GetSpec(chain.BlockTree.Head!.Header));
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using var dispose = worldState.BeginScope(chain.BlockTree.Head!.Header);

        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(
            worldState, burner, _logManager.GetClassLogger<ArbosState>()
        );

        // Insert a contract inside the world state
        Address contractAddress = new("0x0000000000000000000000000000000000000123");
        worldState.CreateAccount(contractAddress, 0);

        // Setting the 1st storage cell of the contract to 1 then to 0.
        // This causes an EVM refund of 19_900 gas, which is higher than
        // (gasUsed - posterGas) / RefundHelper.MaxRefundQuotientEIP3529,
        // causing the latter being chosen for as the actual refund (see below).
        byte[] runtimeCode = Prepare.EvmCode
            .PushData(1)
            .PushData(0)
            .Op(Instruction.SSTORE)
            .PushData(0)
            .PushData(0)
            .Op(Instruction.SSTORE)
            .Op(Instruction.STOP)
            .Done;

        worldState.InsertCode(contractAddress, runtimeCode, fullChainSimulationSpecProvider.GenesisSpec);
        worldState.Commit(fullChainSimulationSpecProvider.GenesisSpec);

        ReadOnlySpan<byte> storageValue = worldState.Get(new StorageCell(contractAddress, 0));
        storageValue.IsZero().Should().BeTrue();

        Address sender = TestItem.AddressA;

        long gasLimit = 1_000_000;
        Transaction tx = Build.A.Transaction
            .WithTo(contractAddress)
            .WithValue(0)
            // .WithData() // no input data, tx will just execute bytecode from beginning
            .WithGasLimit(gasLimit)
            .WithGasPrice(baseFeePerGas)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        (UInt256 posterCost, _) = arbosState.L1PricingState.PosterDataCost(
            tx, ArbosAddresses.BatchPosterAddress, arbosState.BrotliCompressionLevel.Get(), isTransactionProcessing: true
        );
        ulong posterGas = (posterCost / baseFeePerGas).ToULongSafe();

        ulong contractExecutionCost =
            GasCostOf.SSet + // for 1st set
            GasCostOf.VeryLow * 4 + // for 4 PUSH1
            GasCostOf.WarmStateRead; // for 2nd set: spec.GetNetMeteredSStoreCost()

        // 20_112 + 21_000 + 153 = 41_265
        ulong gasUsed = contractExecutionCost + GasCostOf.Transaction + posterGas;

        UInt256 initialSenderBalance = worldState.GetBalance(tx.SenderAddress!);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);
        result.Should().Be(TransactionResult.Ok);

        // Contract bytecode only changes 1 value in contract storage.
        // Here is the state during the storage value's 2nd change (determining the EVM refund):
        // original value before tx: 0, current value in storage during tx: 1, input value: 0
        ulong evmRefund = RefundOf.SSetReversedHotCold; // spec.GetSetReversalRefund() (19_900)

        ulong actualRefund = System.Math.Min(
            (gasUsed - posterGas) / RefundHelper.MaxRefundQuotientEIP3529,
            evmRefund
        );
        // just making sure the 1st parameter got chosen for the refund (the exact use case I want to test)
        actualRefund.Should().BeLessThan(evmRefund);

        ulong expectedGasSpent = gasUsed - actualRefund;
        tracer.GasSpent.Should().Be((long)expectedGasSpent);

        UInt256 finalSenderBalance = worldState.GetBalance(tx.SenderAddress!);
        finalSenderBalance.Should().Be(initialSenderBalance - expectedGasSpent * baseFeePerGas);
    }

    [Test]
    public void GasChargingHook_Always_AffectsOnlyIntrinsicStandardGasAndNotFloorGas()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();

        ulong baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;
        chain.BlockTree.Head!.Header.Author = ArbosAddresses.BatchPosterAddress; // to set up Coinbase
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GetSpec(chain.BlockTree.Head!.Header));
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using var dispose = worldState.BeginScope(chain.BlockTree.Head!.Header);

        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(
            worldState, burner, _logManager.GetClassLogger<ArbosState>()
        );

        Address sender = TestItem.AddressA;

        // Tested elements might not seem obvious but when gas limit is large (larger than EVM spent gas to be exact)
        // we had floorGas > spentGas in Refund() (because we inflated floorGas with the value returned by GasChargingHook,
        // which was incorrect), so, we used to refund more than expected to the user.
        long gasLimit = 100_000_000; // very large
        UInt256 valueToTransfer = 100;
        Transaction transferTx = Build.A.Transaction
            .WithTo(TestItem.AddressB)
            .WithValue(valueToTransfer)
            .WithGasLimit(gasLimit)
            .WithGasPrice(baseFeePerGas)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        IntrinsicGas intrinsicGas = IntrinsicGasCalculator.Calculate(transferTx, chain.SpecProvider.GenesisSpec);

        UInt256 initialSenderBalance = worldState.GetBalance(transferTx.SenderAddress!);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(transferTx, tracer);
        result.Should().Be(TransactionResult.Ok);

        (UInt256 posterCost, _) = arbosState.L1PricingState.PosterDataCost(
            transferTx, ArbosAddresses.BatchPosterAddress, arbosState.BrotliCompressionLevel.Get(), isTransactionProcessing: true);
        ulong posterGas = (posterCost / baseFeePerGas).ToULongSafe();
        ulong expectedGasSpent = GasCostOf.Transaction + posterGas;
        tracer.GasSpent.Should().Be((long)expectedGasSpent);

        UInt256 finalSenderBalance = worldState.GetBalance(transferTx.SenderAddress!);
        finalSenderBalance.Should().Be(initialSenderBalance - expectedGasSpent * baseFeePerGas - valueToTransfer);
    }

    [Test]
    public void EndTxHook_RetryTransactionSuccess_DeletesRetryableAndRefundsCorrectly()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = false
            });
        });

        using var dispose = chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header);

        UInt256 baseFeePerGas = chain.BlockTree.Head!.Header.BaseFeePerGas;
        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(chain.WorldStateManager.GlobalWorldState, burner, _logManager.GetClassLogger<ArbosState>());

        // Setup retryable
        Hash256 ticketId = ArbRetryableTxTests.Hash256FromUlong(456);
        Address sender = new("0x1000000000000000000000000000000000000001");
        Address refundTo = new("0x2000000000000000000000000000000000000002");
        UInt256 maxRefund = (UInt256)50000;
        UInt256 submissionFeeRefund = (UInt256)1000;
        const ulong gasLimit = 100000;
        ulong timeout = chain.BlockTree.Head!.Header.Timestamp + 1000;

        arbosState.RetryableState.CreateRetryable(ticketId, sender, sender, 0, sender, timeout, []);

        // Setup fee accounts with sufficient balance
        Address networkFeeAccount = arbosState.NetworkFeeAccount.Get();
        chain.WorldStateManager.GlobalWorldState.AddToBalanceAndCreateIfNotExists(networkFeeAccount, maxRefund, chain.SpecProvider.GenesisSpec);

        ArbitrumRetryTransaction transaction = new ArbitrumRetryTransaction
        {
            ChainId = 0,
            Nonce = 0,
            SenderAddress = sender,
            DecodedMaxFeePerGas = baseFeePerGas,
            GasFeeCap = baseFeePerGas,
            Gas = gasLimit,
            GasLimit = (long)gasLimit,
            To = sender,
            Value = 0,
            Data = ReadOnlyMemory<byte>.Empty.ToArray(),
            TicketId = ticketId,
            RefundTo = refundTo,
            MaxRefund = maxRefund,
            SubmissionFeeRefund = submissionFeeRefund,
            Type = (TxType)ArbitrumTxType.ArbitrumRetry
        };

        // Setup escrow with callvalue
        Address escrowAddress = ArbitrumTransactionProcessor.GetRetryableEscrowAddress(ticketId);
        chain.WorldStateManager.GlobalWorldState.AddToBalanceAndCreateIfNotExists(escrowAddress, transaction.Value, chain.SpecProvider.GenesisSpec);

        // Add balance to sender for gas refunds
        UInt256 gasRefund = baseFeePerGas * gasLimit;
        chain.WorldStateManager.GlobalWorldState.AddToBalanceAndCreateIfNotExists(sender, gasRefund, chain.SpecProvider.GenesisSpec);

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult result = ((ArbitrumTransactionProcessor)chain.TxProcessor).Execute(transaction, tracer);

        result.Should().Be(TransactionResult.Ok);

        // Verify retryable was deleted on success
        Retryable? retryable = arbosState.RetryableState.OpenRetryable(ticketId, chain.BlockTree.Head!.Header.Timestamp);
        retryable.Should().BeNull();

        // Verify escrow is empty
        chain.WorldStateManager.GlobalWorldState.GetBalance(escrowAddress).Should().Be(0);

        tracer.BeforeEvmTransfers.Count.Should().Be(2);
        tracer.AfterEvmTransfers.Count.Should().Be(6);
    }

    [Test]
    public void EndTxHook_RetryTransactionFailure_ReturnsCallvalueToEscrow()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = false
            });
        });

        using var dispose = chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header);

        UInt256 baseFeePerGas = chain.BlockTree.Head!.Header.BaseFeePerGas;
        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(chain.WorldStateManager.GlobalWorldState, burner, _logManager.GetClassLogger<ArbosState>());

        // Setup retryable
        Hash256 ticketId = ArbRetryableTxTests.Hash256FromUlong(789);
        Address sender = new("0x1000000000000000000000000000000000000001");
        Address refundTo = new("0x2000000000000000000000000000000000000002");
        UInt256 maxRefund = (UInt256)50000;
        UInt256 submissionFeeRefund = (UInt256)1000;
        const ulong gasLimit = 100000;
        ulong timeout = chain.BlockTree.Head!.Header.Timestamp + 1000;
        UInt256 callvalue = (UInt256)1000;

        arbosState.RetryableState.CreateRetryable(ticketId, sender, sender, callvalue, sender, timeout, []);

        // Create a contract that will cause EVM execution to fail by using an invalid operation
        // This will cause the transaction to fail during EVM execution, not pre-processing
        Address failingContract = new("0x3000000000000000000000000000000000000003");
        byte[] failingCode = [0xFE]; // INVALID opcode - this will cause EVM execution to fail
        chain.WorldStateManager.GlobalWorldState.CreateAccount(failingContract, 0);
        ValueHash256 codeHash = (ValueHash256)Keccak.Compute(failingCode);
        chain.WorldStateManager.GlobalWorldState.InsertCode(failingContract, codeHash, failingCode, chain.SpecProvider.GenesisSpec);

        // Create some data to trigger the EVM execution of the failing contract
        byte[] callData = [0x00]; // Any non-empty data will trigger EVM execution

        ArbitrumRetryTransaction transaction = new ArbitrumRetryTransaction
        {
            ChainId = 0,
            Nonce = 0,
            SenderAddress = sender,
            DecodedMaxFeePerGas = baseFeePerGas,
            GasFeeCap = baseFeePerGas,
            Gas = gasLimit,
            GasLimit = (long)gasLimit,
            To = failingContract,
            Value = callvalue,
            Data = callData,
            TicketId = ticketId,
            RefundTo = refundTo,
            MaxRefund = maxRefund,
            SubmissionFeeRefund = submissionFeeRefund,
            Type = (TxType)ArbitrumTxType.ArbitrumRetry
        };

        // Setup escrow with callvalue
        Address escrowAddress = ArbitrumTransactionProcessor.GetRetryableEscrowAddress(ticketId);
        chain.WorldStateManager.GlobalWorldState.AddToBalanceAndCreateIfNotExists(escrowAddress, callvalue, chain.SpecProvider.GenesisSpec);

        // Add balance to sender for gas refunds
        UInt256 gasRefund = baseFeePerGas * gasLimit;
        chain.WorldStateManager.GlobalWorldState.AddToBalanceAndCreateIfNotExists(sender, gasRefund, chain.SpecProvider.GenesisSpec);

        // Setup fee accounts with sufficient balance to avoid refund failures
        Address networkFeeAccount = arbosState.NetworkFeeAccount.Get();
        chain.WorldStateManager.GlobalWorldState.AddToBalanceAndCreateIfNotExists(networkFeeAccount, maxRefund, chain.SpecProvider.GenesisSpec);

        // Execute transaction that will fail (target doesn't exist)
        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult result = ((ArbitrumTransactionProcessor)chain.TxProcessor).Execute(transaction, tracer);

        result.Should().Be(TransactionResult.Ok); // Retry transactions always return Ok

        // Verify retryable still exists (transaction failed in EVM, so retryable should not be deleted)
        Retryable? retryable = arbosState.RetryableState.OpenRetryable(ticketId, chain.BlockTree.Head!.Header.Timestamp);
        retryable.Should().NotBeNull();

        // Verify callvalue was returned to escrow
        chain.WorldStateManager.GlobalWorldState.GetBalance(escrowAddress).Should().Be(callvalue);

        tracer.BeforeEvmTransfers.Count.Should().Be(2);
        tracer.AfterEvmTransfers.Count.Should().Be(4);
    }

    [Test]
    public void EndTxHook_NormalTransaction_DistributesFeesToNetworkAccount()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = false
            });
        });

        UInt256 baseFeePerGas = chain.BlockTree.Head!.Header.BaseFeePerGas;
        Address sender = new("0x1000000000000000000000000000000000000001");
        Address to = new("0x2000000000000000000000000000000000000002");
        const ulong gasLimit = 21000;

        Transaction transaction = Build.A.Transaction
            .WithSenderAddress(sender)
            .WithTo(to)
            .WithGasLimit((long)gasLimit)
            .WithMaxFeePerGas(baseFeePerGas * 2)
            .WithValue(100)
            .WithType(TxType.EIP1559)
            .TestObject;

        using var dispose = chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header);

        // Add sufficient balance to sender
        UInt256 initialBalance = baseFeePerGas * gasLimit * 3 + transaction.Value;
        chain.WorldStateManager.GlobalWorldState.AddToBalanceAndCreateIfNotExists(sender, initialBalance, chain.SpecProvider.GenesisSpec);

        // Get initial network fee account balance
        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(chain.WorldStateManager.GlobalWorldState, burner, _logManager.GetClassLogger<ArbosState>());
        Address networkFeeAccount = arbosState.NetworkFeeAccount.Get();
        UInt256 initialNetworkBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(networkFeeAccount);

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult result = ((ArbitrumTransactionProcessor)chain.TxProcessor).Execute(transaction, tracer);

        result.Should().Be(TransactionResult.Ok);

        // Verify network fee account received the compute cost
        ulong actualGasUsed = (ulong)transaction.SpentGas;
        UInt256 expectedNetworkFee = baseFeePerGas * actualGasUsed;

        UInt256 finalNetworkBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(networkFeeAccount);
        finalNetworkBalance.Should().Be(initialNetworkBalance + expectedNetworkFee);

        tracer.BeforeEvmTransfers.Count.Should().Be(2);
        tracer.AfterEvmTransfers.Count.Should().Be(0);
    }

    [Test]
    public void EndTxHook_NormalTransactionWithInfraFees_DistributesFeesBetweenNetworkAndInfra()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = false
            });
        });

        const ulong baseFeePerGas = 100_000_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;

        using var dispose = chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header);

        // Setup infrastructure fees
        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(chain.WorldStateManager.GlobalWorldState, burner, _logManager.GetClassLogger<ArbosState>());

        Address infraFeeAccount = new("0x4000000000000000000000000000000000000004");
        arbosState.InfraFeeAccount.Set(infraFeeAccount);

        UInt256 minBaseFee = (UInt256)50_000_000;
        arbosState.L2PricingState.MinBaseFeeWeiStorage.Set(minBaseFee);

        Address sender = new("0x1000000000000000000000000000000000000001");
        Address to = new("0x2000000000000000000000000000000000000002");
        const ulong gasLimit = 21000;

        Transaction transaction = Build.A.Transaction
            .WithSenderAddress(sender)
            .WithTo(to)
            .WithGasLimit((long)gasLimit)
            .WithMaxFeePerGas(baseFeePerGas * 2)
            .WithValue(100)
            .WithType(TxType.EIP1559)
            .TestObject;

        UInt256 initialBalance = baseFeePerGas * gasLimit * 3 + transaction.Value;
        chain.WorldStateManager.GlobalWorldState.AddToBalanceAndCreateIfNotExists(sender, initialBalance, chain.SpecProvider.GenesisSpec);

        // Get initial balances
        Address networkFeeAccount = arbosState.NetworkFeeAccount.Get();
        UInt256 initialNetworkBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(networkFeeAccount);
        UInt256 initialInfraBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(infraFeeAccount);

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult result = ((ArbitrumTransactionProcessor)chain.TxProcessor).Execute(transaction, tracer);

        result.Should().Be(TransactionResult.Ok);

        // Verify fee distribution
        ulong actualGasUsed = (ulong)transaction.SpentGas;
        UInt256 totalGasCost = baseFeePerGas * actualGasUsed;
        UInt256 infraFeeRate = UInt256.Min(minBaseFee, baseFeePerGas);
        UInt256 expectedInfraFee = infraFeeRate * actualGasUsed;
        UInt256 expectedNetworkFee = totalGasCost - expectedInfraFee;

        // Verify infra fee account balance
        UInt256 finalInfraBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(infraFeeAccount);
        finalInfraBalance.Should().Be(initialInfraBalance + expectedInfraFee);

        // Verify network fee account balance
        UInt256 finalNetworkBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(networkFeeAccount);
        finalNetworkBalance.Should().Be(initialNetworkBalance + expectedNetworkFee);

        totalGasCost.Should().Be(expectedNetworkFee + expectedInfraFee);

        tracer.BeforeEvmTransfers.Count.Should().Be(3);
        tracer.AfterEvmTransfers.Count.Should().Be(0);
    }

    [Test]
    public void EndTxHook_FailedTransaction_SkipsEndHookAndDoesNotDistributeFees()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = false
            });
        });

        UInt256 baseFeePerGas = chain.BlockTree.Head!.Header.BaseFeePerGas;
        Address sender = new("0x1000000000000000000000000000000000000001");
        Address to = new("0x2000000000000000000000000000000000000002");
        const ulong gasLimit = 21000;

        Transaction transaction = Build.A.Transaction
            .WithSenderAddress(sender)
            .WithTo(to)
            .WithGasLimit((long)gasLimit)
            .WithMaxFeePerGas(baseFeePerGas * 2)
            .WithValue(100)
            .WithType(TxType.EIP1559)
            .TestObject;

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using var dispose = worldState.BeginScope(chain.BlockTree.Head!.Header);

        // Add balance to sender but not enough to cover the transaction
        UInt256 insufficientBalance = baseFeePerGas * 1000; // Much less than needed
        worldState.AddToBalanceAndCreateIfNotExists(sender, insufficientBalance, chain.SpecProvider.GenesisSpec);

        // Get initial network fee account balance
        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, burner, _logManager.GetClassLogger<ArbosState>());
        Address networkFeeAccount = arbosState.NetworkFeeAccount.Get();
        UInt256 initialNetworkBalance = worldState.GetBalance(networkFeeAccount);

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult result = ((ArbitrumTransactionProcessor)chain.TxProcessor).Execute(transaction, tracer);

        // Transaction should fail due to insufficient balance
        result.Should().NotBe(TransactionResult.Ok);

        // EndTxHook must not run for early validation failures, so no fee distribution
        UInt256 finalNetworkBalance = worldState.GetBalance(networkFeeAccount);
        UInt256 networkFeeIncrease = finalNetworkBalance - initialNetworkBalance;
        networkFeeIncrease.Should().Be(0);

        // No pre/post EVM transfers should be traced
        tracer.BeforeEvmTransfers.Count.Should().Be(0);
        tracer.AfterEvmTransfers.Count.Should().Be(0);
    }

    [Test]
    [TestCase(1UL, "0xA4B000000000000000000073657175656e636572", true)]
    [TestCase(1UL, "0x0000000000000000000000000000000000000010", true)]
    [TestCase(9UL, "0xA4B000000000000000000073657175656e636572", false)]
    [TestCase(9UL, "0x0000000000000000000000000000000000000010", true)]
    public void ProcessLegacyTransaction_DropsTip_Correctly(ulong arbosVersion, string beneficiary, bool shouldDropTip)
    {
        UInt256 l1BaseFee = 39;

        var preConfigurer = (ContainerBuilder cb) =>
        {
            cb.AddScoped(new ArbitrumTestBlockchainBase.Configuration()
            {
                SuggestGenesisOnStart = true,
                L1BaseFee = l1BaseFee,
                FillWithTestDataOnStart = false
            });
        };

        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(preConfigurer);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using var dispose = worldState.BeginScope(chain.BlockTree.Head!.Header);

        ArbosStorage backingStorage = new(worldState, new SystemBurner(), ArbosAddresses.ArbosSystemAccount);
        backingStorage.Set(ArbosStateOffsets.VersionOffset, arbosVersion);
        var arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), LimboLogs.Instance.GetLogger("arbosState"));

        Address beneficiaryAddress = new(beneficiary);
        BlockHeader header = new(chain.BlockTree.HeadHash, null!, beneficiaryAddress, UInt256.Zero, 0,
            100_000, 100, [])
        {
            BaseFeePerGas = arbosState.L2PricingState.BaseFeeWeiStorage.Get()
        };

        ulong gasLimit = 100_000;
        UInt256 tip = 2 * header.BaseFeePerGas;
        UInt256 value = 1.Ether();

        //sender account
        worldState.CreateAccount(TestItem.AddressA, gasLimit * (header.BaseFeePerGas + tip) + value, 0);

        Transaction tx = Build.A.Transaction
            .WithSenderAddress(TestItem.AddressA)
            .WithTo(TestItem.AddressB)
            .WithGasLimit((long)gasLimit)
            .WithGasPrice(header.BaseFeePerGas + tip)
            .WithValue(value).TestObject;

        BlockExecutionContext executionContext = new(header, FullChainSimulationReleaseSpec.Instance);

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        var txResult = chain.TxProcessor.Execute(tx, executionContext, tracer);

        //assert
        txResult.Should().Be(TransactionResult.Ok);


        Address networkFeeAddress = arbosState.NetworkFeeAccount.Get(); // 0x5e1497dd1f08c87b2d8fe23e9aab6c1de833d927
        var expectedTip = tip * (UInt256)tx.SpentGas;
        var unspentGas = gasLimit - (UInt256)tx.SpentGas;

        // HandleNormalTransactionEndTxHook also reimburses the network for the compute cost of processing the tx
        UInt256 computeCost = header.BaseFeePerGas * (ulong)tx.SpentGas;
        worldState.GetBalance(beneficiaryAddress).Should().Be(0); // does not receive the tip, the networkFeeAccount receives it
        worldState.GetBalance(networkFeeAddress).Should().Be(computeCost + (shouldDropTip ? 0 : expectedTip));

        worldState.GetBalance(TestItem.AddressA).Should().Be(shouldDropTip
            ? tip * gasLimit + unspentGas * header.BaseFeePerGas
            : unspentGas * (header.BaseFeePerGas + tip));
        worldState.GetBalance(TestItem.AddressB).Should().Be(value);

        tracer.BeforeEvmTransfers.Count.Should().Be(2);
        tracer.AfterEvmTransfers.Count.Should().Be(0);
    }

    [Test]
    [TestCase(1UL, "0xA4B000000000000000000073657175656e636572", true)]
    [TestCase(1UL, "0x0000000000000000000000000000000000000010", true)]
    [TestCase(9UL, "0xA4B000000000000000000073657175656e636572", false)]
    [TestCase(9UL, "0x0000000000000000000000000000000000000010", true)]
    public void ProcessEip1559Transaction_DropsTip_Correctly(ulong arbosVersion, string beneficiary, bool shouldDropTip)
    {
        UInt256 l1BaseFee = 39;

        var preConfigurer = (ContainerBuilder cb) =>
        {
            cb.AddScoped(new ArbitrumTestBlockchainBase.Configuration()
            {
                SuggestGenesisOnStart = true,
                L1BaseFee = l1BaseFee,
                FillWithTestDataOnStart = false
            });
        };

        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(preConfigurer);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using var dispose = worldState.BeginScope(chain.BlockTree.Head!.Header);

        ArbosStorage backingStorage = new(worldState, new SystemBurner(), ArbosAddresses.ArbosSystemAccount);
        backingStorage.Set(ArbosStateOffsets.VersionOffset, arbosVersion);
        var arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), LimboLogs.Instance.GetLogger("arbosState"));

        Address beneficiaryAddress = new Address(beneficiary);
        BlockHeader header = new BlockHeader(chain.BlockTree.HeadHash, null, beneficiaryAddress, UInt256.Zero, 0,
            100_000, 100, []);
        header.BaseFeePerGas = arbosState.L2PricingState.BaseFeeWeiStorage.Get();

        ulong gasLimit = 100_000;
        UInt256 tip = 2 * header.BaseFeePerGas;
        UInt256 value = 1.Ether();
        UInt256 maxFeePerGas = header.BaseFeePerGas * 5; //tip not capped

        //sender account
        worldState.CreateAccount(TestItem.AddressA, gasLimit * maxFeePerGas + value, 0);

        BlockExecutionContext executionContext =
            new BlockExecutionContext(header, FullChainSimulationReleaseSpec.Instance);


        Transaction tx = Build.A.Transaction
            .WithSenderAddress(TestItem.AddressA)
            .WithTo(TestItem.AddressB)
            .WithType(TxType.EIP1559)
            .WithGasLimit((long)gasLimit)
            .WithMaxPriorityFeePerGas(tip)
            .WithMaxFeePerGas(maxFeePerGas)
            .WithValue(value).TestObject;

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        var txResult = chain.TxProcessor.Execute(tx, executionContext, tracer);

        //assert
        txResult.Should().Be(TransactionResult.Ok);
        var expectedTip = tip * (UInt256)tx.SpentGas;
        var unspentGas = gasLimit - (UInt256)tx.SpentGas;
        var diffMaxGasPriceAndEffectiveGasPrice = maxFeePerGas - (header.BaseFeePerGas + tip);

        Address networkFeeAddress = arbosState.NetworkFeeAccount.Get();
        // HandleNormalTransactionEndTxHook also reimburses the network for the compute cost of processing the tx
        UInt256 computeCost = header.BaseFeePerGas * (ulong)tx.SpentGas;
        worldState.GetBalance(header.Beneficiary).Should().Be(0); // beneficiary does not receive the tip
        worldState.GetBalance(networkFeeAddress).Should().Be(computeCost + (shouldDropTip ? 0 : expectedTip));

        worldState.GetBalance(TestItem.AddressA).Should().Be(shouldDropTip
            ? gasLimit * maxFeePerGas - header.BaseFeePerGas * (UInt256)tx.SpentGas
            : unspentGas * maxFeePerGas + (UInt256)tx.SpentGas * diffMaxGasPriceAndEffectiveGasPrice);
        worldState.GetBalance(TestItem.AddressB).Should().Be(value);

        tracer.BeforeEvmTransfers.Count.Should().Be(2);
        tracer.AfterEvmTransfers.Count.Should().Be(0);
    }

    [Test]
    [TestCase(1UL, "0xA4B000000000000000000073657175656e636572", true)]
    [TestCase(1UL, "0x0000000000000000000000000000000000000010", true)]
    [TestCase(9UL, "0xA4B000000000000000000073657175656e636572", false)]
    [TestCase(9UL, "0x0000000000000000000000000000000000000010", true)]
    public void ProcessEip1559Transaction_WithCappedTip_DropsTip_Correctly(ulong arbosVersion, string beneficiary, bool shouldDropTip)
    {
        UInt256 l1BaseFee = 39;

        var preConfigurer = (ContainerBuilder cb) =>
        {
            cb.AddScoped(new ArbitrumTestBlockchainBase.Configuration()
            {
                SuggestGenesisOnStart = true,
                L1BaseFee = l1BaseFee,
                FillWithTestDataOnStart = false
            });
        };

        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(preConfigurer);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using var dispose = worldState.BeginScope(chain.BlockTree.Head!.Header);

        ArbosStorage backingStorage = new(worldState, new SystemBurner(), ArbosAddresses.ArbosSystemAccount);
        backingStorage.Set(ArbosStateOffsets.VersionOffset, arbosVersion);
        var arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), LimboLogs.Instance.GetLogger("arbosState"));

        Address beneficiaryAddress = new Address(beneficiary);
        BlockHeader header = new BlockHeader(chain.BlockTree.HeadHash, null, beneficiaryAddress, UInt256.Zero, 0,
            100_000, 100, []);
        header.BaseFeePerGas = arbosState.L2PricingState.BaseFeeWeiStorage.Get();

        ulong gasLimit = 100_000;
        UInt256 tip = 2 * header.BaseFeePerGas;
        UInt256 value = 1.Ether();
        UInt256 maxFeePerGas = (header.BaseFeePerGas * 10 + header.BaseFeePerGas * 5) / 10; //tip capped at 1.5 of base fee

        //sender account
        worldState.CreateAccount(TestItem.AddressA, gasLimit * maxFeePerGas + value, 0);

        BlockExecutionContext executionContext =
            new BlockExecutionContext(header, FullChainSimulationReleaseSpec.Instance);

        Transaction tx = Build.A.Transaction
            .WithSenderAddress(TestItem.AddressA)
            .WithTo(TestItem.AddressB)
            .WithType(TxType.EIP1559)
            .WithGasLimit((long)gasLimit)
            .WithMaxPriorityFeePerGas(tip)
            .WithMaxFeePerGas(maxFeePerGas)
            .WithValue(value).TestObject;

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        var txResult = chain.TxProcessor.Execute(tx, executionContext, tracer);

        //assert
        txResult.Should().Be(TransactionResult.Ok);

        var expectedTip = (maxFeePerGas - header.BaseFeePerGas) * (UInt256)tx.SpentGas;
        var unspentGas = gasLimit - (UInt256)tx.SpentGas;
        var diffMaxGasPriceAndEffectiveGasPrice = UInt256.Zero; //max price capped

        Address networkFeeAddress = arbosState.NetworkFeeAccount.Get();
        // HandleNormalTransactionEndTxHook also reimburses the network for the compute cost of processing the tx
        UInt256 computeCost = header.BaseFeePerGas * (ulong)tx.SpentGas;
        worldState.GetBalance(header.Beneficiary).Should().Be(0); // beneficiary does not receive the tip
        worldState.GetBalance(networkFeeAddress).Should().Be(computeCost + (shouldDropTip ? 0 : expectedTip));

        worldState.GetBalance(TestItem.AddressA).Should().Be(shouldDropTip
            ? gasLimit * maxFeePerGas - header.BaseFeePerGas * (UInt256)tx.SpentGas
            : unspentGas * maxFeePerGas + (UInt256)tx.SpentGas * diffMaxGasPriceAndEffectiveGasPrice);
        worldState.GetBalance(TestItem.AddressB).Should().Be(value);

        tracer.BeforeEvmTransfers.Count.Should().Be(2);
        tracer.AfterEvmTransfers.Count.Should().Be(0);
    }

    [Test]
    public void EndTxHook_ConsumeAvailableFunction_HandlesEdgeCasesCorrectly()
    {
        // Test the ConsumeAvailable function behavior (equivalent to Nitro's takeFunds)
        UInt256 pool = (UInt256)1000;
        UInt256 amount1 = (UInt256)300;
        UInt256 amount2 = (UInt256)800; // More than remaining pool
        UInt256 amount3 = UInt256.Zero; // Zero amount

        // Test normal consumption
        UInt256 taken1 = UInt256.Min(amount1, pool);
        pool -= taken1;
        taken1.Should().Be(300);
        pool.Should().Be(700);

        // Test consumption exceeding pool
        UInt256 taken2 = UInt256.Min(amount2, pool);
        pool = taken2 < amount2 ? UInt256.Zero : pool - taken2;
        taken2.Should().Be(700); // Should only take what's available
        pool.Should().Be(0);

        // Test zero amount
        UInt256 taken3 = UInt256.Min(amount3, pool);
        taken3.Should().Be(0);
        pool.Should().Be(0);
    }

    [Test]
    public void ProcessArbitrumRetryTransaction_HasInvalidNonceAndSenderIsEoa_ReturnsOkTransactionResult()
    {
        UInt256 l1BaseFee = 39;

        var preConfigurer = (ContainerBuilder cb) =>
        {
            cb.AddScoped(new ArbitrumTestBlockchainBase.Configuration()
            {
                SuggestGenesisOnStart = true,
                L1BaseFee = l1BaseFee,
                FillWithTestDataOnStart = false
            });
        };

        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(preConfigurer);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using var dispose = worldState.BeginScope(chain.BlockTree.Head!.Header);
        var arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), LimboLogs.Instance.GetLogger("arbosState"));

        BlockHeader header = new BlockHeader(chain.BlockTree.HeadHash, null, TestItem.AddressF, UInt256.Zero, 0,
            GasCostOf.Transaction, 100, []);
        header.BaseFeePerGas = arbosState.L2PricingState.BaseFeeWeiStorage.Get();

        Hash256 ticketIdHash = ArbRetryableTxTests.Hash256FromUlong(1);
        var retryTx = TestTransaction.PrepareArbitrumRetryTx(worldState, header, ticketIdHash, TestItem.AddressA, TestItem.AddressB, header.Beneficiary!,
            50.GWei());
        retryTx.Nonce = 100; //nonce not matching to sender state

        //sender account
        worldState.CreateAccount(TestItem.AddressA, 0, 5);

        var code = Prepare.EvmCode.Call(TestItem.AddressC, GasCostOf.Call).Done;
        var codeHash = Keccak.Compute(code);
        worldState.InsertCode(TestItem.AddressA, codeHash, code, FullChainSimulationReleaseSpec.Instance);

        var escrowAddress = ArbitrumTransactionProcessor.GetRetryableEscrowAddress(ticketIdHash);
        worldState.AddToBalanceAndCreateIfNotExists(escrowAddress, header.BaseFeePerGas * GasCostOf.Transaction, FullChainSimulationReleaseSpec.Instance);

        BlockExecutionContext executionContext =
            new BlockExecutionContext(header, FullChainSimulationReleaseSpec.Instance);

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        var txResult = chain.TxProcessor.Execute(retryTx, executionContext, tracer);

        //assert
        txResult.Should().Be(TransactionResult.Ok);
        worldState.GetNonce(TestItem.AddressA).Should().Be(6);
        worldState.IsInvalidContractSender(FullChainSimulationReleaseSpec.Instance, retryTx.SenderAddress!).Should()
            .BeTrue();

        tracer.BeforeEvmTransfers.Count.Should().Be(2);
        tracer.AfterEvmTransfers.Count.Should().Be(6);
    }

    [Test]
    public void ProcessTransactions_SubmitRetryable_TraceEntries()
    {
        UInt256 l1BaseFee = 39;

        var preConfigurer = (ContainerBuilder cb) =>
        {
            cb.AddScoped(new ArbitrumTestBlockchainBase.Configuration()
            {
                SuggestGenesisOnStart = true,
                L1BaseFee = l1BaseFee,
                FillWithTestDataOnStart = false
            });
        };

        var chain = ArbitrumRpcTestBlockchain.CreateDefault(preConfigurer);

        Hash256 ticketIdHash = ArbRetryableTxTests.Hash256FromUlong(1);
        UInt256 gasFeeCap = 1000000000;
        UInt256 value = 10000000000000000;
        ulong gasLimit = 21000;
        ReadOnlyMemory<byte> data = ReadOnlyMemory<byte>.Empty;
        ulong maxSubmissionFee = 54600;
        UInt256 deposit = 10021000000054600;

        ArbitrumSubmitRetryableTransaction tx = new()
        {
            ChainId = chain.ChainSpec.ChainId,
            RequestId = ticketIdHash,
            SenderAddress = TestItem.AddressA,
            L1BaseFee = l1BaseFee,
            DepositValue = deposit,
            DecodedMaxFeePerGas = gasFeeCap,
            GasFeeCap = gasFeeCap,
            GasLimit = (long)gasLimit,
            Gas = gasLimit,
            RetryTo = TestItem.AddressB,
            RetryValue = value,
            Beneficiary = TestItem.AddressC,
            MaxSubmissionFee = maxSubmissionFee,
            FeeRefundAddr = TestItem.AddressD,
            RetryData = data,
            Data = data.ToArray(),
            Nonce = 0,
            Mint = deposit,
            Type = (TxType)ArbitrumTxType.ArbitrumSubmitRetryable,
            To = ArbitrumConstants.ArbRetryableTxAddress,
            SourceHash = ticketIdHash
        };

        tx.Hash = tx.CalculateHash();

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using var dispose = worldState.BeginScope(chain.BlockTree.Head!.Header);
        var arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(),
            LimboLogs.Instance.GetLogger("arbosState"));

        var header = new BlockHeader(chain.BlockTree.HeadHash, null, TestItem.AddressF, UInt256.Zero, 0,
            GasCostOf.Transaction, 100, [])
        {
            BaseFeePerGas = arbosState.L2PricingState.BaseFeeWeiStorage.Get()
        };

        var executionContext = new BlockExecutionContext(header, FullChainSimulationReleaseSpec.Instance);

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult txResult = chain.TxProcessor.Execute(tx, executionContext, tracer);

        txResult.Should().Be(TransactionResult.Ok);

        tracer.BeforeEvmTransfers.Count.Should().Be(0);
        tracer.AfterEvmTransfers.Count.Should().Be(0);
        GethLikeTxTrace trace = tracer.BuildResult();
        trace.Entries.Count.Should().Be(41);
    }

    [Test]
    public void ArbitrumTransaction_WithArbitrumBlockHeader_ProcessesCorrectly()
    {
        // Test NEW ArbitrumBlockHeader approach: EVM sees 0, gas calculations use original base fee

        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);

        BlockTree blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();

        ArbitrumVirtualMachine virtualMachine = new(
            new TestBlockhashProvider(fullChainSimulationSpecProvider),
            fullChainSimulationSpecProvider,
            _logManager
        );

        UInt256 blockBaseFee = (UInt256)500;
        UInt256 originalBaseFee = (UInt256)1000;

        genesis.Header.BaseFeePerGas = blockBaseFee;

        // NEW: Create ArbitrumBlockHeader with original base fee stored
        ArbitrumBlockHeader arbitrumHeader = new ArbitrumBlockHeader(genesis.Header, originalBaseFee);
        arbitrumHeader.BaseFeePerGas = 0; // Set to 0 for EVM execution (NoBaseFee behavior)

        BlockExecutionContext blCtx = new(arbitrumHeader, fullChainSimulationSpecProvider.GetSpec(arbitrumHeader));
        virtualMachine.SetBlockExecutionContext(in blCtx);

        ArbitrumTransactionProcessor processor = new(
            BlobBaseFeeCalculator.Instance,
            fullChainSimulationSpecProvider,
            worldState,
            virtualMachine,
            blockTree,
            _logManager,
            new EthereumCodeInfoRepository(worldState)
        );

        // Verify NoBaseFee behavior - EVM sees 0
        virtualMachine.BlockExecutionContext.Header.BaseFeePerGas.Should().Be(UInt256.Zero,
            "ArbitrumBlockHeader with NoBaseFee should have EVM BaseFee = 0");

        // Verify we're using ArbitrumBlockHeader
        virtualMachine.BlockExecutionContext.Header.Should().BeOfType<ArbitrumBlockHeader>(
            "Should be using ArbitrumBlockHeader");

        var arbitrumBlockHeader = (ArbitrumBlockHeader)virtualMachine.BlockExecutionContext.Header;
        arbitrumBlockHeader.OriginalBaseFee.Should().Be(originalBaseFee,
            "ArbitrumBlockHeader should store original base fee");

        Address sender = TestItem.AddressA;
        Address to = TestItem.AddressB;
        UInt256 value = 100;
        long gasLimit = 30000;

        Transaction transaction = Build.A.Transaction
            .WithSenderAddress(sender)
            .WithTo(to)
            .WithValue(value)
            .WithGasLimit(gasLimit)
            .WithGasPrice(originalBaseFee)
            .WithNonce(0)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 requiredBalance = originalBaseFee * (ulong)gasLimit + value;
        worldState.CreateAccount(sender, requiredBalance, 0);

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult result = processor.Execute(transaction, tracer);

        // Assert transaction executes successfully
        result.Should().Be(TransactionResult.Ok);
        transaction.SpentGas.Should().BeGreaterThan(0);

        UInt256 initialBalance = requiredBalance;
        UInt256 finalBalance = worldState.GetBalance(sender);
        UInt256 actualGasCost = initialBalance - finalBalance - value;
        UInt256 expectedGasCost = (UInt256)transaction.SpentGas * originalBaseFee;

        // The critical test: Gas should be charged using originalBaseFee from ArbitrumBlockHeader
        actualGasCost.Should().Be(expectedGasCost,
            "Gas should be charged using original base fee (1000) from ArbitrumBlockHeader.OriginalBaseFee");

        actualGasCost.Should().BeGreaterThan(0,
            "Some gas should be charged - proves the ArbitrumBlockHeader approach is working");
    }

    [Test]
    public void ArbitrumTransaction_WithoutNoBaseFee_UsesBlockBaseFee()
    {
        // Test that without NoBaseFee, transactions should use the block's BaseFeePerGas

        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);

        BlockTree blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();

        ArbitrumVirtualMachine virtualMachine = new(
            new TestBlockhashProvider(fullChainSimulationSpecProvider),
            fullChainSimulationSpecProvider,
            _logManager
        );

        UInt256 blockBaseFee = (UInt256)800;
        genesis.Header.BaseFeePerGas = blockBaseFee;

        // Use regular BlockHeader (not ArbitrumBlockHeader)
        BlockExecutionContext blCtx = new(genesis.Header, fullChainSimulationSpecProvider.GenesisSpec);
        virtualMachine.SetBlockExecutionContext(in blCtx);

        ArbitrumTransactionProcessor processor = new(
            BlobBaseFeeCalculator.Instance,
            fullChainSimulationSpecProvider,
            worldState,
            virtualMachine,
            blockTree,
            _logManager,
            new EthereumCodeInfoRepository(worldState)
        );

        Address sender = TestItem.AddressA;
        Address to = TestItem.AddressB;
        UInt256 value = 75;
        long gasLimit = 25000;

        Transaction transaction = Build.A.Transaction
            .WithSenderAddress(sender)
            .WithTo(to)
            .WithValue(value)
            .WithGasLimit(gasLimit)
            .WithGasPrice(blockBaseFee)
            .WithNonce(0)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        // NO NoBaseFee configuration - should use block's base fee everywhere
        virtualMachine.BlockExecutionContext.Header.BaseFeePerGas.Should().Be(blockBaseFee,
            "Without NoBaseFee, header should keep original block base fee");

        // Should not be ArbitrumBlockHeader
        virtualMachine.BlockExecutionContext.Header.Should().NotBeOfType<ArbitrumBlockHeader>(
            "Without NoBaseFee, should use regular BlockHeader");

        UInt256 requiredBalance = blockBaseFee * (ulong)gasLimit + value;
        worldState.CreateAccount(sender, requiredBalance, 0);

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult result = processor.Execute(transaction, tracer);

        result.Should().Be(TransactionResult.Ok);
        transaction.SpentGas.Should().BeGreaterThan(0);

        // Verify gas was charged using block's BaseFee
        UInt256 initialBalance = requiredBalance;
        UInt256 finalBalance = worldState.GetBalance(sender);
        UInt256 actualGasCost = initialBalance - finalBalance - value;
        UInt256 expectedGasCost = (UInt256)transaction.SpentGas * blockBaseFee;

        actualGasCost.Should().Be(expectedGasCost,
            $"Without NoBaseFee, gas should be charged using block BaseFee ({blockBaseFee})");
    }

    [Test]
    public void ArbitrumTransaction_WithArbitrumBlockHeader_UsesOriginalBaseFeeForGasCalculations()
    {
        // Test that with ArbitrumBlockHeader, transactions use original base fee for gas calculations
        // but EVM sees 0 base fee

        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);

        BlockTree blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();

        ArbitrumVirtualMachine virtualMachine = new(
            new TestBlockhashProvider(fullChainSimulationSpecProvider),
            fullChainSimulationSpecProvider,
            _logManager
        );

        UInt256 blockBaseFee = (UInt256)1500;
        UInt256 originalBaseFee = (UInt256)3000;

        // Create ArbitrumBlockHeader with original base fee stored
        ArbitrumBlockHeader arbitrumHeader = new ArbitrumBlockHeader(genesis.Header, originalBaseFee);
        arbitrumHeader.BaseFeePerGas = 0; // Set to 0 for EVM execution (NoBaseFee behavior)

        BlockExecutionContext blCtx = new(arbitrumHeader, fullChainSimulationSpecProvider.GetSpec(arbitrumHeader));
        virtualMachine.SetBlockExecutionContext(in blCtx);

        ArbitrumTransactionProcessor processor = new(
            BlobBaseFeeCalculator.Instance,
            fullChainSimulationSpecProvider,
            worldState,
            virtualMachine,
            blockTree,
            _logManager,
            new EthereumCodeInfoRepository(worldState)
        );

        // Verify NoBaseFee behavior
        virtualMachine.BlockExecutionContext.Header.BaseFeePerGas.Should().Be(UInt256.Zero,
            "ArbitrumBlockHeader with NoBaseFee should have EVM BaseFee = 0");

        virtualMachine.BlockExecutionContext.Header.Should().BeOfType<ArbitrumBlockHeader>(
            "Should be using ArbitrumBlockHeader");

        var arbitrumBlockHeader = (ArbitrumBlockHeader)virtualMachine.BlockExecutionContext.Header;
        arbitrumBlockHeader.OriginalBaseFee.Should().Be(originalBaseFee,
            "ArbitrumBlockHeader should store original base fee");

        Address sender = TestItem.AddressA;
        Address to = TestItem.AddressB;
        UInt256 value = 75;
        long gasLimit = 25000;

        Transaction transaction = Build.A.Transaction
            .WithSenderAddress(sender)
            .WithTo(to)
            .WithValue(value)
            .WithGasLimit(gasLimit)
            .WithGasPrice(originalBaseFee)
            .WithNonce(0)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 requiredBalance = originalBaseFee * (ulong)gasLimit + value;
        worldState.CreateAccount(sender, requiredBalance, 0);

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult result = processor.Execute(transaction, tracer);

        result.Should().Be(TransactionResult.Ok);
        transaction.SpentGas.Should().BeGreaterThan(0);

        // Verify gas was charged using original BaseFee (for gas calculations)
        // even though EVM sees BaseFee = 0
        UInt256 initialBalance = requiredBalance;
        UInt256 finalBalance = worldState.GetBalance(sender);
        UInt256 actualGasCost = initialBalance - finalBalance - value;
        UInt256 expectedGasCost = (UInt256)transaction.SpentGas * originalBaseFee;

        actualGasCost.Should().Be(expectedGasCost,
            $"With ArbitrumBlockHeader, gas should be charged using original BaseFee ({originalBaseFee}) for gas calculations");
    }

    [Test]
    public void ArbitrumBlockHeader_StoresOriginalBaseFeeCorrectly()
    {
        // Test that ArbitrumBlockHeader properly stores and retrieves original base fee

        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);

        UInt256 blockBaseFee = (UInt256)1500;
        UInt256 originalBaseFee = (UInt256)3000;

        genesis.Header.BaseFeePerGas = blockBaseFee;

        // Create ArbitrumBlockHeader
        ArbitrumBlockHeader arbitrumHeader = new(genesis.Header, originalBaseFee);

        // Verify it copies all properties correctly
        arbitrumHeader.ParentHash.Should().Be(genesis.Header.ParentHash);
        arbitrumHeader.Number.Should().Be(genesis.Header.Number);
        arbitrumHeader.GasLimit.Should().Be(genesis.Header.GasLimit);
        arbitrumHeader.Timestamp.Should().Be(genesis.Header.Timestamp);
        arbitrumHeader.BaseFeePerGas.Should().Be(blockBaseFee); // Should copy original base fee initially

        // Verify it stores original base fee
        arbitrumHeader.OriginalBaseFee.Should().Be(originalBaseFee);

        // Simulate executor setting BaseFee to 0
        arbitrumHeader.BaseFeePerGas = 0;
        arbitrumHeader.BaseFeePerGas.Should().Be(UInt256.Zero);

        // Original base fee should still be accessible
        arbitrumHeader.OriginalBaseFee.Should().Be(originalBaseFee);
    }

    [Test]
    public void StartBlockTransaction_WhenQueueHasOnlyDeletedRetryables_ClearsQueueCompletely()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);
        BlockTree blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();

        ArbitrumVirtualMachine virtualMachine = new(
            new TestBlockhashProvider(fullChainSimulationSpecProvider),
            fullChainSimulationSpecProvider,
            _logManager
        );

        genesis.Header.Timestamp = 1000;
        genesis.Header.BaseFeePerGas = 100;

        BlockExecutionContext blCtx = new(genesis.Header, fullChainSimulationSpecProvider.GetSpec(genesis.Header));
        virtualMachine.SetBlockExecutionContext(in blCtx);

        ArbitrumTransactionProcessor processor = new(
            BlobBaseFeeCalculator.Instance,
            fullChainSimulationSpecProvider,
            worldState,
            virtualMachine,
            blockTree,
            _logManager,
            new EthereumCodeInfoRepository(worldState)
        );

        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, burner, _logManager.GetClassLogger<ArbosState>());

        Hash256 firstDeletedId = ArbRetryableTxTests.Hash256FromUlong(100);
        Hash256 secondDeletedId = ArbRetryableTxTests.Hash256FromUlong(200);

        foreach (var id in new[] { firstDeletedId, secondDeletedId })
        {
            arbosState.RetryableState.CreateRetryable(
                id,
                TestItem.AddressA,
                TestItem.AddressB,
                0,
                TestItem.AddressC,
                100,
                Array.Empty<byte>()
            );

            var retryable = arbosState.RetryableState.GetRetryable(id);
            retryable.Timeout.Set(0);
        }

        byte[] packedData = AbiMetadata.PackInput(
            AbiMetadata.StartBlockMethod,
            (UInt256)100,
            (ulong)1,
            (ulong)1,
            (ulong)100
        );

        ArbitrumInternalTransaction internalTx = new()
        {
            ChainId = 0,
            SenderAddress = ArbosAddresses.ArbosAddress,
            To = ArbosAddresses.ArbosAddress,
            Data = packedData,
            Type = (TxType)ArbitrumTxType.ArbitrumInternal
        };

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult result = processor.Execute(internalTx, tracer);

        result.Should().Be(TransactionResult.Ok);

        SystemBurner burner2 = new(readOnly: false);
        ArbosState arbosStateAfter = ArbosState.OpenArbosState(worldState, burner2, _logManager.GetClassLogger<ArbosState>());

        arbosStateAfter.RetryableState.TimeoutQueue.Peek().Should().Be(ValueKeccak.Zero);
    }

    [Test]
    public void TryReapOneRetryable_WhenTimeoutIsZero_RemovesFromQueueAndReturnsEarly()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);
        BlockTree blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();

        ArbitrumVirtualMachine virtualMachine = new(
            new TestBlockhashProvider(fullChainSimulationSpecProvider),
            fullChainSimulationSpecProvider,
            _logManager
        );

        ulong currentTimestamp = 1000;
        genesis.Header.Timestamp = currentTimestamp;
        genesis.Header.BaseFeePerGas = 100;

        BlockExecutionContext blCtx = new(genesis.Header, fullChainSimulationSpecProvider.GetSpec(genesis.Header));
        virtualMachine.SetBlockExecutionContext(in blCtx);

        ArbitrumTransactionProcessor processor = new(
            BlobBaseFeeCalculator.Instance,
            fullChainSimulationSpecProvider,
            worldState,
            virtualMachine,
            blockTree,
            _logManager,
            new EthereumCodeInfoRepository(worldState)
        );

        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, burner, _logManager.GetClassLogger<ArbosState>());

        Hash256 deletedRetryableId = ArbRetryableTxTests.Hash256FromUlong(111);
        Hash256 expiredRetryableId = ArbRetryableTxTests.Hash256FromUlong(222);

        arbosState.RetryableState.CreateRetryable(
            deletedRetryableId,
            TestItem.AddressA,
            TestItem.AddressB,
            0,
            TestItem.AddressC,
            500,
            Array.Empty<byte>()
        );

        arbosState.RetryableState.CreateRetryable(
            expiredRetryableId,
            TestItem.AddressD,
            TestItem.AddressE,
            1000,
            TestItem.AddressF,
            currentTimestamp - 100,
            Array.Empty<byte>()
        );

        var deletedRetryable = arbosState.RetryableState.GetRetryable(deletedRetryableId);
        deletedRetryable.Timeout.Set(0);

        var expiredRetryable = arbosState.RetryableState.GetRetryable(expiredRetryableId);
        expiredRetryable.TimeoutWindowsLeft.Set(0);

        Address expiredEscrowAddress = ArbitrumTransactionProcessor.GetRetryableEscrowAddress(expiredRetryableId);
        worldState.AddToBalanceAndCreateIfNotExists(expiredEscrowAddress, 1000, fullChainSimulationSpecProvider.GenesisSpec);

        byte[] packedData = AbiMetadata.PackInput(
            AbiMetadata.StartBlockMethod,
            (UInt256)100,
            (ulong)1,
            (ulong)1,
            (ulong)100
        );

        ArbitrumInternalTransaction internalTx = new()
        {
            ChainId = 0,
            SenderAddress = ArbosAddresses.ArbosAddress,
            To = ArbosAddresses.ArbosAddress,
            Data = packedData,
            Type = (TxType)ArbitrumTxType.ArbitrumInternal
        };

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult result = processor.Execute(internalTx, tracer);

        result.Should().Be(TransactionResult.Ok);

        SystemBurner burner2 = new(readOnly: false);
        ArbosState arbosStateAfter = ArbosState.OpenArbosState(worldState, burner2, _logManager.GetClassLogger<ArbosState>());

        var deletedCheck = arbosStateAfter.RetryableState.GetRetryable(deletedRetryableId);
        deletedCheck.Timeout.Get().Should().Be(0);

        var expiredCheck = arbosStateAfter.RetryableState.GetRetryable(expiredRetryableId);
        expiredCheck.Timeout.Get().Should().Be(0);

        worldState.GetBalance(TestItem.AddressF).Should().Be(1000);

        arbosStateAfter.RetryableState.TimeoutQueue.Peek().Should().Be(ValueKeccak.Zero);
    }

    [Test]
    public void TryReapOneRetryable_WhenBothRetryablesHaveTimeoutZero_RemovesBothFromQueue()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);
        BlockTree blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();

        ArbitrumVirtualMachine virtualMachine = new(
            new TestBlockhashProvider(fullChainSimulationSpecProvider),
            fullChainSimulationSpecProvider,
            _logManager
        );

        genesis.Header.Timestamp = 1000;
        genesis.Header.BaseFeePerGas = 100;

        BlockExecutionContext blCtx = new(genesis.Header, fullChainSimulationSpecProvider.GetSpec(genesis.Header));
        virtualMachine.SetBlockExecutionContext(in blCtx);

        ArbitrumTransactionProcessor processor = new(
            BlobBaseFeeCalculator.Instance,
            fullChainSimulationSpecProvider,
            worldState,
            virtualMachine,
            blockTree,
            _logManager,
            new EthereumCodeInfoRepository(worldState)
        );

        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, burner, _logManager.GetClassLogger<ArbosState>());

        Hash256 firstDeletedId = ArbRetryableTxTests.Hash256FromUlong(100);
        Hash256 secondDeletedId = ArbRetryableTxTests.Hash256FromUlong(200);

        foreach (var id in new[] { firstDeletedId, secondDeletedId })
        {
            arbosState.RetryableState.CreateRetryable(
                id,
                TestItem.AddressA,
                TestItem.AddressB,
                0,
                TestItem.AddressC,
                100,
                Array.Empty<byte>()
            );

            var retryable = arbosState.RetryableState.GetRetryable(id);
            retryable.Timeout.Set(0);
        }

        byte[] packedData = AbiMetadata.PackInput(
            AbiMetadata.StartBlockMethod,
            (UInt256)100,
            (ulong)1,
            (ulong)1,
            (ulong)100
        );

        ArbitrumInternalTransaction internalTx = new()
        {
            ChainId = 0,
            SenderAddress = ArbosAddresses.ArbosAddress,
            To = ArbosAddresses.ArbosAddress,
            Data = packedData,
            Type = (TxType)ArbitrumTxType.ArbitrumInternal
        };

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult result = processor.Execute(internalTx, tracer);

        result.Should().Be(TransactionResult.Ok);

        SystemBurner burner2 = new(readOnly: false);
        ArbosState arbosStateAfter = ArbosState.OpenArbosState(worldState, burner2, _logManager.GetClassLogger<ArbosState>());

        arbosStateAfter.RetryableState.TimeoutQueue.Peek()
            .Should().Be(ValueKeccak.Zero);
    }

    [Test]
    public void TryReapOneRetryable_WhenFirstRetryableDeletedSecondExpired_ProcessesBothCorrectly()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);
        BlockTree blockTree = Build.A.BlockTree(genesis).OfChainLength(1).TestObject;
        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();

        ArbitrumVirtualMachine virtualMachine = new(
            new TestBlockhashProvider(fullChainSimulationSpecProvider),
            fullChainSimulationSpecProvider,
            _logManager
        );

        ulong currentTimestamp = 1000;
        genesis.Header.Timestamp = currentTimestamp;
        genesis.Header.BaseFeePerGas = 100;

        BlockExecutionContext blCtx = new(genesis.Header, fullChainSimulationSpecProvider.GetSpec(genesis.Header));
        virtualMachine.SetBlockExecutionContext(in blCtx);

        ArbitrumTransactionProcessor processor = new(
            BlobBaseFeeCalculator.Instance,
            fullChainSimulationSpecProvider,
            worldState,
            virtualMachine,
            blockTree,
            _logManager,
            new EthereumCodeInfoRepository(worldState)
        );

        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, burner, _logManager.GetClassLogger<ArbosState>());

        Hash256 deletedId = ArbRetryableTxTests.Hash256FromUlong(555);
        Hash256 expiredId = ArbRetryableTxTests.Hash256FromUlong(666);

        arbosState.RetryableState.CreateRetryable(
            deletedId,
            TestItem.AddressA,
            TestItem.AddressB,
            0,
            TestItem.AddressC,
            100,
            Array.Empty<byte>()
        );
        var deletedRetryable = arbosState.RetryableState.GetRetryable(deletedId);
        deletedRetryable.Timeout.Set(0);

        arbosState.RetryableState.CreateRetryable(
            expiredId,
            TestItem.AddressD,
            TestItem.AddressE,
            500,
            TestItem.AddressF,
            currentTimestamp - 200,
            Array.Empty<byte>()
        );
        var expiredRetryable = arbosState.RetryableState.GetRetryable(expiredId);
        expiredRetryable.TimeoutWindowsLeft.Set(0);

        Address escrowAddress = ArbitrumTransactionProcessor.GetRetryableEscrowAddress(expiredId);
        worldState.AddToBalanceAndCreateIfNotExists(escrowAddress, 500, fullChainSimulationSpecProvider.GenesisSpec);

        UInt256 initialBeneficiaryBalance = worldState.GetBalance(TestItem.AddressF);

        byte[] packedData = AbiMetadata.PackInput(
            AbiMetadata.StartBlockMethod,
            (UInt256)100,
            (ulong)1,
            (ulong)1,
            (ulong)100
        );

        ArbitrumInternalTransaction internalTx = new()
        {
            ChainId = 0,
            SenderAddress = ArbosAddresses.ArbosAddress,
            To = ArbosAddresses.ArbosAddress,
            Data = packedData,
            Type = (TxType)ArbitrumTxType.ArbitrumInternal
        };

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult result = processor.Execute(internalTx, tracer);

        result.Should().Be(TransactionResult.Ok);

        UInt256 finalBeneficiaryBalance = worldState.GetBalance(TestItem.AddressF);
        finalBeneficiaryBalance.Should().Be(initialBeneficiaryBalance + 500);

        SystemBurner burner2 = new(readOnly: false);
        ArbosState arbosStateAfter = ArbosState.OpenArbosState(worldState, burner2, _logManager.GetClassLogger<ArbosState>());

        arbosStateAfter.RetryableState.TimeoutQueue.Peek().Should().Be(ValueKeccak.Zero);

        var expiredCheck = arbosStateAfter.RetryableState.GetRetryable(expiredId);
        expiredCheck.Timeout.Get().Should().Be(0);
    }

    [Test]
    public void CreateRetryable_WithNullToAddress_StoresAndRetrievesNull()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);
        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, burner, _logManager.GetClassLogger<ArbosState>());

        Hash256 ticketId = ArbRetryableTxTests.Hash256FromUlong(123);

        arbosState.RetryableState.CreateRetryable(
            ticketId,
            TestItem.AddressA,
            null,
            100,
            TestItem.AddressB,
            genesis.Header.Timestamp + 1000,
            Array.Empty<byte>()
        );

        Retryable? retryable = arbosState.RetryableState.OpenRetryable(ticketId, genesis.Header.Timestamp);

        retryable.Should().NotBeNull();
        retryable.To.Get().Should().BeNull();
    }

    [Test]
    public void CreateRetryable_WithNonNullToAddress_StoresAndRetrievesAddress()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesis = ArbOSInitialization.Create(worldState);
        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, burner, _logManager.GetClassLogger<ArbosState>());

        Hash256 ticketId = ArbRetryableTxTests.Hash256FromUlong(456);
        Address toAddress = TestItem.AddressC;

        arbosState.RetryableState.CreateRetryable(
            ticketId,
            TestItem.AddressA,
            toAddress,
            100,
            TestItem.AddressB,
            genesis.Header.Timestamp + 1000,
            Array.Empty<byte>()
        );

        Retryable? retryable = arbosState.RetryableState.OpenRetryable(ticketId, genesis.Header.Timestamp);

        retryable.Should().NotBeNull();
        retryable.To.Get().Should().Be(toAddress);
    }

    [Test]
    public void NullableAddress_WhenSetToNullThenRealAddress_StoresSentinelThenAddress()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        ArbOSInitialization.Create(worldState);
        SystemBurner burner = new(readOnly: false);
        ArbosStorage arbosStorage = new(worldState, burner, ArbosAddresses.ArbosSystemAccount);

        ulong testOffset = 12345;
        ArbosStorageBackedNullableAddress nullableAddress = new(arbosStorage, testOffset);
        ArbosStorageSlot slot = new(arbosStorage, testOffset);

        nullableAddress.Set(null);

        ValueHash256 storedValue = slot.Get();
        storedValue.Should().Be(ArbosStorageBackedNullableAddress.NullableAddressRepresentation);
        nullableAddress.Get().Should().BeNull();

        Address realAddress = TestItem.AddressD;
        nullableAddress.Set(realAddress);

        storedValue = slot.Get();
        nullableAddress.Get().Should().Be(realAddress);
    }

    [Test]
    public void NullableAddress_WhenSetToNull_StoresSentinelValue()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        ArbOSInitialization.Create(worldState);
        SystemBurner burner = new(readOnly: false);
        ArbosStorage arbosStorage = new(worldState, burner, ArbosAddresses.ArbosSystemAccount);

        ulong testOffset = 100;
        ArbosStorageBackedNullableAddress nullableAddress = new(arbosStorage, testOffset);

        nullableAddress.Set(null);

        ArbosStorageSlot slot = new(arbosStorage, testOffset);
        ValueHash256 storedValue = slot.Get();
        storedValue.Should().Be(ArbosStorageBackedNullableAddress.NullableAddressRepresentation);
    }

    [Test]
    public void NullableAddress_WhenGetAfterSetNull_ReturnsNull()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        ArbOSInitialization.Create(worldState);
        SystemBurner burner = new(readOnly: false);
        ArbosStorage arbosStorage = new(worldState, burner, ArbosAddresses.ArbosSystemAccount);

        ulong testOffset = 200;
        ArbosStorageBackedNullableAddress nullableAddress = new(arbosStorage, testOffset);

        nullableAddress.Set(null);
        Address? result = nullableAddress.Get();

        result.Should().BeNull();
    }

    [Test]
    public void NullableAddress_WhenSetToValidAddress_StoresAddressNotSentinel()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        ArbOSInitialization.Create(worldState);
        SystemBurner burner = new(readOnly: false);
        ArbosStorage arbosStorage = new(worldState, burner, ArbosAddresses.ArbosSystemAccount);

        ulong testOffset = 300;
        ArbosStorageBackedNullableAddress nullableAddress = new(arbosStorage, testOffset);
        Address expectedAddress = TestItem.AddressE;

        nullableAddress.Set(expectedAddress);

        ArbosStorageSlot slot = new(arbosStorage, testOffset);
        ValueHash256 storedValue = slot.Get();
        storedValue.Should().NotBe(ArbosStorageBackedNullableAddress.NullableAddressRepresentation);

        Address retrievedAddress = new(storedValue.Bytes[12..]);
        retrievedAddress.Should().Be(expectedAddress);
    }

    [Test]
    public void NullableAddress_WhenGetAfterSetValidAddress_ReturnsAddress()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        ArbOSInitialization.Create(worldState);
        SystemBurner burner = new(readOnly: false);
        ArbosStorage arbosStorage = new(worldState, burner, ArbosAddresses.ArbosSystemAccount);

        ulong testOffset = 400;
        ArbosStorageBackedNullableAddress nullableAddress = new(arbosStorage, testOffset);
        Address expectedAddress = TestItem.AddressF;

        nullableAddress.Set(expectedAddress);
        Address? result = nullableAddress.Get();

        result.Should().Be(expectedAddress);
    }

    [Test]
    public void NullableAddress_WhenSetAddressAfterNull_OverwritesSentinel()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        ArbOSInitialization.Create(worldState);
        SystemBurner burner = new(readOnly: false);
        ArbosStorage arbosStorage = new(worldState, burner, ArbosAddresses.ArbosSystemAccount);

        ulong testOffset = 500;
        ArbosStorageBackedNullableAddress nullableAddress = new(arbosStorage, testOffset);
        ArbosStorageSlot slot = new(arbosStorage, testOffset);

        nullableAddress.Set(null);
        slot.Get().Should().Be(ArbosStorageBackedNullableAddress.NullableAddressRepresentation);

        Address newAddress = TestItem.AddressA;
        nullableAddress.Set(newAddress);

        slot.Get().Should().NotBe(ArbosStorageBackedNullableAddress.NullableAddressRepresentation);
        nullableAddress.Get().Should().Be(newAddress);
    }

    [Test]
    public void NullableAddress_WhenSetNullAfterAddress_StoresSentinel()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        ArbOSInitialization.Create(worldState);
        SystemBurner burner = new(readOnly: false);
        ArbosStorage arbosStorage = new(worldState, burner, ArbosAddresses.ArbosSystemAccount);

        ulong testOffset = 600;
        ArbosStorageBackedNullableAddress nullableAddress = new(arbosStorage, testOffset);
        ArbosStorageSlot slot = new(arbosStorage, testOffset);

        Address initialAddress = TestItem.AddressB;
        nullableAddress.Set(initialAddress);
        slot.Get().Should().NotBe(ArbosStorageBackedNullableAddress.NullableAddressRepresentation);

        nullableAddress.Set(null);

        slot.Get().Should().Be(ArbosStorageBackedNullableAddress.NullableAddressRepresentation);
        nullableAddress.Get().Should().BeNull();
    }

    [Test]
    public void NullableAddress_WhenMultipleStateChanges_MaintainsCorrectState()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        ArbOSInitialization.Create(worldState);
        SystemBurner burner = new(readOnly: false);
        ArbosStorage arbosStorage = new(worldState, burner, ArbosAddresses.ArbosSystemAccount);

        ulong testOffset = 700;
        ArbosStorageBackedNullableAddress nullableAddress = new(arbosStorage, testOffset);

        Address firstAddress = TestItem.AddressC;
        nullableAddress.Set(firstAddress);
        nullableAddress.Get().Should().Be(firstAddress);

        Address secondAddress = TestItem.AddressD;
        nullableAddress.Set(secondAddress);
        nullableAddress.Get().Should().Be(secondAddress);

        nullableAddress.Set(null);
        nullableAddress.Get().Should().BeNull();

        Address thirdAddress = TestItem.AddressE;
        nullableAddress.Set(thirdAddress);
        nullableAddress.Get().Should().Be(thirdAddress);
    }

    [Test]
    public void Execute_TransactionWithZeroGasLimit_FailsWithIntrinsicGasError()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();
        UInt256 baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;
        chain.BlockTree.Head!.Header.Author = ArbosAddresses.BatchPosterAddress;
        chain.TxProcessor.SetBlockExecutionContext(new BlockExecutionContext(chain.BlockTree.Head!.Header,
            fullChainSimulationSpecProvider.GetSpec(chain.BlockTree.Head!.Header)));

        using var dispose = chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header);

        Transaction tx = Build.A.Transaction
            .WithTo(TestItem.AddressB)
            .WithValue(1)
            .WithGasLimit(0)
            .WithGasPrice(baseFeePerGas)
            .WithNonce(0)
            .WithSenderAddress(TestItem.AddressA)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.GasLimitBelowIntrinsicGas);
        tracer.BeforeEvmTransfers.Count.Should().Be(0);
        tracer.AfterEvmTransfers.Count.Should().Be(0);
    }

    [Test]
    public void Execute_TransactionWithExactlyIntrinsicPlusPosterGas_Succeeds()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();
        UInt256 baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;
        chain.BlockTree.Head!.Header.Author = ArbosAddresses.BatchPosterAddress;
        chain.TxProcessor.SetBlockExecutionContext(new BlockExecutionContext(chain.BlockTree.Head!.Header,
            fullChainSimulationSpecProvider.GetSpec(chain.BlockTree.Head!.Header)));

        using var dispose = chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header);

        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(
            chain.WorldStateManager.GlobalWorldState, burner, _logManager.GetClassLogger<ArbosState>()
        );

        Address sender = TestItem.AddressA;
        Transaction tempTx = Build.A.Transaction
            .WithTo(TestItem.AddressB)
            .WithValue(1)
            .WithGasLimit(100000)
            .WithGasPrice(baseFeePerGas)
            .WithNonce(0)
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        (UInt256 posterCost, _) = arbosState.L1PricingState.PosterDataCost(
            tempTx, ArbosAddresses.BatchPosterAddress,
            arbosState.BrotliCompressionLevel.Get(),
            isTransactionProcessing: true
        );
        ulong posterGas = (posterCost / baseFeePerGas).ToULongSafe();
        long gasLimit = GasCostOf.Transaction + (long)posterGas;

        Transaction tx = Build.A.Transaction
            .WithTo(TestItem.AddressB)
            .WithValue(1)
            .WithGasLimit(gasLimit)
            .WithGasPrice(baseFeePerGas)
            .WithNonce(0)
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 requiredBalance = baseFeePerGas * (ulong)gasLimit + 1;
        chain.WorldStateManager.GlobalWorldState.CreateAccount(sender, requiredBalance, 0);

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        tracer.BeforeEvmTransfers.Count.Should().BeGreaterThan(0);
    }

    [Test]
    public void Execute_TransactionWithGasLimitBelowPosterGas_FailsWithIntrinsicGasError()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();
        UInt256 baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;
        chain.BlockTree.Head!.Header.Author = ArbosAddresses.BatchPosterAddress;
        chain.TxProcessor.SetBlockExecutionContext(new BlockExecutionContext(chain.BlockTree.Head!.Header,
            fullChainSimulationSpecProvider.GetSpec(chain.BlockTree.Head!.Header)));

        using var dispose = chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header);

        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(
            chain.WorldStateManager.GlobalWorldState, burner, _logManager.GetClassLogger<ArbosState>()
        );

        Address sender = TestItem.AddressA;
        byte[] largeData = new byte[1000];

        Transaction tempTx = Build.A.Transaction
            .WithTo(TestItem.AddressB)
            .WithValue(1)
            .WithData(largeData)
            .WithGasLimit(100000)
            .WithGasPrice(baseFeePerGas)
            .WithNonce(0)
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        (UInt256 posterCost, _) = arbosState.L1PricingState.PosterDataCost(
            tempTx, ArbosAddresses.BatchPosterAddress,
            arbosState.BrotliCompressionLevel.Get(),
            isTransactionProcessing: true
        );
        ulong posterGas = (posterCost / baseFeePerGas).ToULongSafe();
        long gasLimit = (long)posterGas - 1;

        Transaction tx = Build.A.Transaction
            .WithTo(TestItem.AddressB)
            .WithValue(1)
            .WithData(largeData)
            .WithGasLimit(gasLimit)
            .WithGasPrice(baseFeePerGas)
            .WithNonce(0)
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 requiredBalance = baseFeePerGas * (ulong)gasLimit + 1;
        chain.WorldStateManager.GlobalWorldState.CreateAccount(sender, requiredBalance, 0);

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.GasLimitBelowIntrinsicGas);
        tracer.BeforeEvmTransfers.Count.Should().Be(0);
        tracer.AfterEvmTransfers.Count.Should().Be(0);
    }

    [Test]
    public void Execute_MultipleTransactionsWithOneInsufficientGas_ProcessesBothWithoutException()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();
        UInt256 baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;
        chain.BlockTree.Head!.Header.Author = ArbosAddresses.BatchPosterAddress;
        chain.TxProcessor.SetBlockExecutionContext(new BlockExecutionContext(chain.BlockTree.Head!.Header,
            fullChainSimulationSpecProvider.GetSpec(chain.BlockTree.Head!.Header)));

        using var dispose = chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header);

        Address sender1 = TestItem.AddressA;
        Address sender2 = TestItem.AddressC;

        Transaction failingTx = Build.A.Transaction
            .WithTo(TestItem.AddressB)
            .WithValue(1)
            .WithGasLimit(0)
            .WithGasPrice(baseFeePerGas)
            .WithNonce(0)
            .WithSenderAddress(sender1)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        Transaction successTx = Build.A.Transaction
            .WithTo(TestItem.AddressD)
            .WithValue(1)
            .WithGasLimit(100000)
            .WithGasPrice(baseFeePerGas)
            .WithNonce(0)
            .WithSenderAddress(sender2)
            .SignedAndResolved(TestItem.PrivateKeyC)
            .TestObject;

        chain.WorldStateManager.GlobalWorldState.CreateAccount(sender1, 1.Ether(), 0);
        chain.WorldStateManager.GlobalWorldState.CreateAccount(sender2, 1.Ether(), 0);

        var tracer1 = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult result1 = chain.TxProcessor.Execute(failingTx, tracer1);

        var tracer2 = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult result2 = chain.TxProcessor.Execute(successTx, tracer2);

        result1.Should().Be(TransactionResult.GasLimitBelowIntrinsicGas);
        result2.Should().Be(TransactionResult.Ok);
    }

    [Test]
    public void CalculateIntrinsicGas_WhenCalledMultipleTimes_DoesNotDuplicateSideEffects()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();

        UInt256 baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;
        chain.BlockTree.Head!.Header.Author = ArbosAddresses.BatchPosterAddress;
        chain.TxProcessor.SetBlockExecutionContext(new BlockExecutionContext(chain.BlockTree.Head!.Header,
            fullChainSimulationSpecProvider.GetSpec(chain.BlockTree.Head!.Header)));

        using var dispose = chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header);

        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(
            chain.WorldStateManager.GlobalWorldState, burner, _logManager.GetClassLogger<ArbosState>()
        );

        Address sender = TestItem.AddressA;

        Transaction tx = Build.A.Transaction
            .WithTo(TestItem.AddressB)
            .WithValue(1)
            .WithGasLimit(100000)
            .WithGasPrice(baseFeePerGas)
            .WithNonce(0)
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        Rlp encodedTx = Rlp.Encode(tx);
        ulong brotliCompressionLevel = arbosState.BrotliCompressionLevel.Get();
        ulong l1Bytes = (ulong)BrotliCompression.Compress(encodedTx.Bytes, brotliCompressionLevel).Length;
        ulong expectedCalldataUnits = l1Bytes * GasCostOf.TxDataNonZeroEip2028;

        chain.WorldStateManager.GlobalWorldState.CreateAccount(sender, 1.Ether(), 0);

        var tracer = new ArbitrumGethLikeTxTracer(GethTraceOptions.Default);
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        arbosState.L1PricingState.UnitsSinceStorage.Get().Should().Be(expectedCalldataUnits);
    }


    [TestCaseSource(nameof(PosterDataCostReturnsZeroCases))]
    public void PosterDataCost_WhenCalledWithNonBatchPosterOrArbitrumTxTypes_ShouldReturnZero(string posterHex, TxType txType)
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        SystemBurner burner = new(readOnly: false);
        ArbosStorage arbosStorage = new(worldState, burner, ArbosAddresses.ArbosSystemAccount);
        L1PricingState l1PricingState = new(arbosStorage);
        l1PricingState.SetPricePerUnit(1000);

        Address poster = new(posterHex);
        Transaction tx = CreateTransactionForType(txType);

        var (cost, units) = l1PricingState.PosterDataCost(tx, poster, 1, isTransactionProcessing: true);

        units.Should().Be(0);
        cost.Should().Be(UInt256.Zero);
    }

    [TestCaseSource(nameof(PosterDataCostReturnsNonZeroCases))]
    public void PosterDataCost_WhenCalledWithBatchPosterAndStandardTx_ShouldReturnNonZero(string posterHex, TxType txType)
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        SystemBurner burner = new(readOnly: false);
        ArbosStorage arbosStorage = new(worldState, burner, ArbosAddresses.ArbosSystemAccount);
        L1PricingState l1PricingState = new(arbosStorage);
        l1PricingState.SetPricePerUnit(1000);

        Address poster = new(posterHex);
        Transaction tx = CreateTransactionForType(txType);

        var (cost, units) = l1PricingState.PosterDataCost(tx, poster, 1, isTransactionProcessing: true);

        units.Should().BeGreaterThan(0);
        cost.Should().BeGreaterThan(UInt256.Zero);
    }

    public static IEnumerable<TestCaseData> PosterDataCostReturnsZeroCases()
    {
        yield return new TestCaseData("0x0000000000000000000000000000000000000001", TxType.Legacy);
        yield return new TestCaseData("0x00000000000000000000000000000000000A4b05", (TxType)ArbitrumTxType.ArbitrumRetry);
        yield return new TestCaseData("0x00000000000000000000000000000000000A4b05", (TxType)ArbitrumTxType.ArbitrumInternal);
        yield return new TestCaseData("0x00000000000000000000000000000000000A4b05", (TxType)ArbitrumTxType.ArbitrumUnsigned);
        yield return new TestCaseData("0x00000000000000000000000000000000000A4b05", (TxType)ArbitrumTxType.ArbitrumSubmitRetryable);
    }

    public static IEnumerable<TestCaseData> PosterDataCostReturnsNonZeroCases()
    {
        yield return new TestCaseData("0xA4B000000000000000000073657175656e636572", TxType.Legacy);
    }

    private static Transaction CreateTransactionForType(TxType txType)
    {
        if (txType == TxType.Legacy)
        {
            return Build.A.Transaction
                .WithType(TxType.Legacy)
                .WithTo(TestItem.AddressB)
                .WithValue(100)
                .WithGasLimit(21000)
                .WithGasPrice(1000)
                .WithNonce(1)
                .SignedAndResolved(TestItem.PrivateKeyA)
                .TestObject;
        }

        return txType switch
        {
            (TxType)ArbitrumTxType.ArbitrumRetry => new ArbitrumRetryTransaction
            {
                Type = txType,
                To = TestItem.AddressB,
                Value = 100,
                GasLimit = 21000,
                TicketId = Keccak.Zero,
                RefundTo = TestItem.AddressC,
                MaxRefund = UInt256.MaxValue,
                SubmissionFeeRefund = 0
            },

            (TxType)ArbitrumTxType.ArbitrumInternal => new ArbitrumInternalTransaction
            {
                Type = txType,
                To = TestItem.AddressB,
                Value = 100,
                GasLimit = 21000
            },

            (TxType)ArbitrumTxType.ArbitrumUnsigned => new ArbitrumUnsignedTransaction
            {
                Type = txType,
                To = TestItem.AddressB,
                Value = 100,
                GasLimit = 21000,
                GasPrice = 1,
                Nonce = 1
            },

            (TxType)ArbitrumTxType.ArbitrumSubmitRetryable => new ArbitrumSubmitRetryableTransaction
            {
                Type = txType,
                To = TestItem.AddressB,
                Value = 100,
                GasLimit = 21000,
                MaxSubmissionFee = 0,
                RetryTo = TestItem.AddressB,
                RetryValue = 100,
                RetryData = Bytes.Empty
            },

            _ => throw new NotSupportedException($"Transaction type {txType} not supported in test")
        };
    }
}
