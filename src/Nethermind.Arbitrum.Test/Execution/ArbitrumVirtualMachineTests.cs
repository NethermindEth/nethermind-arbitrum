using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Evm.Test;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Test.Execution;

public class ArbitrumVirtualMachineTests
{
    private static readonly TestLogManager _logManager = new();

    [Test]
    public void OpGasPriceOpCode_ArbosVersionIsGreaterThanTwoAndNotNine_ReturnsBaseFeePerGas()
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
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        // Insert a contract inside the world state
        Address contractAddress = new("0x0000000000000000000000000000000000000123");
        worldState.CreateAccount(contractAddress, 0);

        // Bytecode to return the gas price used in a tx
        byte[] runtimeCode = Prepare.EvmCode
            .Op(Instruction.GASPRICE)
            .PushData(0)
            .Op(Instruction.MSTORE) // stores gas price at memory offset 0
            .PushData(32)
            .PushData(0)
            .Op(Instruction.RETURN) // returns 32 bytes of data from memory offset 0
            .Op(Instruction.STOP)
            .Done;

        worldState.InsertCode(contractAddress, runtimeCode, fullChainSimulationSpecProvider.GenesisSpec);
        worldState.Commit(fullChainSimulationSpecProvider.GenesisSpec);

        // Just making sure contract got created
        ReadOnlySpan<byte> storageValue = worldState.Get(new StorageCell(contractAddress, 0));
        storageValue.IsZero().Should().BeTrue();

        Address sender = TestItem.AddressA;

        long gasLimit = 1_000_000;

        Transaction tx = Build.A.Transaction
            .WithTo(contractAddress)
            .WithValue(0)
            // .WithData() // no input data, tx will just call execute bytecode from beginning
            .WithGasLimit(gasLimit)

            // make tx.GasPrice <= baseFee to have a different effectiveGasPrice
            // (hence vm.txExecContext.GasPrice) than baseFee. This allows to have vm.TxExecutionContext.GasPrice
            // different from vm.BlockExecutionContext.Header.BaseFeePerGas to correctly assert GasPrice opcode's returned value.
            .WithMaxPriorityFeePerGas(baseFeePerGas)

            // MaxFeePerGas will become effectiveGasPrice as maxFeePerGas < tx.MaxPriorityFeePerGas + baseFee
            // Make it greater than baseFeePerGas for BuyGas to succeed
            .WithMaxFeePerGas(baseFeePerGas + 1)

