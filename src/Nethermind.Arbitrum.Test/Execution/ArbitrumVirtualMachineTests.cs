using System.Buffers.Binary;
using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain.Tracing;
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
    public void BlockHashOpcode_WhenL1BlockRecorded_ReturnsExpectedHash()
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

        ArbosState arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), NullLogger.Instance);

        ulong currentL1BlockNumber = 300;
        ValueHash256 expectedHash = ValueKeccak.Compute("block256");

        // Record block 256 with a known hash
        arbosState.Blockhashes.RecordNewL1Block(256, new ValueHash256(expectedHash.Bytes), ArbosVersion.Forty);
        arbosState.Blockhashes.RecordNewL1Block(currentL1BlockNumber, ValueKeccak.Compute("block300"), ArbosVersion.Forty);

        ulong testBlockNumber = 256;

        // Build runtime EVM code
        byte[] runtimeCode = Prepare.EvmCode
            .PushData(new UInt256(testBlockNumber).ToBigEndian())
            .Op(Instruction.BLOCKHASH)
            .PushData(0)
            .Op(Instruction.MSTORE)
            .PushData(32)
            .PushData(0)
            .Op(Instruction.RETURN)
            .Done;

        Address contractAddress = new("0x0000000000000000000000000000000000000123");
        worldState.CreateAccount(contractAddress, 0);
        worldState.InsertCode(contractAddress, runtimeCode, fullChainSimulationSpecProvider.GenesisSpec);
        worldState.Commit(fullChainSimulationSpecProvider.GenesisSpec);

        Address sender = TestItem.AddressA;
        Transaction tx = Build.A.Transaction
            .WithTo(contractAddress)
            .WithValue(0)
            .WithGasLimit(1_000_000)
            .WithMaxFeePerGas(1_000_000_000)
            .WithMaxPriorityFeePerGas(100_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        TestAllTracerWithOutput tracer = new();

        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        tracer.ReturnValue.Should().Equal(expectedHash.Bytes.ToArray());
    }

    [Test]
    public void InstructionBlockHash_ReturnsZero_WhenBlockNumberTooOld()
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

        ArbosState arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), NullLogger.Instance);

        ulong currentL1BlockNumber = 300;
        arbosState.Blockhashes.RecordNewL1Block(currentL1BlockNumber, ValueKeccak.Compute("block300"), ArbosVersion.Forty);

        ulong testBlockNumber = 43; // Too old (< 44)

        byte[] runtimeCode = Prepare.EvmCode
            .PushData(new UInt256(testBlockNumber).ToBigEndian())
            .Op(Instruction.BLOCKHASH)
            .PushData(0)
            .Op(Instruction.MSTORE)
            .PushData(32)
            .PushData(0)
            .Op(Instruction.RETURN)
            .Done;

        Address contractAddress = new("0x0000000000000000000000000000000000000123");
        worldState.CreateAccount(contractAddress, 0);
        worldState.InsertCode(contractAddress, runtimeCode, fullChainSimulationSpecProvider.GenesisSpec);
        worldState.Commit(fullChainSimulationSpecProvider.GenesisSpec);

        Address sender = TestItem.AddressA;
        Transaction tx = Build.A.Transaction
            .WithTo(contractAddress)
            .WithValue(0)
            .WithGasLimit(1_000_000)
            .WithMaxFeePerGas(1_000_000_000)
            .WithMaxPriorityFeePerGas(100_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        TestAllTracerWithOutput tracer = new();

        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        tracer.ReturnValue.Should().Equal(new byte[32]);
    }

    [Test]
    public void InstructionBlockHash_ReturnsZeroHash_WhenBlockNumberIsInRangeButNoHashStored()
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

        ArbosState arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), NullLogger.Instance);

        ulong currentL1BlockNumber = 300;

        // First, ensure slot for block 256 is cleared
        // Block 256 maps to slot 1 (256 % 256 = 0, plus offset of 1)
        // Record a block that will use the same slot with zero hash to clear it
        arbosState.Blockhashes.RecordNewL1Block(512, new ValueHash256(new byte[32]), ArbosVersion.Forty);

        // Now record the current block number
        arbosState.Blockhashes.RecordNewL1Block(currentL1BlockNumber, ValueKeccak.Compute("block300"), ArbosVersion.Forty);

        // Record some other blocks in range, but NOT block 256
        arbosState.Blockhashes.RecordNewL1Block(299, ValueKeccak.Compute("block299"), ArbosVersion.Forty);
        arbosState.Blockhashes.RecordNewL1Block(257, ValueKeccak.Compute("block257"), ArbosVersion.Forty);
        arbosState.Blockhashes.RecordNewL1Block(255, ValueKeccak.Compute("block255"), ArbosVersion.Forty);

        ulong testBlockNumber = 256;  // Within [44..300), but no valid hash stored

        // Build runtime EVM code
        byte[] runtimeCode = Prepare.EvmCode
            .PushData(new UInt256(testBlockNumber).ToBigEndian())
            .Op(Instruction.BLOCKHASH)
            .PushData(0)
            .Op(Instruction.MSTORE)
            .PushData(32)
            .PushData(0)
            .Op(Instruction.RETURN)
            .Done;

        // Create contract account
        Address contractAddress = new("0x0000000000000000000000000000000000000123");
        worldState.CreateAccount(contractAddress, 0);
        worldState.InsertCode(contractAddress, runtimeCode, fullChainSimulationSpecProvider.GenesisSpec);
        worldState.Commit(fullChainSimulationSpecProvider.GenesisSpec);

        // Prepare transaction
        Address sender = TestItem.AddressA;
        Transaction tx = Build.A.Transaction
            .WithTo(contractAddress)
            .WithValue(0)
            .WithGasLimit(1_000_000)
            .WithMaxFeePerGas(1_000_000_000)
            .WithMaxPriorityFeePerGas(100_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        TestAllTracerWithOutput tracer = new();

        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        byte[] expectedZeroHash = new byte[32];
        tracer.ReturnValue.Should().Equal(expectedZeroHash);
    }

    [Test]
    public void InstructionBlockHash_UsesL1BlockNumber_FromArbosState()
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

        // Open ArbOS state with the existing world state
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), NullLogger.Instance);

        // L1 is only at block 150 while L2 is at a higher number
        ulong l1BlockNumber = 150;
        ValueHash256 l1Block149Hash = ValueKeccak.Compute("L1_block_149_hash");

        arbosState.Blockhashes.RecordNewL1Block(149, new ValueHash256(l1Block149Hash.Bytes), ArbosVersion.Forty);
        arbosState.Blockhashes.RecordNewL1Block(l1BlockNumber, ValueKeccak.Compute("L1_block_150"), ArbosVersion.Forty);

        // Build code that queries for block 149
        byte[] runtimeCode = Prepare.EvmCode
            .PushData(new UInt256(149).ToBigEndian())
            .Op(Instruction.BLOCKHASH)
            .PushData(0)
            .Op(Instruction.MSTORE)
            .PushData(32)
            .PushData(0)
            .Op(Instruction.RETURN)
            .Done;

        Address contractAddress = new("0x0000000000000000000000000000000000000126");
        worldState.CreateAccount(contractAddress, 0);
        worldState.InsertCode(contractAddress, runtimeCode, fullChainSimulationSpecProvider.GenesisSpec);

        // Ensure sender has balance
        Address sender = TestItem.AddressA;
        worldState.CreateAccount(sender, 1.Ether());
        worldState.Commit(fullChainSimulationSpecProvider.GenesisSpec);

        Transaction tx = Build.A.Transaction
            .WithTo(contractAddress)
            .WithValue(0)
            .WithGasLimit(1_000_000)
            .WithMaxFeePerGas(1_000_000_000)
            .WithMaxPriorityFeePerGas(100_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        TestAllTracerWithOutput tracer = new();

        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);

        // Should return the L1 hash, proving it's using L1 block numbers not L2
        tracer.ReturnValue.Should().Equal(l1Block149Hash.Bytes.ToArray());

        // Update nonce for next transaction
        worldState.IncrementNonce(sender);
        worldState.Commit(fullChainSimulationSpecProvider.GenesisSpec);

        // Additional verification: querying for a high block number should fail
        // since L1 is only at block 150
        byte[] runtimeCode2 = Prepare.EvmCode
            .PushData(new UInt256(9999).ToBigEndian())
            .Op(Instruction.BLOCKHASH)
            .PushData(0)
            .Op(Instruction.MSTORE)
            .PushData(32)
            .PushData(0)
            .Op(Instruction.RETURN)
            .Done;

        Address contractAddress2 = new("0x0000000000000000000000000000000000000127");
        worldState.CreateAccount(contractAddress2, 0);
        worldState.InsertCode(contractAddress2, runtimeCode2, fullChainSimulationSpecProvider.GenesisSpec);
        worldState.Commit(fullChainSimulationSpecProvider.GenesisSpec);

        Transaction tx2 = Build.A.Transaction
            .WithTo(contractAddress2)
            .WithValue(0)
            .WithGasLimit(1_000_000)
            .WithMaxFeePerGas(1_000_000_000)
            .WithMaxPriorityFeePerGas(100_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        TestAllTracerWithOutput tracer2 = new();
        TransactionResult result2 = chain.TxProcessor.Execute(tx2, tracer2);

        // Should return zero because L1 hasn't reached block 9999
        result2.Should().Be(TransactionResult.Ok);
        tracer2.ReturnValue.Should().Equal(new byte[32]);
    }

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
    public void CallingOwnerPrecompile_CallerIsOwner_RestoresGasSuppliedAndEmitsSuccessLog()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        ulong baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using IDisposable worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        Address sender = TestItem.AddressA;

        // Add sender as chain owner in the ArbOS state
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), NullLogger.Instance);
        arbosState.ChainOwners.Add(sender);

        byte[] addChainOwnerMethodId = Keccak.Compute("addChainOwner(address)").Bytes[..4].ToArray();
        Address newOwner = new("0x0000000000000000000000000000000000000123");

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            ArbOwnerParser.PrecompileFunctions[BinaryPrimitives.ReadUInt32BigEndian(addChainOwnerMethodId)].AbiFunctionDescription.GetCallInfo().Signature,
            [newOwner]
        );

        long intrinsicGas = GasCostOf.Transaction + 216;

        Transaction tx = Build.A.Transaction
            .WithTo(ArbosAddresses.ArbOwnerAddress)
            .WithValue(0)
            .WithData(calldata)
            .WithGasLimit(1_000_000)
            .WithGasPrice(1_000_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 senderInitialBalance = worldState.GetBalance(sender);

        BlockReceiptsTracer tracer = new();
        tracer.StartNewBlockTrace(chain.BlockTree.Head);
        tracer.StartNewTxTrace(tx);
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);
        tracer.EndTxTrace();
        tracer.EndBlockTrace();

        result.Should().Be(TransactionResult.Ok);
        result.EvmExceptionType.Should().Be(EvmExceptionType.None);

        tracer.TxReceipts.Should().HaveCount(1);
        TxReceipt receipt = tracer.TxReceipts[0];
        receipt.StatusCode.Should().Be(StatusCode.Success);

        // As tx succeeds in owner-only precompile, a success log is emitted
        LogEntry ownerActsEvent = EventsEncoder.BuildLogEntryFromEvent(
            ArbOwner.OwnerActsEvent, ArbOwner.Address, addChainOwnerMethodId, sender, calldata
        );
        receipt.Logs.Should().BeEquivalentTo(new[] { ownerActsEvent });

        // Only intrinsic gas is spent (restore gas supplied as owner-only precompile)
        long expectedGasSpent = intrinsicGas;
        receipt.GasUsed.Should().Be(expectedGasSpent);

        UInt256 senderFinalBalance = worldState.GetBalance(sender);
        senderFinalBalance.Should().Be(senderInitialBalance - (ulong)expectedGasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas

        // Address has been added a chain owner
        arbosState.ChainOwners.IsMember(newOwner).Should().BeTrue();
    }

    [Test]
    public void CallingOwnerPrecompile_OutOfGasDuringIsChainOwnerCheckAsOwner_FailsAndConsumesAllGas()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        ulong baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using IDisposable worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        Address sender = TestItem.AddressA;

        // Add sender as chain owner in the ArbOS state
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), NullLogger.Instance);
        arbosState.ChainOwners.Add(sender);

        // Call getAllChainOwners() on ArbOwner (owner-only precompile)
        byte[] callData = Keccak.Compute("getAllChainOwners()").Bytes[..4].ToArray();

        // Set gas limit to run out of gas after opening arbos state when
        // checking if sender is a chain owner before precompile execution
        long intrinsicGas = GasCostOf.Transaction + 64;
        long gasLimit = intrinsicGas + (long)ArbosStorage.StorageReadCost + 100;

        Transaction tx = Build.A.Transaction
            .WithTo(ArbosAddresses.ArbOwnerAddress)
            .WithValue(0)
            .WithData(callData)
            .WithGasLimit(gasLimit)
            .WithGasPrice(1_000_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 senderInitialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.EvmExceptionType.Should().Be(EvmExceptionType.PrecompileFailure); // Fails

        tracer.ReturnValue.Should().BeEmpty();

        // Does not restore gas supplied even if the sender was the owner
        // And anyway, as it does not revert but fails instead so no refund
        long expectedGasSpent = gasLimit;
        tracer.GasSpent.Should().Be(expectedGasSpent);

        UInt256 senderFinalBalance = worldState.GetBalance(sender);
        senderFinalBalance.Should().Be(senderInitialBalance - (ulong)expectedGasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void CallingOwnerPrecompile_CallerIsNotAnOwner_FailsAndConsumesAllGas()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        ulong baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using IDisposable worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        Address sender = TestItem.AddressA;
        long gasLimit = 1_000_000;

        // Try to call ArbOwner precompile
        byte[] calldata = [.. Keccak.Compute("setL1BaseFeeEstimateInertia(uint64)").Bytes[..4], .. new byte[32]];

        Transaction tx = Build.A.Transaction
            .WithTo(ArbosAddresses.ArbOwnerAddress)
            .WithValue(0)
            .WithData(calldata)
            .WithGasLimit(gasLimit)
            .WithGasPrice(1_000_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 senderInitialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.EvmExceptionType.Should().Be(EvmExceptionType.PrecompileFailure);

        long gasSpent = gasLimit; // Consumes all gas in EVM (no refund as failed without reverting)
        tracer.GasSpent.Should().Be(gasSpent);

        tracer.ReturnValue.Should().BeEmpty();

        UInt256 senderFinalBalance = worldState.GetBalance(sender);
        senderFinalBalance.Should().Be(senderInitialBalance - (ulong)gasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void CallingOwnerPrecompile_NotEnoughGasToPayForInputDataCost_RevertsAndRestoresGasSupplied()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        ulong baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using IDisposable worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        Address sender = TestItem.AddressA;

        // Add sender as chain owner in the ArbOS state
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), NullLogger.Instance);
        arbosState.ChainOwners.Add(sender);

        // Use ArbOwner.getInfraFeeAccount() with MASSIVE calldata to trigger huge gas burning cost
        // 50KB of data to create big input data cost even if the method does not take any input
        byte[] massiveData = new byte[50_000];
        byte[] callData = [.. Keccak.Compute("getInfraFeeAccount()").Bytes[..4], .. massiveData];

        // Set gas limit to be just above intrinsic gas but below input data cost
        long intrinsicGas = GasCostOf.Transaction + 200_064;
        long ownerOnlyChecks = 2 * (long)ArbosStorage.StorageReadCost;
        long gasLimit = intrinsicGas + ownerOnlyChecks + 100; // Just above intrinsic + owner only checks, below input data cost (4689)

        Transaction tx = Build.A.Transaction
            .WithTo(ArbosAddresses.ArbOwnerAddress)
            .WithValue(0)
            .WithData(callData)
            .WithGasLimit(gasLimit)
            .WithGasPrice(1_000_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 senderInitialBalance = worldState.GetBalance(sender);

        BlockReceiptsTracer tracer = new();
        tracer.StartNewBlockTrace(chain.BlockTree.Head);
        tracer.StartNewTxTrace(tx);
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);
        tracer.EndTxTrace();
        tracer.EndBlockTrace();

        result.Should().Be(TransactionResult.Ok);
        result.EvmExceptionType.Should().Be(EvmExceptionType.Revert);

        tracer.TxReceipts.Should().HaveCount(1);
        TxReceipt receipt = tracer.TxReceipts[0];
        receipt.StatusCode.Should().Be(StatusCode.Failure);

        receipt.Logs.Should().HaveCount(0); // As tx failed, no success log is emitted

        // Only intrinsic gas is spent (the owner only checks are paid only if they fail or if sender is not an owner)
        long expectedGasSpent = intrinsicGas;
        receipt.GasUsed.Should().Be(expectedGasSpent);

        UInt256 senderFinalBalance = worldState.GetBalance(sender);
        senderFinalBalance.Should().Be(senderInitialBalance - (ulong)expectedGasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void CallingNonOwnerPrecompile_NotEnoughGasToPayForInputDataCost_RevertsAndConsumesAllGas()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        ulong baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using IDisposable worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        Address sender = TestItem.AddressA;

        // Use ArbAddressTable.size() with MASSIVE calldata to trigger huge gas burning cost
        // 50KB of data to create big input data cost even if size() does not take any input
        byte[] massiveData = new byte[50_000];
        byte[] callData = [.. Keccak.Compute("size()").Bytes[..4], .. massiveData];

        // Set gas limit to be just above intrinsic gas but below input data cost
        long intrinsicGas = GasCostOf.Transaction + 200_064;
        long gasLimit = intrinsicGas + 1000; // Just above intrinsic, below input data cost (4689)

        Transaction tx = Build.A.Transaction
            .WithTo(ArbosAddresses.ArbAddressTableAddress)
            .WithValue(0)
            .WithData(callData)
            .WithGasLimit(gasLimit)
            .WithGasPrice(1_000_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 senderInitialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.EvmExceptionType.Should().Be(EvmExceptionType.Revert);

        tracer.GasSpent.Should().Be(gasLimit); // User cannot afford argument data supplied
        tracer.ReturnValue.Should().BeEmpty();

        UInt256 senderFinalBalance = worldState.GetBalance(sender);
        senderFinalBalance.Should().Be(senderInitialBalance - (ulong)gasLimit * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void CallingNonOwnerPrecompile_OutOfGasWhenOpeningArbosState_FailsAndConsumesAllGas()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        ulong baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using IDisposable worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        Address sender = TestItem.AddressA;

        byte[] callData = Keccak.Compute("getL1BaseFeeEstimate()").Bytes[..4].ToArray();

        // Set gas limit to run out of gas after opening arbos state
        long intrinsicGas = GasCostOf.Transaction + 64;
        long gasLimit = intrinsicGas;

        Transaction tx = Build.A.Transaction
            .WithTo(ArbosAddresses.ArbGasInfoAddress)
            .WithValue(0)
            .WithData(callData)
            .WithGasLimit(gasLimit)
            .WithGasPrice(1_000_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 senderInitialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.EvmExceptionType.Should().Be(EvmExceptionType.PrecompileFailure); // Fails

        tracer.ReturnValue.Should().BeEmpty();

        long expectedGasSpent = gasLimit; // Runs out of gas, consumes everything
        tracer.GasSpent.Should().Be(expectedGasSpent);

        UInt256 senderFinalBalance = worldState.GetBalance(sender);
        senderFinalBalance.Should().Be(senderInitialBalance - (ulong)expectedGasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void CallingOwnerPrecompile_ErrorWhenDecodingCalldata_RevertsAndRestoresGasSupplied()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        ulong baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using IDisposable worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        // Set up the sender as a chain owner to bypass authorization checks
        Address sender = TestItem.AddressA;

        // Add sender as chain owner in the ArbOS state
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), NullLogger.Instance);
        arbosState.ChainOwners.Add(sender);

        // Calldata is too small, expects a static (32 bytes) argument
        // Will throw a revert exception when decoding calldata
        byte[] malformedData = new byte[31];
        byte[] callData = [.. Keccak.Compute("setL1BaseFeeEstimateInertia(uint64)").Bytes[..4], .. malformedData];

        Transaction tx = Build.A.Transaction
            .WithTo(ArbosAddresses.ArbOwnerAddress)
            .WithValue(0)
            .WithData(callData)
            .WithGasLimit(100_000)
            .WithGasPrice(1_000_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 senderInitialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.EvmExceptionType.Should().Be(EvmExceptionType.Revert);

        tracer.ReturnValue.Should().BeEmpty();

        long expectedGasSpent = GasCostOf.Transaction + 188; // intrinsic gas only
        tracer.GasSpent.Should().Be(expectedGasSpent);

        UInt256 senderFinalBalance = worldState.GetBalance(sender);
        senderFinalBalance.Should().Be(senderInitialBalance - (ulong)expectedGasSpent * baseFeePerGas);
    }

    [Test]
    public void CallingNonOwnerPrecompile_ErrorWhenDecodingCalldata_RevertsButConsumesAllGas()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        ulong baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using IDisposable worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        // Calldata is too small, expects a static (32 bytes) argument
        // Will throw a revert exception when decoding calldata
        byte[] malformedAddress = new byte[31];
        byte[] callData = [.. Keccak.Compute("getBalance(address)").Bytes[..4], .. malformedAddress];

        Address sender = TestItem.AddressA;
        long gasLimit = 100_000;
        Transaction tx = Build.A.Transaction
            .WithTo(ArbosAddresses.ArbInfoAddress)
            .WithValue(0)
            .WithData(callData)
            .WithGasLimit(gasLimit)
            .WithGasPrice(1_000_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 senderInitialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.EvmExceptionType.Should().Be(EvmExceptionType.Revert);

        tracer.ReturnValue.Should().BeEmpty();

        long expectedGasSpent = gasLimit; // consumes all gas
        tracer.GasSpent.Should().Be(expectedGasSpent);

        UInt256 senderFinalBalance = worldState.GetBalance(sender);
        senderFinalBalance.Should().Be(senderInitialBalance - (ulong)expectedGasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void CallingNonOwnerPrecompile_RunsOutOfGasWhenPayingForRegularOutput_RevertsAndReturnsNoOutput()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        ulong baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using IDisposable worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        byte[] calldata = Keccak.Compute("getL1FeesAvailable()").Bytes[..4].ToArray();

        Address sender = TestItem.AddressA;
        long intrinsicGas = GasCostOf.Transaction + 64;
        // just enough for the function execution and method execution (will run out of gas when paying for the 32-byte output)
        long gasLimit = intrinsicGas + 2 * (long)ArbosStorage.StorageReadCost;

        Transaction tx = Build.A.Transaction
            .WithTo(ArbosAddresses.ArbGasInfoAddress)
            .WithValue(0)
            .WithData(calldata)
            .WithGasLimit(gasLimit)
            .WithGasPrice(1_000_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 senderInitialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.EvmExceptionType.Should().Be(EvmExceptionType.OutOfGas); // Reverts with out-of-gas exception

        long gasSpent = gasLimit; // Consumes all gas (runs out of gas)
        tracer.GasSpent.Should().Be(gasSpent);

        tracer.ReturnValue.Should().BeEmpty();

        UInt256 senderFinalBalance = worldState.GetBalance(sender);
        senderFinalBalance.Should().Be(senderInitialBalance - (ulong)gasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void CallingNonOwnerPrecompile_RunsOutOfGasWhenPayingForSolidityErrorOutput_RevertsAndReturnsNoOutput()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        ulong baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using IDisposable worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        uint getArbBlockNumberMethodId = PrecompileHelper.GetMethodId("arbBlockHash(uint256)");
        UInt256 arbBlockNum = ulong.MaxValue + UInt256.One; // bigger than uint64 max to trigger the solidity error
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            ArbSysParser.PrecompileFunctions[getArbBlockNumberMethodId].AbiFunctionDescription.GetCallInfo().Signature,
            [arbBlockNum]
        );

        Address sender = TestItem.AddressA;

        long intrinsicGas = GasCostOf.Transaction + 204;

        ulong inputDataCost = GasCostOf.DataCopy * Math.Utils.Div32Ceiling((ulong)calldata.Length - 4);
        PrecompileSolidityError expectedSolidityError = ArbSys.InvalidBlockNumberSolidityError(arbBlockNum, blCtx.Number);
        ulong solidityErrorCost = GasCostOf.DataCopy * Math.Utils.Div32Ceiling((ulong)expectedSolidityError.ErrorData.Length);
        // input data cost + opening arbos + error data cost
        ulong precompileExec = inputDataCost + ArbosStorage.StorageReadCost + solidityErrorCost;

        // Make the gas limit just below the needed gas to run out of gas when paying for solidity error output
        long gasLimit = intrinsicGas + (long)precompileExec - 1;

        Transaction tx = Build.A.Transaction
            .WithTo(ArbosAddresses.ArbSysAddress)
            .WithValue(0)
            .WithData(calldata)
            .WithGasLimit(gasLimit)
            .WithGasPrice(1_000_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 senderInitialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.EvmExceptionType.Should().Be(EvmExceptionType.OutOfGas); // Reverts with out-of-gas exception

        long gasSpent = gasLimit;
        tracer.GasSpent.Should().Be(gasSpent);

        tracer.ReturnValue.Should().BeEmpty(); // Revert with no output

        UInt256 senderFinalBalance = worldState.GetBalance(sender);
        senderFinalBalance.Should().Be(senderInitialBalance - (ulong)gasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void CallingNonOwnerPrecompile_SolidityError_RevertsAndReturnsSolidityErrorOutput()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        ulong baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using IDisposable worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        uint getArbBlockNumberMethodId = PrecompileHelper.GetMethodId("arbBlockHash(uint256)");
        UInt256 arbBlockNum = ulong.MaxValue + UInt256.One; // bigger than uint64 max to trigger the solidity error
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            ArbSysParser.PrecompileFunctions[getArbBlockNumberMethodId].AbiFunctionDescription.GetCallInfo().Signature,
            [arbBlockNum]
        );

        Address sender = TestItem.AddressA;
        long gasLimit = 1_000_000;

        Transaction tx = Build.A.Transaction
            .WithTo(ArbosAddresses.ArbSysAddress)
            .WithValue(0)
            .WithData(calldata)
            .WithGasLimit(gasLimit)
            .WithGasPrice(1_000_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 senderInitialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.EvmExceptionType.Should().Be(EvmExceptionType.Revert);

        long intrinsicGas = GasCostOf.Transaction + 204;

        ulong inputDataCost = GasCostOf.DataCopy * Math.Utils.Div32Ceiling((ulong)calldata.Length - 4);
        PrecompileSolidityError expectedSolidityError = ArbSys.InvalidBlockNumberSolidityError(arbBlockNum, blCtx.Number);
        ulong solidityErrorCost = GasCostOf.DataCopy * Math.Utils.Div32Ceiling((ulong)expectedSolidityError.ErrorData.Length);
        // input data cost + opening arbos + error data cost
        ulong precompileExec = inputDataCost + ArbosStorage.StorageReadCost + solidityErrorCost;

        long gasSpent = intrinsicGas + (long)precompileExec;
        tracer.GasSpent.Should().Be(gasSpent);

        // Revert returns output data
        tracer.ReturnValue.Should().BeEquivalentTo(expectedSolidityError.ErrorData);

        UInt256 senderFinalBalance = worldState.GetBalance(sender);
        senderFinalBalance.Should().Be(senderInitialBalance - (ulong)gasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void PrecompileExecution_ProgramActivationError_FailsAndConsumesAllGas()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        ulong baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using IDisposable worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        Address sender = TestItem.AddressA;

        uint activateProgramMethodId = PrecompileHelper.GetMethodId("activateProgram(address)");
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            ArbWasmParser.PrecompileFunctions[activateProgramMethodId].AbiFunctionDescription.GetCallInfo().Signature,
            [TestItem.AddressB] // give some non existing program address
        );

        long gasLimit = 2_000_000; // higher than high activation fixed cost
        Transaction tx = Build.A.Transaction
            .WithTo(ArbosAddresses.ArbWasmAddress)
            .WithValue(0)
            .WithData(calldata)
            .WithGasLimit(gasLimit)
            .WithGasPrice(1_000_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 senderInitialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.EvmExceptionType.Should().Be(EvmExceptionType.PrecompileFailure); // Fails

        tracer.ReturnValue.Should().BeEmpty();

        long expectedGasSpent = gasLimit; // Runs out of gas, consumes everything
        tracer.GasSpent.Should().Be(expectedGasSpent);

        UInt256 senderFinalBalance = worldState.GetBalance(sender);
        senderFinalBalance.Should().Be(senderInitialBalance - (ulong)expectedGasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void PrecompileExecution_ThrowsExceptionButArbosVersionGreaterOrEqualToEleven_RevertsAndRefundsGasLeft()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        ulong baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using IDisposable worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        uint sendMerkleTreeStateMethodId = PrecompileHelper.GetMethodId("sendMerkleTreeState()");
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            ArbSysParser.PrecompileFunctions[sendMerkleTreeStateMethodId].AbiFunctionDescription.GetCallInfo().Signature,
            []
        );

        Address sender = TestItem.AddressA;
        long gasLimit = 1_000_000;
        Transaction tx = Build.A.Transaction
            .WithTo(ArbosAddresses.ArbSysAddress)
            .WithValue(0)
            .WithData(calldata)
            .WithGasLimit(gasLimit)
            .WithGasPrice(1_000_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 senderInitialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.EvmExceptionType.Should().Be(EvmExceptionType.Revert);

        tracer.ReturnValue.Should().BeEmpty();

        long intrinsicGas = GasCostOf.Transaction + 64;
        long precompileExecGas = (long)ArbosStorage.StorageReadCost; // opening arbos
        long expectedGasSpent = intrinsicGas + precompileExecGas; // Got refunded the rest
        tracer.GasSpent.Should().Be(expectedGasSpent);

        UInt256 senderFinalBalance = worldState.GetBalance(sender);
        senderFinalBalance.Should().Be(senderInitialBalance - (ulong)expectedGasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void PrecompileExecution_ThrowsExceptionAndArbosVersionLowerThanEleven_FailsAndConsumesAllGas()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        ulong baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using IDisposable worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        // Make the arbos version lower than 11
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), NullLogger.Instance);
        arbosState.BackingStorage.Set(ArbosStateOffsets.VersionOffset, ArbosVersion.Eleven - 1);

        uint sendMerkleTreeStateMethodId = PrecompileHelper.GetMethodId("sendMerkleTreeState()");
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            ArbSysParser.PrecompileFunctions[sendMerkleTreeStateMethodId].AbiFunctionDescription.GetCallInfo().Signature,
            []
        );

        Address sender = TestItem.AddressA;
        long gasLimit = 1_000_000;
        Transaction tx = Build.A.Transaction
            .WithTo(ArbosAddresses.ArbSysAddress)
            .WithValue(0)
            .WithData(calldata)
            .WithGasLimit(gasLimit)
            .WithGasPrice(1_000_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 senderInitialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.EvmExceptionType.Should().Be(EvmExceptionType.PrecompileFailure);

        tracer.ReturnValue.Should().BeEmpty();

        long expectedGasSpent = gasLimit; // Consumes everything
        tracer.GasSpent.Should().Be(expectedGasSpent);

        UInt256 senderFinalBalance = worldState.GetBalance(sender);
        senderFinalBalance.Should().Be(senderInitialBalance - (ulong)expectedGasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void PrecompileExecution_ThrowsRevertException_RevertsAndRefundsGasLeft()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        ulong baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using IDisposable worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        // To trigger the revert in the minInitGas() function, arbos version should be lower than 32
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), NullLogger.Instance);
        arbosState.BackingStorage.Set(ArbosStateOffsets.VersionOffset, ArbosVersion.StylusChargingFixes - 1);

        uint minInitGasMethodId = PrecompileHelper.GetMethodId("minInitGas()");
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            ArbWasmParser.PrecompileFunctions[minInitGasMethodId].AbiFunctionDescription.GetCallInfo().Signature,
            []
        );

        Address sender = TestItem.AddressA;
        long gasLimit = 1_000_000;
        Transaction tx = Build.A.Transaction
            .WithTo(ArbosAddresses.ArbWasmAddress)
            .WithValue(0)
            .WithData(calldata)
            .WithGasLimit(gasLimit)
            .WithGasPrice(1_000_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 senderInitialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.EvmExceptionType.Should().Be(EvmExceptionType.Revert);

        tracer.ReturnValue.Should().BeEmpty(); // No output even if precompile failure is a revert !

        long intrinsicGas = GasCostOf.Transaction + 64;
        // Opening arbos + get stylus params in precompile method
        long precompileExecGas = (long)ArbosStorage.StorageReadCost + GasCostOf.CallPrecompileEip2929;
        long expectedGasSpent = intrinsicGas + precompileExecGas; // Got refunded the rest
        tracer.GasSpent.Should().Be(expectedGasSpent);

        UInt256 senderFinalBalance = worldState.GetBalance(sender);
        senderFinalBalance.Should().Be(senderInitialBalance - (ulong)expectedGasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void PrecompileExecution_CalldataTooSmallForMethodId_RevertsAndConsumesAllGas()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });
        });

        ulong baseFeePerGas = 1_000;
        chain.BlockTree.Head!.Header.BaseFeePerGas = baseFeePerGas;

        FullChainSimulationSpecProvider fullChainSimulationSpecProvider = new();
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using IDisposable worldStateDisposer = worldState.BeginScope(chain.BlockTree.Head!.Header);

        byte[] calldata = new byte[3]; // Method ID should be 4 bytes long

        Address sender = TestItem.AddressA;
        long gasLimit = 1_000_000;

        Transaction tx = Build.A.Transaction
            .WithTo(ArbosAddresses.ArbGasInfoAddress)
            .WithValue(0)
            .WithData(calldata)
            .WithGasLimit(gasLimit)
            .WithGasPrice(1_000_000_000)
            .WithNonce(worldState.GetNonce(sender))
            .WithSenderAddress(sender)
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;

        UInt256 senderInitialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.EvmExceptionType.Should().Be(EvmExceptionType.Revert);

        long gasSpent = gasLimit; // Consumes all gas (runs out of gas)
        tracer.GasSpent.Should().Be(gasSpent);

        tracer.ReturnValue.Should().BeEmpty(); // Revert with no output data

        UInt256 senderFinalBalance = worldState.GetBalance(sender);
        senderFinalBalance.Should().Be(senderInitialBalance - (ulong)gasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
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
    public async Task CallingPrecompileWithValue_FunctionIsNotPayable_RevertsAndConsumesAllGas()
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
        result.Result.Should().Be(Result.Success); // Overall block creation succeeded even though the tx failed

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
        result.EvmExceptionType.Should().Be(EvmExceptionType.None); // Succeeds (no revert nor failure)

        ulong gasSpent = GasCostOf.Transaction + 64; // 64 gas units for intrinsic gas
        tracer.GasSpent.Should().Be((long)gasSpent); // Consumes 0 gas in EVM

        tracer.ReturnValue.Should().BeEmpty(); // No output data

        UInt256 finalBalance = worldState.GetBalance(sender);
        finalBalance.Should().Be(initialBalance - gasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void CallingPrecompile_FunctionIsNotActivated_RevertsAndConsumesAllGas()
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
        result.EvmExceptionType.Should().Be(EvmExceptionType.Revert);

        long gasSpent = gasLimit; // Consumes all gas in EVM
        tracer.GasSpent.Should().Be(gasSpent);

        tracer.ReturnValue.Should().BeEmpty(); // Revert with no output data

        UInt256 finalBalance = worldState.GetBalance(sender);
        finalBalance.Should().Be(initialBalance - (ulong)gasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void CallingNonOwnerPrecompile_FunctionDoesNotExist_RevertsAndConsumesAllGas()
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
        result.EvmExceptionType.Should().Be(EvmExceptionType.Revert);

        long gasSpent = gasLimit; // Consumes all gas in EVM
        tracer.GasSpent.Should().Be(gasSpent);

        tracer.ReturnValue.Should().BeEmpty(); // Revert with no output data

        UInt256 finalBalance = worldState.GetBalance(sender);
        finalBalance.Should().Be(initialBalance - (ulong)gasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void CallingPrecompile_ExecutingAccountIsNotActingAsPrecompileAndCallsViewFunction_RevertsAndConsumesAllGas()
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

        byte[] runtimeCode = PrepareByteCodeWithCallToPrecompile(
            Instruction.DELEGATECALL, ArbInfo.Address, methodSelector, addressWhoseBalanceToGet, outputSize: 32);

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
        // even if precompile reverted (and not failed) because precompile explicitly set state.GasAvailable to 0.
        // Some amount of gas is left over due to the 63/64 rule before the delegatecall, not all being burnt after the delegatecall.
        long gasSpent = 984_724;
        tracer.GasSpent.Should().Be(gasSpent);

        tracer.ReturnValue.Should().BeEmpty(); // Revert with no output data

        UInt256 finalBalance = worldState.GetBalance(sender);
        finalBalance.Should().Be(initialBalance - (ulong)gasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void CallingPrecompile_ReadOnlyFrameCallingNonPayableFunction_RevertsAndConsumesAllGas()
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

        byte[] runtimeCode = PrepareByteCodeWithCallToPrecompile(
            Instruction.STATICCALL, ArbAddressTable.Address, methodSelector, addressToRegister, outputSize: 32);

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
        // even if precompile reverted (and not failed) because precompile explicitly set state.GasAvailable to 0.
        // Some amount of gas is left over due to the 63/64 rule before the staticccall, not all being burnt after the staticccall.
        long gasSpent = 984_724;
        tracer.GasSpent.Should().Be(gasSpent);

        tracer.ReturnValue.Should().BeEmpty(); // Revert with no output data

        UInt256 finalBalance = worldState.GetBalance(sender);
        finalBalance.Should().Be(initialBalance - (ulong)gasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void CallingPrecompile_FunctionIsPure_DoesNotOpenArbos()
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

        // Calldata to call mapL1SenderContractAddressToL2Alias(address) on ArbSys precompile
        uint setWasmMinInitGasMethodId = PrecompileHelper.GetMethodId("mapL1SenderContractAddressToL2Alias(address,address)");
        Address addressToMap = TestItem.AddressB;

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            ArbSysParser.PrecompileFunctions[setWasmMinInitGasMethodId].AbiFunctionDescription.GetCallInfo().Signature,
            [addressToMap, Address.Zero] // 2nd address is unused in precompile but still needed by ABI
        );

        long gasLimit = 1_000_000;
        Transaction transaction = Build.A.Transaction
            .WithChainId(chain.ChainSpec.ChainId)
            .WithType(TxType.EIP1559)
            .WithTo(ArbSys.Address)
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
        result.EvmExceptionType.Should().Be(EvmExceptionType.None); // Succeeds

        long intrinsicGas = GasCostOf.Transaction + 560; // Intrinsic gas cost
        // Precompile execution does not open arbos state, so no additional gas cost
        long precompileExecCost = 9; // 6 for input arg cost + 3 for output arg cost
        long gasSpent = intrinsicGas + precompileExecCost;
        tracer.GasSpent.Should().Be(gasSpent);

        UInt256 finalBalance = worldState.GetBalance(sender);
        finalBalance.Should().Be(initialBalance - (ulong)gasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas

        // Make sure expected method indeed got called

        Address offset = new("0x1111000000000000000000000000000000001111");
        UInt256 AddressAliasOffset = new(offset.Bytes, isBigEndian: true);

        UInt256 l1AddressAsNumber = new(addressToMap.Bytes, isBigEndian: true);
        UInt256 sumBytes = l1AddressAsNumber + AddressAliasOffset;
        Address mappedAddress = new(sumBytes.ToBigEndian()[12..]);

        byte[] expectedResult = new byte[32];
        mappedAddress.Bytes.CopyTo(expectedResult, 12);

        tracer.ReturnValue.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public void NestedNonOwnerPrecompileCall_Succeeds_PassesOutputAndRefundsGasLeft()
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

        Address addressWhoseBalanceToGet = new("0x0000000000000000000000000000000000000456");
        UInt256 expectedBalance = 2000;
        worldState.AddToBalanceAndCreateIfNotExists(addressWhoseBalanceToGet, expectedBalance, fullChainSimulationSpecProvider.GenesisSpec);

        // Insert a contract inside the world state
        Address contractAddress = new("0x0000000000000000000000000000000000000123");
        worldState.CreateAccount(contractAddress, 0);

        // Careful: methodSelector should be right-padded with 0s
        byte[] methodSelector = new byte[Hash256.Size];
        byte[] calldata = KeccakHash.ComputeHashBytes("getBalance(address)"u8)[..4];
        calldata.CopyTo(methodSelector, 0);

        // Careful: arguments should be left-padded with 0s
        byte[] methodArgument = new byte[Hash256.Size];
        addressWhoseBalanceToGet.Bytes.CopyTo(methodArgument, Hash256.Size - Address.Size);

        // outputSize is 32 because getBalance(address) returns a uint256
        byte[] runtimeCode = PrepareByteCodeWithCallToPrecompile(
            Instruction.CALL, ArbInfo.Address, methodSelector, methodArgument, outputSize: 32);

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

        UInt256 initialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.TransactionExecuted.Should().Be(true);
        result.EvmExceptionType.Should().Be(EvmExceptionType.None); // Top-level call succeeds (meaning nested call succeeded as well)

        // A bit of a magic number but what is interesting is very little of the gas limit was burned
        // as precompile succeeded, effectively refunding gas left to the caller
        long gasSpent = 22_670;
        tracer.GasSpent.Should().Be(gasSpent);

        // Precompile output was effectively passed to the caller through the memory
        tracer.ReturnValue.Should().BeEquivalentTo(expectedBalance.ToBigEndian());

        UInt256 finalBalance = worldState.GetBalance(sender);
        finalBalance.Should().Be(initialBalance - (ulong)gasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void NestedOwnerPrecompileCall_Succeeds_PassesOutputAndRefundsGasSupplied()
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
        byte[] calldata = KeccakHash.ComputeHashBytes("getNetworkFeeAccount()"u8)[..4];
        calldata.CopyTo(methodSelector, 0);

        byte[] methodArgument = []; // Empty calldata

        // outputSize is 32 because getNetworkFeeAccount() returns an address abi encoded (static arg, 32 bytes)
        byte[] runtimeCode = PrepareByteCodeWithCallToPrecompile(
            Instruction.CALL, ArbOwner.Address, methodSelector, methodArgument, outputSize: 32);

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

        // Add contract as a chain owner as it will be the one invoking the owner precompile
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), NullLogger.Instance);
        arbosState.ChainOwners.Add(contractAddress);

        UInt256 initialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.TransactionExecuted.Should().Be(true);
        result.EvmExceptionType.Should().Be(EvmExceptionType.None); // Top-level call succeeds (meaning nested call succeeded as well)

        // A bit of a magic number but what is interesting is very little of the gas limit was burned
        // as precompile succeeded, effectively refunding gas supplied to the caller
        long gasSpent = 21_163;
        tracer.GasSpent.Should().Be(gasSpent);

        // Expected result
        Address networkFeeAccount = arbosState.NetworkFeeAccount.Get();
        byte[] abiEncodedAccount = new byte[Hash256.Size];
        networkFeeAccount.Bytes.CopyTo(abiEncodedAccount, 12);

        // Precompile output was effectively passed to the caller through the memory
        tracer.ReturnValue.Should().BeEquivalentTo(abiEncodedAccount);

        UInt256 finalBalance = worldState.GetBalance(sender);
        finalBalance.Should().Be(initialBalance - (ulong)gasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void NestedOwnerPrecompileCall_RevertsWithEmptyOutput_ReturnsNoOutputAndRefundsGasSupplied()
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
        byte[] calldata = KeccakHash.ComputeHashBytes("someInexistingFunction()"u8)[..4];
        calldata.CopyTo(methodSelector, 0);

        byte[] methodArgument = []; // Empty calldata, will fail before anyway

        // Set outputSize as 32 even if precompile reverting for an inexisting method
        // should not return any output data. This allows to test that even if the method
        // was expected to return some output, precompile returned RETURNDATASIZE (0) bytes of memory.
        ulong outputSize = 32;

        byte[] runtimeCode = PrepareByteCodeWithCallToPrecompile(
            Instruction.CALL, ArbOwner.Address, methodSelector, methodArgument, outputSize);

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

        // Add contract as a chain owner as it will be the one invoking the owner precompile
        ArbosState arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), NullLogger.Instance);
        arbosState.ChainOwners.Add(contractAddress);

        UInt256 initialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.TransactionExecuted.Should().Be(true);
        result.EvmExceptionType.Should().Be(EvmExceptionType.Revert); // Top-level call reverts (as nested call failed)

        // A bit of a magic number but what is interesting is very little of the gas limit was burned.
        // Even if precompile reverted and set state.GasAvailable to 0, the owner-only precompile access
        // refunded the gas supplied to the caller.
        long gasSpent = 21_161;
        tracer.GasSpent.Should().Be(gasSpent);

        // Making sure precompile reverted with no output data (RETURNDATASIZE opcode should return 0)
        tracer.ReturnValue.Should().BeEmpty();

        UInt256 finalBalance = worldState.GetBalance(sender);
        finalBalance.Should().Be(initialBalance - (ulong)gasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void NestedNonOwnerPrecompileCall_RevertsWithSolidityError_ReturnsErrorDataAndRefundsGasLeft()
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
        byte[] calldata = KeccakHash.ComputeHashBytes("arbBlockHash(uint256)"u8)[..4];
        calldata.CopyTo(methodSelector, 0);

        // Careful: arguments should be left-padded with 0s
        byte[] methodArgument = new byte[Hash256.Size];
        UInt256 arbBlockNum = ulong.MaxValue + UInt256.One; // bigger than uint64 max to trigger the solidity error
        arbBlockNum.ToBigEndian().CopyTo(methodArgument, 0);

        // Set outputSize as 68 as I expect the precompile to return a solidity error of size 68 bytes
        ulong outputSize = 68;

        byte[] runtimeCode = PrepareByteCodeWithCallToPrecompile(
            Instruction.CALL, ArbSys.Address, methodSelector, methodArgument, outputSize);

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

        UInt256 initialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.TransactionExecuted.Should().Be(true);
        result.EvmExceptionType.Should().Be(EvmExceptionType.Revert); // Top-level call reverts (as nested call failed)

        // A bit of a magic number but what is interesting is very little of the gas limit was burned.
        // Even if precompile reverted and set state.GasAvailable to 0, the owner-only precompile access
        // refunded the gas supplied to the caller.
        long gasSpent = 21_977;
        tracer.GasSpent.Should().Be(gasSpent);

        // Making sure precompile returned and passed the solidity error data to enclosing call
        PrecompileSolidityError expectedSolidityError = ArbSys.InvalidBlockNumberSolidityError(arbBlockNum, blCtx.Number);
        tracer.ReturnValue.Should().BeEquivalentTo(expectedSolidityError.ErrorData);

        UInt256 finalBalance = worldState.GetBalance(sender);
        finalBalance.Should().Be(initialBalance - (ulong)gasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    [Test]
    public void NestedOwnerPrecompileCall_FailsWithoutReverting_PassesNoOutputToCallerAndNoRefundToCaller()
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
        byte[] methodId = KeccakHash.ComputeHashBytes("getNetworkFeeAccount()"u8)[..4];
        methodId.CopyTo(methodSelector, 0);

        byte[] methodArgument = []; // Empty but will fail before anyway

        // Set outputSize as 32 even if precompile failing (without reverting) should not return any output data.
        // This allows to test that even if the method was expected to return some output,
        // precompile returned RETURNDATASIZE (0) bytes of memory.
        ulong outputSize = 32;

        byte[] runtimeCode = PrepareByteCodeWithCallToPrecompile(
            Instruction.CALL, ArbOwner.Address, methodSelector, methodArgument, outputSize);

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

        UInt256 initialBalance = worldState.GetBalance(sender);

        TestAllTracerWithOutput tracer = new();
        TransactionResult result = chain.TxProcessor.Execute(tx, tracer);

        result.Should().Be(TransactionResult.Ok);
        result.TransactionExecuted.Should().Be(true);

        // We omitted adding the contract as a chain owner, inner call to precompile will fail
        result.EvmExceptionType.Should().Be(EvmExceptionType.Revert); // Top-level call reverts (as nested call failed)

        // A bit of a magic number but what is interesting is most of the tx's gas limit was burned
        // as precompile failed (without reverting), even if precompile returned gasLeft to caller.
        // Some amount of gas is left over due to the 63/64 rule before the inner call, not all being burnt after the call.
        long gasSpent = 984_724;
        tracer.GasSpent.Should().Be(gasSpent);

        tracer.ReturnValue.Should().BeEmpty(); // No output data returned by precompile to enclosing call

        UInt256 finalBalance = worldState.GetBalance(sender);
        finalBalance.Should().Be(initialBalance - (ulong)gasSpent * baseFeePerGas); // Effective gas price is baseFeePerGas
    }

    /// <summary>
    /// Bytecode to call a precompile and returns the precompile output if it was successful,
    /// and otherwise reverts with the error output data.
    /// </summary>
    /// <param name="callType">The type of call to make. If CALL or CALLCODE, you can provide a value parameter.</param>
    /// <param name="precompileAddress">The address of the precompile to call.</param>
    /// <param name="methodSelector">Abi-encoded method selector to call.</param>
    /// <param name="methodArguments">Abi-encoded arguments of the method.</param>
    /// <param name="outputSize">The size of the output to expect. Either expected METHOD output
    /// if call is supposed to be successful, or expected SOLIDITY ERROR output if call is supposed to revert.
    /// See further explanation inside function.</param>
    /// <param name="value">The value to send with the call.</param>
    /// <returns>Bytecode.</returns>
    private static byte[] PrepareByteCodeWithCallToPrecompile(
        Instruction callType, Address precompileAddress, byte[] methodSelector, byte[] methodArguments, ulong outputSize, ulong value = 0)
    {
        if (methodSelector.Length == 32 && !methodSelector.AsSpan()[4..].IsZero())
            throw new ArgumentException("methodSelector does not respect the expected format");

        if (methodArguments.Length % 32 != 0)
            throw new ArgumentException("methodArguments must be a multiple of 32 bytes long");

        if (value != 0 && (callType == Instruction.STATICCALL || callType == Instruction.DELEGATECALL))
            throw new ArgumentException("no value (or value 0) must be provided for STATICCALL and DELEGATECALL");

        int calldataSize = 4 + methodArguments.Length; // method ID + arguments

        const int OutputLocation = 0; // Precompile expected output location in memory
        const int CalldataStartInMemory = 0;

        // We take the expected output size as an argument because a contract method's
        // successful return data size and its revert data size are independent.
        //
        // Therefore, the failure path in below bytecode will revert with RETURNDATASIZE bytes (revert data size here)
        // when actually the call's output saved in memory will be of size min(outputSize, RETURNDATASIZE).
        // If outputSize is smaller than RETURNDATASIZE, the top level call will return non-sense (only a part of the error).
        // So, when expecting to return some solidity error output, make sure to set outputSize to the expected error output size (or larger).

        // Bytecode to call a precompile and returns the precompile output if it was successful, otherwise reverts
        Prepare runtimeCode = Prepare.EvmCode
            // 1. Store input data in memory
            .PushData(methodSelector)
            .PushData(CalldataStartInMemory)
            .Op(Instruction.MSTORE)               // Stores the method selector in memory at offset 0

            .PushData(methodArguments)
            .PushData(CalldataStartInMemory + 4)  // Overwrite the right-padding of the method selector with the method argument
            .Op(Instruction.MSTORE)               // Stores the method argument in memory at offset 4

            // 2. Prepare arguments and execute the call
            .PushData(outputSize)                 // retSize: we expect outputSize bytes back
            .PushData(OutputLocation)             // retOffset: where to store the return data in memory
            .PushData(calldataSize)               // dataSize: input data size
            .PushData(CalldataStartInMemory);     // dataOffset: start of calldata in memory

        if (callType == Instruction.CALL || callType == Instruction.CALLCODE)
            runtimeCode.PushData(value);          // value: amount of value to transfer

        runtimeCode
            .PushData(precompileAddress)          // address: precompile to call
            .Op(Instruction.GAS)                  // gas: forward all remaining gas to call
            .Op(callType);                        // call will pop all 6 (or 7) arguments depending on the call type

        // JumpDest is at this offset in the final bytecode. This holds as long as the bytecode
        // is not modified between here and the JUMPDEST opcode.
        byte[] bytecodeSoFar = runtimeCode.Done;
        int jumpdestOffsetInBytecode = bytecodeSoFar.Length - 1 + 8;

        runtimeCode
            // 3. BRANCHING: Check the call result (1 or 0) and jump if successful
            .PushData(jumpdestOffsetInBytecode)   // Code offset to jump to (JUMPDEST index in bytecode)
            .Op(Instruction.JUMPI)                // Jumps if the result on the stack is 1 (success)

            // 4. FAILURE PATH: This code only runs if the JUMPI condition was false (precompile result was 0)
            .Op(Instruction.RETURNDATASIZE)       // retSize: the size of the data returned by the precompile call
            .PushData(OutputLocation)             // retOffset: 0 bytes
            .Op(Instruction.REVERT)               // Revert with 0 bytes of data

            // 5. SUCCESS PATH: Return the result from the call.
            .Op(Instruction.JUMPDEST)             // Mark a valid jump destination
                                                  // The balance is now in memory at offset 0, as specified by earlier retOffset.
            .PushData(outputSize)                 // size: The size of the data to return (32 bytes).
            .PushData(OutputLocation)             // offset: The memory location of the data to return.
            .Op(Instruction.RETURN);              // Return the result from the precompile call

        return runtimeCode.Done;
    }
}
