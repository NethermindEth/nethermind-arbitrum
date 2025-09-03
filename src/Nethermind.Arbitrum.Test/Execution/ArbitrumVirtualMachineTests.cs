using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Evm.Test;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
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

        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(
            worldState, burner, _logManager.GetClassLogger<ArbosState>()
        );

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
        BlockExecutionContext blCtx = new(chain.BlockTree.Head!.Header, fullChainSimulationSpecProvider.GenesisSpec);
        chain.TxProcessor.SetBlockExecutionContext(in blCtx);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;

        SystemBurner burner = new(readOnly: false);
        ArbosState arbosState = ArbosState.OpenArbosState(
            worldState, burner, _logManager.GetClassLogger<ArbosState>()
        );

        // Set arbos version to 9 so that GasPrice opcode returns tx.GasPrice
        arbosState.BackingStorage.Set(ArbosStateOffsets.VersionOffset, ArbosVersion.Nine);

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
        // MaxFeePerGas will become effectiveGasPrice as maxFeePerGas < tx.MaxPriorityFeePerGas + baseFee
        // Make it greater than baseFeePerGas for BuyGas to succeed
        ulong maxFeePerGas = baseFeePerGas + 1;

        Transaction tx = Build.A.Transaction
            .WithTo(contractAddress)
            .WithValue(0)
            // .WithData() // no input data, tx will just execute bytecode from beginning
            .WithGasLimit(gasLimit)

            // make tx.GasPrice <= baseFee to have a different effectiveGasPrice
            // (hence vm.txExecContext.GasPrice) than baseFee. This allows to have vm.TxExecutionContext.GasPrice
            // different from vm.BlockExecutionContext.Header.BaseFeePerGas to correctly assert GasPrice opcode's returned value.
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

        ArbosState arbosState = ArbosState.OpenArbosState(
            worldState, new ZeroGasBurner(), _logManager.GetClassLogger<ArbosState>()
        );

        ulong l1BlockNumber = 111;
        arbosState.Blockhashes.SetL1BlockNumber(l1BlockNumber);

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
        l2BlockNumber.Should().Be(0);
        returnedBlockNumber.ToUInt64(null).Should().NotBe(l2BlockNumber);
    }
}
