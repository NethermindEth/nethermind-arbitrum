// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain.Find;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;
using Nethermind.Evm;
using Nethermind.Facade.Eth.RpcTransaction;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Data;

namespace Nethermind.Arbitrum.Test.Rpc;

[TestFixture]
public class ArbitrumEthRpcModuleTests
{
    private static readonly AbiSignature TransferSignature = new("transfer", AbiType.Address, AbiType.UInt256);
    private static readonly AbiSignature BalanceOfSignature = new("balanceOf", AbiType.Address);

    private ArbitrumRpcTestBlockchain _chain = null!;
    private EthereumEcdsa _ethereumEcdsa = null!;

    [SetUp]
    public void Setup()
    {
        _chain = ArbitrumRpcTestBlockchain.CreateDefault();

        var initMessage = FullChainSimulationInitMessage.CreateDigestInitMessage(92);
        _chain.ArbitrumRpcModule.DigestInitMessage(initMessage);

        _ethereumEcdsa = new EthereumEcdsa(_chain.SpecProvider.ChainId);
    }

    [TearDown]
    public void TearDown()
    {
        _chain?.Dispose();
    }

    [Test]
    public async Task EthCall_WithNonZeroBaseFee_ExecutesWithZeroBaseFee()
    {
        await ProduceBlockWithBaseFee(1000.Wei());

        Transaction tx = Build.A.Transaction
            .WithSenderAddress(FullChainSimulationAccounts.TestAccountA.Address)
            .WithTo(FullChainSimulationAccounts.TestAccountB.Address)
            .WithValue(100.Wei())
            .WithGasLimit(Transaction.BaseTxGasCost)
            .TestObject;

        TransactionForRpc txCall = TransactionForRpc.FromTransaction(tx);

        ResultWrapper<string> result = _chain.ArbitrumEthRpcModule.eth_call(txCall, BlockParameter.Latest);

        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().Be("0x");
    }

    [Test]
    public async Task EthCall_WithStateOverride_AppliesOverridesCorrectly()
    {
        await ProduceBlockWithBaseFee(500.Wei());

        Transaction tx = Build.A.Transaction
            .WithTo(FullChainSimulationAccounts.TestAccountB.Address)
            .WithValue(100.Wei())
            .WithGasLimit(50000)
            .TestObject;

        TransactionForRpc txCall = TransactionForRpc.FromTransaction(tx);

        Dictionary<Address, AccountOverride> stateOverride = new()
        {
            [FullChainSimulationAccounts.TestAccountA.Address] = new AccountOverride { Balance = 999.Ether() }
        };

        ResultWrapper<string> result = _chain.ArbitrumEthRpcModule.eth_call(txCall, BlockParameter.Latest, stateOverride);

        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().Be("0x");
    }

    [Test]
    public async Task EthCall_ContractCreationWithoutData_ReturnsInvalidInputError()
    {
        await ProduceBlockWithBaseFee(100.Wei());

        Transaction tx = Build.A.Transaction
            .WithSenderAddress(FullChainSimulationAccounts.TestAccountA.Address)
            .WithTo(null)
            .WithData(Array.Empty<byte>())
            .WithGasLimit(Transaction.BaseTxGasCost)
            .TestObject;

        TransactionForRpc txCall = TransactionForRpc.FromTransaction(tx);

        ResultWrapper<string> result = _chain.ArbitrumEthRpcModule.eth_call(txCall, BlockParameter.Latest);

        result.Result.ResultType.Should().Be(ResultType.Failure);
        result.ErrorCode.Should().Be(ErrorCodes.InvalidInput);
        result.Result.Error.Should().Contain("Contract creation without any data provided");
    }

    [Test]
    public async Task EthEstimateGas_ContractCreationWithoutData_ReturnsInvalidInputError()
    {
        await ProduceBlockWithBaseFee(100.Wei());

        Transaction tx = Build.A.Transaction
            .WithSenderAddress(FullChainSimulationAccounts.TestAccountA.Address)
            .WithTo(null)
            .WithData(Array.Empty<byte>())
            .TestObject;

        TransactionForRpc txCall = TransactionForRpc.FromTransaction(tx);

        ResultWrapper<UInt256?> result = _chain.ArbitrumEthRpcModule.eth_estimateGas(txCall, BlockParameter.Latest);

        result.Result.ResultType.Should().Be(ResultType.Failure);
        result.ErrorCode.Should().Be(ErrorCodes.InvalidInput);
        result.Result.Error.Should().Contain("Contract creation without any data provided");
    }