            .WithType(TxType.EIP1559)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);

        // return vm.BlockExecutionContext.Header.BaseFeePerGas instead of vm.TxExecutionContext.GasPrice
        UInt256 returnedGasPrice = new(tracer.ReturnValue, isBigEndian: true);
        returnedGasPrice.ToUInt64(null).Should().Be(baseFeePerGas);
    }

    [Test]
    public void OpGasPriceOpCode_ArbosVersionIsNine_ReturnsTxGasPrice()
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
        // Set author to have blockContext.Coinbase == ArbosAddresses.BatchPosterAddress in DropTip logic
        // so that CalculateEffectiveGasPrice() returns effectiveGasPrice instead of effectiveBaseFee
        chain.BlockTree.Head!.Header.Author = ArbosAddresses.BatchPosterAddress;

        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        ArbosState arbosState = ArbosState.OpenArbosState(
            worldState, new ZeroGasBurner(), _logManager.GetClassLogger<ArbosState>()
        );
        // Set arbos version to 9 so that GasPrice opcode returns tx.GasPrice
        arbosState.BackingStorage.Set(ArbosStateOffsets.VersionOffset, ArbosVersion.Nine);

        // Having DropTip return false allows to have a different effectiveGasPrice returned by CalculateEffectiveGasPrice()
        // (hence vm.txExecContext.GasPrice) than baseFee. This allows to have vm.TxExecutionContext.GasPrice
        // different from vm.BlockExecutionContext.Header.BaseFeePerGas to correctly assert GasPrice opcode's returned value.

        // Insert a contract inside the world state
        Address contractAddress = new("0x0000000000000000000000000000000000000123");
        worldState.CreateAccount(contractAddress, 0);

        // Bytecode to return the gas price used in a tx
        byte[] runtimeCode = Prepare.EvmCode
            .Op(Instruction.GASPRICE)
            .PushData(0)
            .Op(Instruction.MSTORE) // stores gas price at memory offset 0
            .PushData(32)
            .PushData(0)
            .Op(Instruction.RETURN) // returns 32 bytes of data from memory offset 0
            .Op(Instruction.STOP)
            .Done;

        worldState.InsertCode(contractAddress, runtimeCode, fullChainSimulationSpecProvider.GenesisSpec);
        worldState.Commit(fullChainSimulationSpecProvider.GenesisSpec);

        // Just making sure contract got created
        ReadOnlySpan<byte> storageValue = worldState.Get(new StorageCell(contractAddress, 0));
        storageValue.IsZero().Should().BeTrue();

        Address sender = TestItem.AddressA;

        long gasLimit = 1_000_000;
        // MaxFeePerGas will become effectiveGasPrice in base.CalculateEffectiveGasPrice()
        // as maxFeePerGas < tx.MaxPriorityFeePerGas + baseFee.
        // And we make it greater than baseFeePerGas for BuyGas to succeed
        ulong maxFeePerGas = baseFeePerGas + 1;

        Transaction tx = Build.A.Transaction
            .WithTo(contractAddress)
            .WithValue(0)
            // .WithData() // no input data, tx will just execute bytecode from beginning
            .WithGasLimit(gasLimit)
            .WithMaxPriorityFeePerGas(baseFeePerGas)
            .WithType(TxType.EIP1559)
            .WithMaxFeePerGas(maxFeePerGas)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);

        // return vm.TxExecutionContext.GasPrice and not vm.BlockExecutionContext.Header.BaseFeePerGas
        UInt256 returnedGasPrice = new(tracer.ReturnValue, isBigEndian: true);
        returnedGasPrice.ToUInt64(null).Should().Be(maxFeePerGas);
    }

    [Test]
    public void OpNumberOpCode_Always_ReturnsL1BlockNumber()
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
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        ulong l2BlockNumber = blCtx.Number;
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        ArbosState arbosState = ArbosState.OpenArbosState(
            worldState, new ZeroGasBurner(), _logManager.GetClassLogger<ArbosState>()
        );

        ulong l1BlockNumber = 111;
        arbosState.Blockhashes.RecordNewL1Block(l1BlockNumber, Hash256.Zero, arbosState.CurrentArbosVersion);

        // Insert a contract inside the world state
        Address contractAddress = new("0x0000000000000000000000000000000000000123");
        worldState.CreateAccount(contractAddress, 0);

        // Bytecode to return the l1 block number associated to the currently processed l2 block
        byte[] runtimeCode = Prepare.EvmCode
            .Op(Instruction.NUMBER)
            .PushData(0)
            .Op(Instruction.MSTORE) // stores l1 block number at memory offset 0
            .PushData(32)
            .PushData(0)
            .Op(Instruction.RETURN) // returns 32 bytes of data from memory offset 0
            .Op(Instruction.STOP)
            .Done;

        worldState.InsertCode(contractAddress, runtimeCode, fullChainSimulationSpecProvider.GenesisSpec);
        worldState.Commit(fullChainSimulationSpecProvider.GenesisSpec);

        // Just making sure contract got created
        ReadOnlySpan<byte> storageValue = worldState.Get(new StorageCell(contractAddress, index: 0));
        storageValue.IsZero().Should().BeTrue();

        Address sender = TestItem.AddressA;

        Transaction tx = Build.A.Transaction
            .WithTo(contractAddress)
            .WithValue(0)
            // .WithData() // no input data, tx will just execute bytecode from beginning
            .WithGasLimit(1_000_000)

            .WithType(TxType.EIP1559)
            .WithMaxFeePerGas(baseFeePerGas)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);

        // return arbosState.Blockhashes.GetL1BlockNumber() and not vm.BlockExecutionContext.Number
        UInt256 returnedBlockNumber = new(tracer.ReturnValue, isBigEndian: true);
        returnedBlockNumber.IsUint64.Should().BeTrue();
        returnedBlockNumber.ToUInt64(null).Should().Be(l1BlockNumber + 1); // blockHashes.RecordNewL1Block() adds + 1
        l2BlockNumber.Should().Be(blCtx.Number);
        returnedBlockNumber.ToUInt64(null).Should().NotBe(l2BlockNumber);
    }

        [Test]
    public void PrecompileExecution_GenericException_NonOwnerPrecompile_ConsumesGas()
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
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        Address sender = TestItem.AddressA;
        long gasLimit = 1_000_000;

        // Create transaction with invalid function selector
        byte[] callData = Bytes.FromHexString("deadbeef");

        Transaction tx = Build.A.Transaction
            .WithTo(ArbosAddresses.ArbSysAddress)
            .WithValue(0)
            .WithData(callData)
            .WithGasLimit(gasLimit)
            .WithGasPrice(1000000000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.EvmExceptionType.Should().Be(EvmExceptionType.PrecompileFailure);
    }

    [Test]
    public void PrecompileExecution_GenericException_OwnerPrecompile_RestoresGas()
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
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        // Set up the sender as a chain owner to bypass authorization checks
        Address sender = TestItem.AddressA;

        // Add sender as chain owner in the ArbOS state
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), NullLogger.Instance);
        arbosState.ChainOwners.Add(sender);

        long gasLimit = 100000;

        // Use setL1BaseFeeEstimateInertia(uint64) with malformed parameter data
        byte[] methodSelector = Bytes.FromHexString("5e8ef106");
        byte[] malformedData = Bytes.FromHexString("deadbeef"); // Invalid parameter data (not uint64)
        byte[] callData = methodSelector.Concat(malformedData).ToArray();

        Transaction tx = Build.A.Transaction
            .WithTo(ArbosAddresses.ArbOwnerAddress)
            .WithValue(0)
            .WithData(callData)
            .WithGasLimit(gasLimit)
            .WithGasPrice(1000000000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        // Exception should be handled gracefully by generic exception handler
        result.Should().Be(TransactionResult.Ok);
        result.EvmExceptionType.Should().Be(EvmExceptionType.PrecompileFailure);
    }

    [Test]
    public void PrecompileExecution_OutOfGas_NonOwnerPrecompile_ConsumesGas()
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
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        Address sender = TestItem.AddressA;

        // Use ArbAddressTable.size() with MASSIVE calldata to trigger huge gas burning cost
        byte[] functionSelector = Bytes.FromHexString("949d225d"); // size() function selector
        byte[] massiveData = new byte[50000]; // 50KB of data to create big dataGasCost
        byte[] callData = functionSelector.Concat(massiveData).ToArray();

        // Set gas limit to be just above intrinsic gas but far below dataGasCost
        long intrinsicGas = 21000 + (callData.Length * 4); // Minimum intrinsic gas
        long gasLimit = intrinsicGas + 1000; // Just above intrinsic, far below dataGasCost

        Transaction tx = Build.A.Transaction
            .WithTo(ArbosAddresses.ArbAddressTableAddress)
            .WithValue(0)
            .WithData(callData)
            .WithGasLimit(gasLimit)
            .WithGasPrice(1000000000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        // OutOfGasException should be handled gracefully
        result.Should().Be(TransactionResult.Ok);
        result.EvmExceptionType.Should().Be(EvmExceptionType.PrecompileFailure);
    }

    [Test]
    public void PrecompileExecution_OutOfGas_OwnerPrecompile_RestoresGas()
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
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        Address sender = TestItem.AddressA;

        // Use ArbOwner (owner precompile) with a method that might burn gas internally
        // Try getAllChainOwners() which might be expensive and burn gas
        byte[] functionSelector = Bytes.FromHexString("db5c7f0d"); // getAllChainOwners() function selector
        byte[] callData = functionSelector;

        // Set very low gas limit to trigger OutOfGasException during precompile execution
        // This should be just above intrinsic gas but insufficient for the precompile operation
        long intrinsicGas = 21000 + (callData.Length * 16);
        long gasLimit = intrinsicGas + 100; // Very tight gas limit

        Transaction tx = Build.A.Transaction
            .WithTo(ArbosAddresses.ArbOwnerAddress)
            .WithValue(0)
            .WithData(callData)
            .WithGasLimit(gasLimit)
            .WithGasPrice(1000000000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        // OutOfGasException in owner precompile should be handled gracefully
        result.Should().Be(TransactionResult.Ok);
        result.EvmExceptionType.Should().Be(EvmExceptionType.PrecompileFailure);
    }

    [Test]
    public void PrecompileExecution_UnauthorizedCallerException_ConsumesGas()
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
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        Address sender = Address.Zero;
        long gasLimit = 1_000_000;

        // Use ArbOwner method with proper calldata construction
        byte[] methodSelector = Bytes.FromHexString("5e8ef106"); // setL1BaseFeeEstimateInertia(uint64)
        byte[] parameter = new UInt256(1).ToBigEndian(); // uint64 parameter value
        byte[] calldata = methodSelector.Concat(parameter).ToArray();

        Transaction tx = Build.A.Transaction
            .WithTo(ArbosAddresses.ArbOwnerAddress)
            .WithValue(0)
            .WithData(calldata)
            .WithGasLimit(gasLimit)
            .WithGasPrice(1000000000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.EvmExceptionType.Should().Be(EvmExceptionType.PrecompileFailure);
    }

    [Test]
    public async Task CallingPrecompileWithValue_FunctionIsPayable_TransfersValue()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"))
            .Build();

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;

        UInt256 nonce;
        UInt256 initialSenderBalance;
        UInt256 initialPrecompileBalance;
        using (worldState.BeginScope(chain.BlockTree.Head!.Header))
        {
            nonce = worldState.GetNonce(FullChainSimulationAccounts.Owner.Address);
            initialSenderBalance = worldState.GetBalance(FullChainSimulationAccounts.Owner.Address);
            initialPrecompileBalance = worldState.GetBalance(ArbosAddresses.ArbSysAddress);
        }

        Address sender = FullChainSimulationAccounts.Owner.Address;
        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));

        // Calldata to call withdrawEth(address) on ArbSys precompile
        byte[] addressBytes = new byte[32];
        sender.Bytes.CopyTo(addressBytes, 12);
        byte[] calldata = [.. KeccakHash.ComputeHashBytes("withdrawEth(address)"u8)[..4], .. addressBytes];

        UInt256 value = 1_000;
        Transaction transaction = Build.A.Transaction
            .WithChainId(chain.ChainSpec.ChainId)
            .WithType(TxType.EIP1559)
            .WithTo(ArbosAddresses.ArbSysAddress)
            .WithData(calldata)
            .WithValue(value)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(1_000_000)
            .WithNonce(nonce)
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

        ResultWrapper<MessageResult> result = await chain.Digest(new TestL2Transactions(requestId, 92, sender, transaction));
        result.Result.Should().Be(Result.Success);

        TxReceipt[] receipts = chain.ReceiptStorage.Get(chain.BlockTree.Head!.Hash!);
        receipts.Should().HaveCount(2); // 2 transactions succeeded: internal, contract call
        receipts[0].StatusCode.Should().Be(StatusCode.Success);
        receipts[1].StatusCode.Should().Be(StatusCode.Success);

        using (worldState.BeginScope(chain.BlockTree.Head!.Header))
        {
            // Precompile received value but burnt it (balance stays the same but tx did not fail as method was payable)
            UInt256 finalPrecompileBalance = worldState.GetBalance(ArbosAddresses.ArbSysAddress);
            finalPrecompileBalance.Should().Be(initialPrecompileBalance);

            // Sender's balance got deducted as expected
            UInt256 finalSenderBalance = worldState.GetBalance(sender);

            // No need to take into account the gas used as the sender is the owner, who is also
            // the network fee account, which receives the network fee (gasUsed * effectiveGasPrice) during post processing.
            // Essentially, the full chain owner just gets reimbursed the eth used for tx execution.
            finalSenderBalance.Should().Be(initialSenderBalance - value);
        }
    }

    [Test]
    public async Task CallingPrecompileWithValue_FunctionIsNotPayable_RevertsAndConsumesGas()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"))
            .Build();

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;

        UInt256 nonce;
        UInt256 initialSenderBalance;
        UInt256 initialPrecompileBalance;
        using (worldState.BeginScope(chain.BlockTree.Head!.Header))
        {
            nonce = worldState.GetNonce(FullChainSimulationAccounts.Owner.Address);
            initialSenderBalance = worldState.GetBalance(FullChainSimulationAccounts.Owner.Address);
            initialPrecompileBalance = worldState.GetBalance(ArbosAddresses.ArbInfoAddress);
        }

        Address sender = FullChainSimulationAccounts.Owner.Address;
        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));

        // Calldata to call getBalance(address) on ArbInfo precompile
        byte[] addressBytes = new byte[32];
        sender.Bytes.CopyTo(addressBytes, 12);
        byte[] calldata = [.. KeccakHash.ComputeHashBytes("getBalance(address)"u8)[..4], .. addressBytes];

        UInt256 value = 1_000;
        long gasLimit = 1_000_000;
        Transaction transaction = Build.A.Transaction
            .WithChainId(chain.ChainSpec.ChainId)
            .WithType(TxType.EIP1559)
            .WithTo(ArbosAddresses.ArbInfoAddress)
            .WithData(calldata)
            .WithValue(value)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(gasLimit)
            .WithNonce(nonce)
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

        ResultWrapper<MessageResult> result = await chain.Digest(new TestL2Transactions(requestId, 92, sender, transaction));
        result.Result.ResultType.Should().Be(ResultType.Success); // Overall block creation succeeded even though the tx failed

        TxReceipt[] receipts = chain.ReceiptStorage.Get(chain.BlockTree.Head!.Hash!);
        receipts.Should().HaveCount(2); // 2 transactions succeeded: internal, contract call
        receipts[0].StatusCode.Should().Be(StatusCode.Success);
        receipts[1].StatusCode.Should().Be(StatusCode.Failure); // Tx failed

        receipts[1].GasUsedTotal.Should().Be(gasLimit); // Contract call consumed all gas

        using (worldState.BeginScope(chain.BlockTree.Head!.Header))
        {
            // Precompile did not receive value
            UInt256 finalPrecompileBalance = worldState.GetBalance(ArbosAddresses.ArbInfoAddress);
            finalPrecompileBalance.Should().Be(initialPrecompileBalance);

            // Sender's balance did not get deducted for tx's value
            UInt256 finalSenderBalance = worldState.GetBalance(sender);

            // We should deduct the gas used, but the sender being the owner is also
            // the network fee account, which receives the network fee (gasUsed * effectiveGasPrice) during post processing.
            // Essentially, the full chain owner just gets reimbursed the eth used for tx execution.
            //
            // What's interesting here is the value was not transferred as the tx reverted.
            finalSenderBalance.Should().Be(initialSenderBalance);
        }
    }

    [Test]
    public void CallingPrecompile_WholePrecompileIsNotActivated_TreatCallAsIfContractDoesNotExist()
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
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using IDisposable worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        ArbosState arbosState = ArbosState.OpenArbosState(
            worldState, new ZeroGasBurner(), _logManager.GetClassLogger<ArbosState>()
        );

        // Make it lower than ArbWasmParser.AvailableFromArbosVersion to test the specific test case
        arbosState.BackingStorage.Set(ArbosStateOffsets.VersionOffset, ArbWasmParser.AvailableFromArbosVersion - 1);

        Address sender = TestItem.AddressA;
        // Calldata to call inkPrice() on ArbWasm precompile
        byte[] calldata = KeccakHash.ComputeHashBytes("inkPrice()"u8)[..4];

        Transaction transaction = Build.A.Transaction
            .WithChainId(chain.ChainSpec.ChainId)
            .WithType(TxType.EIP1559)
            .WithTo(ArbWasmParser.Address)
            .WithData(calldata)
            .WithValue(0)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(1_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 initialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(transaction, tracer);

        result.Should().Be(TransactionResult.Ok);

        result.TransactionExecuted.Should().Be(true);
        result.EvmExceptionType.Should().Be(EvmExceptionType.None);

        ulong gasSpent = GasCostOf.Transaction + 64; // 64 gas units for intrinsic gas
        tracer.GasSpent.Should().Be((long)gasSpent); // Consumes 0 gas in EVM

        UInt256 finalBalance = worldState.GetBalance(sender);
        finalBalance.Should().Be(initialBalance - gasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void CallingPrecompile_FunctionIsNotActivated_RevertsAndConsumesGas()
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
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using IDisposable worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        ArbosState arbosState = ArbosState.OpenArbosState(
            worldState, new ZeroGasBurner(), _logManager.GetClassLogger<ArbosState>()
        );

        Address sender = TestItem.AddressA;
        // Calldata to call getL1PricingEquilibrationUnits() on ArbGasInfo precompile
        byte[] calldata = KeccakHash.ComputeHashBytes("getL1PricingEquilibrationUnits()"u8)[..4];

        // Make it lower than the arbos version of the function being called
        arbosState.BackingStorage.Set(ArbosStateOffsets.VersionOffset, ArbosVersion.Twenty - 1);

        long gasLimit = 1_000_000;
        Transaction transaction = Build.A.Transaction
            .WithChainId(chain.ChainSpec.ChainId)
            .WithType(TxType.EIP1559)
            .WithTo(ArbGasInfoParser.Address)
            .WithData(calldata)
            .WithValue(0)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(gasLimit)
            .WithNonce(worldState.GetNonce(sender))
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 initialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(transaction, tracer);

        result.TransactionExecuted.Should().Be(true);
        result.EvmExceptionType.Should().Be(EvmExceptionType.PrecompileFailure); // Reverts

        long gasSpent = gasLimit;
        tracer.GasSpent.Should().Be(gasSpent); // Consumes all gas in EVM

        UInt256 finalBalance = worldState.GetBalance(sender);
        finalBalance.Should().Be(initialBalance - (ulong)gasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void CallingPrecompile_FunctionDoesNotExist_RevertsAndConsumesGas()
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
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using IDisposable worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        Address sender = TestItem.AddressA;
        byte[] calldata = KeccakHash.ComputeHashBytes("someInexistingFunction()"u8)[..4];

        long gasLimit = 1_000_000;
        Transaction transaction = Build.A.Transaction
            .WithChainId(chain.ChainSpec.ChainId)
            .WithType(TxType.EIP1559)
            .WithTo(ArbInfoParser.Address)
            .WithData(calldata)
            .WithValue(0)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(gasLimit)
            .WithNonce(worldState.GetNonce(sender))
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 initialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(transaction, tracer);

        result.Should().Be(TransactionResult.Ok);

        result.TransactionExecuted.Should().Be(true);
        result.EvmExceptionType.Should().Be(EvmExceptionType.PrecompileFailure); // Reverts

        long gasSpent = gasLimit;
        tracer.GasSpent.Should().Be(gasSpent); // Consumes all gas in EVM

        UInt256 finalBalance = worldState.GetBalance(sender);
        finalBalance.Should().Be(initialBalance - (ulong)gasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void CallingPrecompile_ExecutingAccountNotActingAsPrecompileAndCallsViewFunction_RevertsAndConsumesGas()
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
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using IDisposable worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        // Insert a contract inside the world state
        Address contractAddress = new("0x0000000000000000000000000000000000000123");
        worldState.CreateAccount(contractAddress, 0);

        // Careful: methodSelector should be right-padded with 0s
        byte[] methodSelector = new byte[Hash256.Size];
        byte[] calldata = KeccakHash.ComputeHashBytes("getBalance(address)"u8)[..4];
        calldata.CopyTo(methodSelector, 0);

        // Careful: arguments should be left-padded with 0s
        byte[] addressWhoseBalanceToGet = new byte[Hash256.Size];
        Address sender = TestItem.AddressA;
        sender.Bytes.CopyTo(addressWhoseBalanceToGet, Hash256.Size - Address.Size);

        // Bytecode to Staticcall a precompile and returns the precompile output
        // if it was successful, otherwise reverts
        byte[] runtimeCode = Prepare.EvmCode
            // 1. Store input data in memory
            .PushData(methodSelector)
            .PushData(0)
            .Op(Instruction.MSTORE) // Stores the method selector in memory at offset 0

            .PushData(addressWhoseBalanceToGet)
            .PushData(4)            // Overwrite the right-padding of the address with the method argument
            .Op(Instruction.MSTORE) // Stores the method argument in memory at offset 4

            // 2. Prepare arguments and execute the DELEGATECALL
            .PushData(32)                 // retSize: we expect 32 bytes back (uint256)
            .PushData(0)                  // retOffset: where to store the return data in memory
            .PushData(36)                 // dataSize: input data size
            .PushData(0)                  // dataOffset: start of calldata in memory
            .PushData(ArbInfo.Address)    // address: precompile to call
            .Op(Instruction.GAS)          // gas: forward all remaining gas to delegate call
            .Op(Instruction.DELEGATECALL) // Delegatecall will pop all 6 arguments

            // 3. BRANCHING: Check the delegatecall result (1 or 0) and jump if successful
            .PushData(111)                // Code offset to jump to (JUMPDEST is at index 111 in bytecode)
            .Op(Instruction.JUMPI)        // Jumps if the result on the stack is 1 (success)

            // 4. FAILURE PATH: This code only runs if the JUMPI condition was false (precompile result was 0)
            .PushData(0)                  // retSize: 0 bytes
            .PushData(0)                  // retOffset: 0 bytes
            .Op(Instruction.REVERT)       // Revert with 0 bytes of data

            // 5. SUCCESS PATH: Return the result from the call.
            .Op(Instruction.JUMPDEST)     // Mark a valid jump destination
            // The balance is now in memory at offset 0, as specified by earlier retOffset.
            .PushData(32)                 // size: The size of the data to return (32 bytes).
            .PushData(0)                  // offset: The memory location of the data to return.
            .Op(Instruction.RETURN)
            .Done;

        worldState.InsertCode(contractAddress, runtimeCode, fullChainSimulationSpecProvider.GenesisSpec);
        worldState.Commit(fullChainSimulationSpecProvider.GenesisSpec);

        // Just making sure contract got created
        ReadOnlySpan<byte> storageValue = worldState.Get(new StorageCell(contractAddress, index: 0));
        storageValue.IsZero().Should().BeTrue();

        Transaction tx = Build.A.Transaction
            .WithTo(contractAddress)
            .WithValue(0)
            // .WithData() // no input data, tx will just execute bytecode from beginning
            .WithGasLimit(1_000_000)

            .WithType(TxType.EIP1559)
            .WithMaxFeePerGas(baseFeePerGas)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 initialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.TransactionExecuted.Should().Be(true);
        result.EvmExceptionType.Should().Be(EvmExceptionType.Revert);

        // A bit of a magic number but what is interesting is almost all of the tx's gas limit was burned
        // as precompile failed, except for some gas left over due to the 63/64 rule before the delegatecall
        // not all being burnt after the delegatecall.
        long gasSpent = 984_725;
        tracer.GasSpent.Should().Be(gasSpent);

        UInt256 finalBalance = worldState.GetBalance(sender);
        finalBalance.Should().Be(initialBalance - (ulong)gasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void CallingPrecompile_ReadOnlyFrameCallingNonPayableFunction_RevertsAndConsumesGas()
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
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using IDisposable worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        // Insert a contract inside the world state
        Address contractAddress = new("0x0000000000000000000000000000000000000123");
        worldState.CreateAccount(contractAddress, 0);

        // Careful: methodSelector should be right-padded with 0s
        byte[] methodSelector = new byte[Hash256.Size];
        byte[] calldata = KeccakHash.ComputeHashBytes("register(address)"u8)[..4];
        calldata.CopyTo(methodSelector, 0);

        // Careful: arguments should be left-padded with 0s
        byte[] addressToRegister = new byte[Hash256.Size];
        Address sender = TestItem.AddressA;
        sender.Bytes.CopyTo(addressToRegister, Hash256.Size - Address.Size);

        // Bytecode to staticcall a precompile and returns the precompile output
        // if it was successful, otherwise reverts
        byte[] runtimeCode = Prepare.EvmCode
            // 1. Store input data in memory
            .PushData(methodSelector)
            .PushData(0)
            .Op(Instruction.MSTORE) // Stores the method selector in memory at offset 0

            .PushData(addressToRegister)
            .PushData(4)            // Overwrite the right-padding of the address with the method argument
            .Op(Instruction.MSTORE) // Stores the method argument in memory at offset 4

            // 2. Prepare arguments and execute the STATICCALL
            .PushData(32)                       // retSize: we expect 32 bytes back (uint256)
            .PushData(0)                        // retOffset: where to store the return data in memory
            .PushData(36)                       // dataSize: input data size
            .PushData(0)                        // dataOffset: start of calldata in memory
            .PushData(ArbAddressTable.Address)  // address: precompile to call
            .Op(Instruction.GAS)                // gas: forward all remaining gas to staticcall
            .Op(Instruction.STATICCALL)         // Staticcall will pop all 6 arguments

            // 3. BRANCHING: Check the staticcall result (1 or 0) and jump if successful
            .PushData(111)                // Code offset to jump to (JUMPDEST is at index 111 in bytecode)
            .Op(Instruction.JUMPI)        // Jumps if the result on the stack is 1 (success)

            // 4. FAILURE PATH: This code only runs if the JUMPI condition was false (precompile result was 0)
            .PushData(0)                  // retSize: 0 bytes
            .PushData(0)                  // retOffset: 0 bytes
            .Op(Instruction.REVERT)       // Revert with 0 bytes of data

            // 5. SUCCESS PATH: Return the result from the call.
            .Op(Instruction.JUMPDEST)     // Mark a valid jump destination
            // The balance is now in memory at offset 0, as specified by earlier retOffset.
            .PushData(32)                 // size: The size of the data to return (32 bytes).
            .PushData(0)                  // offset: The memory location of the data to return.
            .Op(Instruction.RETURN)
            .Done;

        worldState.InsertCode(contractAddress, runtimeCode, fullChainSimulationSpecProvider.GenesisSpec);
        worldState.Commit(fullChainSimulationSpecProvider.GenesisSpec);

        // Just making sure contract got created
        ReadOnlySpan<byte> storageValue = worldState.Get(new StorageCell(contractAddress, index: 0));
        storageValue.IsZero().Should().BeTrue();

        Transaction tx = Build.A.Transaction
            .WithTo(contractAddress)
            .WithValue(0)
            // .WithData() // no input data, tx will just execute bytecode from beginning
            .WithGasLimit(1_000_000)

            .WithType(TxType.EIP1559)
            .WithMaxFeePerGas(baseFeePerGas)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 initialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.TransactionExecuted.Should().Be(true);
        result.EvmExceptionType.Should().Be(EvmExceptionType.Revert);

        // A bit of a magic number but what is interesting is almost all of the tx's gas limit was burned
        // as precompile failed, except for some gas left over due to the 63/64 rule before the staticcall
        // not all being burnt after the staticcall.
        long gasSpent = 984_725;
        tracer.GasSpent.Should().Be(gasSpent);

        UInt256 finalBalance = worldState.GetBalance(sender);
        finalBalance.Should().Be(initialBalance - (ulong)gasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }
}