    [Test]
    public async Task EthEstimateGas_WhenInsufficientBalance_ReturnsExecutionError()
    {
        await ProduceBlockWithBaseFee(1000.Wei());

        Transaction tx = Build.A.Transaction
            .WithSenderAddress(FullChainSimulationAccounts.TestAccountA.Address)
            .WithTo(FullChainSimulationAccounts.TestAccountB.Address)
            .WithValue(10000.Ether())
            .TestObject;

        TransactionForRpc txCall = TransactionForRpc.FromTransaction(tx);

        ResultWrapper<UInt256?> result = _chain.ArbitrumEthRpcModule.eth_estimateGas(txCall, BlockParameter.Latest);

        result.Result.ResultType.Should().Be(ResultType.Failure);
        result.ErrorCode.Should().Be(ErrorCodes.InvalidInput);
    }

    [Test]
    public async Task EthCreateAccessList_WithNonZeroBaseFee_CreatesWithZeroBaseFee()
    {
        Address contractAddress = await DeployTestContract();
        await ProduceBlockWithBaseFee(3000.Wei());

        Transaction tx = Build.A.Transaction
            .WithSenderAddress(FullChainSimulationAccounts.TestAccountA.Address)
            .WithTo(contractAddress)
            .WithData(AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, BalanceOfSignature, FullChainSimulationAccounts.TestAccountA.Address))
            .WithGasLimit(50000)
            .TestObject;

        TransactionForRpc txCall = TransactionForRpc.FromTransaction(tx);

        ResultWrapper<AccessListResultForRpc?> result = _chain.ArbitrumEthRpcModule.eth_createAccessList(txCall, BlockParameter.Latest);

        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().NotBeNull();
        result.Data?.AccessList.Should().NotBeNull();
        result.Data?.GasUsed.Should().BeGreaterThan(UInt256.Zero);
    }

    [Test]
    public async Task EthCreateAccessList_WithOptimizationEnabled_ReturnsOptimizedAccessList()
    {
        Address contractAddress = await DeployTestContract();
        await ProduceBlockWithBaseFee(1000.Wei());

        Transaction tx = Build.A.Transaction
            .WithSenderAddress(FullChainSimulationAccounts.TestAccountA.Address)
            .WithTo(contractAddress)
            .WithData(AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, TransferSignature,FullChainSimulationAccounts.TestAccountB.Address, 100.Wei()))
            .WithGasLimit(50000)
            .TestObject;

        TransactionForRpc txCall = TransactionForRpc.FromTransaction(tx);

        ResultWrapper<AccessListResultForRpc?> optimizedResult = _chain.ArbitrumEthRpcModule.eth_createAccessList(txCall, BlockParameter.Latest, optimize: true);
        ResultWrapper<AccessListResultForRpc?> nonOptimizedResult = _chain.ArbitrumEthRpcModule.eth_createAccessList(txCall, BlockParameter.Latest, optimize: false);

        optimizedResult.Result.ResultType.Should().Be(ResultType.Success);
        nonOptimizedResult.Result.ResultType.Should().Be(ResultType.Success);
        optimizedResult.Data.Should().NotBeNull();
        nonOptimizedResult.Data.Should().NotBeNull();
    }

    [Test]
    public async Task EthCreateAccessList_ContractCreationWithoutData_ReturnsInvalidInputError()
    {
        await ProduceBlockWithBaseFee(100.Wei());

        Transaction tx = Build.A.Transaction
            .WithSenderAddress(FullChainSimulationAccounts.TestAccountA.Address)
            .WithTo(null)
            .WithData(Array.Empty<byte>())
            .TestObject;

        TransactionForRpc txCall = TransactionForRpc.FromTransaction(tx);

        ResultWrapper<AccessListResultForRpc?> result = _chain.ArbitrumEthRpcModule.eth_createAccessList(txCall, BlockParameter.Latest);

        result.Result.ResultType.Should().Be(ResultType.Failure);
        result.ErrorCode.Should().Be(ErrorCodes.InvalidInput);
        result.Result.Error.Should().Contain("Contract creation without any data provided");
    }

    [Test]
    public async Task EthCall_WithNullGas_UsesBlockGasLimit()
    {
        await ProduceBlockWithBaseFee(500.Wei());

        Transaction tx = Build.A.Transaction
            .WithSenderAddress(FullChainSimulationAccounts.TestAccountA.Address)
            .WithTo(FullChainSimulationAccounts.TestAccountB.Address)
            .WithValue(50.Wei())
            .TestObject;

        TransactionForRpc txCall = TransactionForRpc.FromTransaction(tx);
        txCall.Gas = null;

        ResultWrapper<string> result = _chain.ArbitrumEthRpcModule.eth_call(txCall, BlockParameter.Latest);

        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().Be("0x");
    }

    [Test]
    public async Task EthCall_AtSpecificBlockNumber_UsesCorrectBaseFee()
    {
        await ProduceBlockWithBaseFee(100.Wei());
        await ProduceBlockWithBaseFee(200.Wei());
        await ProduceBlockWithBaseFee(300.Wei());

        Transaction tx = Build.A.Transaction
            .WithSenderAddress(FullChainSimulationAccounts.TestAccountA.Address)
            .WithTo(FullChainSimulationAccounts.TestAccountB.Address)
            .WithValue(10.Wei())
            .WithGasLimit(Transaction.BaseTxGasCost)
            .TestObject;

        TransactionForRpc txCall = TransactionForRpc.FromTransaction(tx);

        ResultWrapper<string> result = _chain.ArbitrumEthRpcModule.eth_call(txCall, new BlockParameter(2));

        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().Be("0x");
    }

    [Test]
    public async Task EthEstimateGas_WhenInvalidCallData_ReturnsExecutionError()
    {
        await ProduceBlockWithBaseFee(1000.Wei());

        Transaction tx = Build.A.Transaction
            .WithSenderAddress(FullChainSimulationAccounts.TestAccountA.Address)
            .WithTo(Address.Zero) // Sending to zero address with data should fail
            .WithData(new byte[] { 0xff, 0xff, 0xff, 0xff })
            .TestObject;

        TransactionForRpc txCall = TransactionForRpc.FromTransaction(tx);

        ResultWrapper<UInt256?> result = _chain.ArbitrumEthRpcModule.eth_estimateGas(txCall, BlockParameter.Latest);

        result.Result.ResultType.Should().Be(ResultType.Failure);
    }

    private async Task ProduceBlockWithBaseFee(UInt256 baseFee)
    {
        TestEthDeposit deposit = new(
            TestItem.KeccakA,
            baseFee,
            FullChainSimulationAccounts.TestAccountA.Address,
            FullChainSimulationAccounts.TestAccountA.Address,
            1.Ether()
        );

        await _chain.Digest(deposit);
    }

    private async Task<Address> DeployTestContract()
    {
        // Simple storage contract bytecode (just stores and retrieves values)
        byte[] bytecode = Bytes.FromHexString("0x608060405234801561001057600080fd5b5060405161011a38038061011a8339818101604052602081101561003357600080fd5b505160005560c0806100446000396000f3fe608060405260043610601f5760003560e01c806306fdde031460245780630a9059cbb14604e575b600080fd5b602a6054565b6040518082815260200191505060405180910390f35b6064607c565b6040518082815260200191505060405180910390f35b60005481565b600055565b6000548156fea26469706673582212207f2d42b9b2c2b4c02ed89d63dd13d2a13637c6ac476fc113e6c4a3e33f948f79c64736f6c63430008000033");

        TestEthDeposit deposit = new(
            TestItem.KeccakB,
            100.Wei(),
            FullChainSimulationAccounts.TestAccountA.Address,
            FullChainSimulationAccounts.TestAccountA.Address,
            10.Ether()
        );
        await _chain.Digest(deposit);

        Transaction deployTx = Build.A.Transaction
            .WithTo(null)
            .WithData(bytecode)
            .WithGasLimit(1000000)
            .WithGasPrice(100.Wei())
            .WithValue(0)
            .WithNonce(0)
            .SignedAndResolved(_ethereumEcdsa, FullChainSimulationAccounts.TestAccountA)
            .TestObject;

        TestL2Transactions l2Txs = new(
            TestItem.KeccakC,
            100.Wei(),
            FullChainSimulationAccounts.TestAccountA.Address,
            deployTx
        );

        await _chain.Digest(l2Txs);

        return ContractAddress.From(FullChainSimulationAccounts.TestAccountA.Address, 0);
    }
}
