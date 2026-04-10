// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Rpc;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain.Find;
using Nethermind.Core;
using Nethermind.Core.Collections;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;
using Nethermind.Evm;
using Nethermind.Facade.Eth;
using Nethermind.Facade.Eth.RpcTransaction;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Data;
using Nethermind.JsonRpc.Modules.Eth;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Test.Rpc;

[TestFixture]
public partial class ArbitrumEthRpcModuleTests
{
    private static readonly AbiSignature TransferSignature = new("transfer", AbiType.Address, AbiType.UInt256);
    private static readonly AbiSignature BalanceOfSignature = new("balanceOf", AbiType.Address);
    private static readonly ILogger TestLogger = LimboLogs.Instance.GetClassLogger<ArbitrumEthRpcModuleTests>();

    private ArbitrumRpcTestBlockchain _chain = null!;
    private EthereumEcdsa _ethereumEcdsa = null!;

    [SetUp]
    public void Setup()
    {
        ChainSpec chainSpec = FullChainSimulationChainSpecProvider.Create(40);
        _chain = ArbitrumRpcTestBlockchain.CreateDefault(null, chainSpec);

        DigestInitMessage initMessage = FullChainSimulationInitMessage.CreateDigestInitMessage(92, 40);
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
        await ProduceBlockWithBaseFee(1000.Wei);

        Transaction tx = Build.A.Transaction
            .WithSenderAddress(FullChainSimulationAccounts.AccountA.Address)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(100.Wei)
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
        await ProduceBlockWithBaseFee(500.Wei);

        Transaction tx = Build.A.Transaction
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(100.Wei)
            .WithGasLimit(50000)
            .WithSenderAddress(FullChainSimulationAccounts.AccountA.Address)
            .TestObject;

        TransactionForRpc txCall = TransactionForRpc.FromTransaction(tx);

        Dictionary<Address, AccountOverride> stateOverride = new()
        {
            [FullChainSimulationAccounts.AccountA.Address] = new AccountOverride { Balance = 999.Ether }
        };

        ResultWrapper<string> result = _chain.ArbitrumEthRpcModule.eth_call(txCall, BlockParameter.Latest, stateOverride);

        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().Be("0x");
    }

    [Test]
    public async Task EthCall_ContractCreationWithoutData_ReturnsInvalidInputError()
    {
        await ProduceBlockWithBaseFee(100.Wei);

        Transaction tx = Build.A.Transaction
            .WithSenderAddress(FullChainSimulationAccounts.AccountA.Address)
            .WithTo(null)
            .WithData(Array.Empty<byte>())
            .WithGasLimit(Transaction.BaseTxGasCost)
            .TestObject;

        TransactionForRpc txCall = TransactionForRpc.FromTransaction(tx);

        ResultWrapper<string> result = _chain.ArbitrumEthRpcModule.eth_call(txCall, BlockParameter.Latest);

        result.Result.ResultType.Should().Be(ResultType.Failure);
        result.ErrorCode.Should().Be(ErrorCodes.InvalidInput);
        result.Result.Error.Should().Contain("contract creation without any data provided");
    }

    [Test]
    public async Task EthEstimateGas_ContractCreationWithoutData_ReturnsInvalidInputError()
    {
        await ProduceBlockWithBaseFee(100.Wei);

        Transaction tx = Build.A.Transaction
            .WithSenderAddress(FullChainSimulationAccounts.AccountA.Address)
            .WithTo(null)
            .WithData(Array.Empty<byte>())
            .TestObject;

        TransactionForRpc txCall = TransactionForRpc.FromTransaction(tx);

        ResultWrapper<UInt256?> result = _chain.ArbitrumEthRpcModule.eth_estimateGas(txCall, BlockParameter.Latest);

        result.Result.ResultType.Should().Be(ResultType.Failure);
        result.ErrorCode.Should().Be(ErrorCodes.InvalidInput);
        result.Result.Error.Should().Contain("contract creation without any data provided");
    }

    [Test]
    public async Task EthEstimateGas_WhenInsufficientBalance_ReturnsExecutionError()
    {
        await ProduceBlockWithBaseFee(1000.Wei);

        Transaction tx = Build.A.Transaction
            .WithSenderAddress(FullChainSimulationAccounts.AccountA.Address)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(10000.Ether)
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
        await ProduceBlockWithBaseFee(3000.Wei);

        Transaction tx = Build.A.Transaction
            .WithSenderAddress(FullChainSimulationAccounts.AccountA.Address)
            .WithTo(contractAddress)
            .WithData(AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, BalanceOfSignature, FullChainSimulationAccounts.AccountA.Address))
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
        await ProduceBlockWithBaseFee(1000.Wei);

        Transaction tx = Build.A.Transaction
            .WithSenderAddress(FullChainSimulationAccounts.AccountA.Address)
            .WithTo(contractAddress)
            .WithData(AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, TransferSignature, FullChainSimulationAccounts.AccountB.Address, 100.Wei))
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
        await ProduceBlockWithBaseFee(100.Wei);

        Transaction tx = Build.A.Transaction
            .WithSenderAddress(FullChainSimulationAccounts.AccountA.Address)
            .WithTo(null)
            .WithData(Array.Empty<byte>())
            .TestObject;

        TransactionForRpc txCall = TransactionForRpc.FromTransaction(tx);

        ResultWrapper<AccessListResultForRpc?> result = _chain.ArbitrumEthRpcModule.eth_createAccessList(txCall, BlockParameter.Latest);

        result.Result.ResultType.Should().Be(ResultType.Failure);
        result.ErrorCode.Should().Be(ErrorCodes.InvalidInput);
        result.Result.Error.Should().Contain("contract creation without any data provided");
    }

    [Test]
    public async Task EthCall_WithNullGas_UsesBlockGasLimit()
    {
        await ProduceBlockWithBaseFee(500.Wei);

        Transaction tx = Build.A.Transaction
            .WithSenderAddress(FullChainSimulationAccounts.AccountA.Address)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(50.Wei)
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
        await ProduceBlockWithBaseFee(100.Wei);
        await ProduceBlockWithBaseFee(200.Wei);
        await ProduceBlockWithBaseFee(300.Wei);

        Transaction tx = Build.A.Transaction
            .WithSenderAddress(FullChainSimulationAccounts.AccountA.Address)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(10.Wei)
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
        await ProduceBlockWithBaseFee(1000.Wei);

        Transaction tx = Build.A.Transaction
            .WithSenderAddress(FullChainSimulationAccounts.AccountA.Address)
            .WithTo(Address.Zero) // Sending to zero address with data should fail
            .WithData(new byte[] { 0xff, 0xff, 0xff, 0xff })
            .TestObject;

        TransactionForRpc txCall = TransactionForRpc.FromTransaction(tx);

        ResultWrapper<UInt256?> result = _chain.ArbitrumEthRpcModule.eth_estimateGas(txCall, BlockParameter.Latest);

        result.Result.ResultType.Should().Be(ResultType.Failure);
    }

    [Test]
    public async Task EthGetStorageAt_HistoricalBlockHashes_ReturnsCorrectHashesForAll300Blocks()
    {
        // Produce 310 blocks to exceed a test threshold
        const int targetBlocks = 310;
        for (int i = 0; i < targetBlocks; i++)
            await ProduceBlockWithBaseFee(1.Wei);

        // Get the current head block
        Block? head = _chain.BlockTree.Head;
        head.Should().NotBeNull();
        head!.Number.Should().BeGreaterThan(300);

        // Forward loop from block 0 to head, matching Nitro's test logic
        // See: arbitrum-nitro/system_tests/historical_block_hash_test.go
        for (ulong i = 0; i < (ulong)head.Number; i++)
        {
            // Calculate the storage index for this block number
            UInt256 storageIndex = new(i % (ulong)_chain.ChainSpec.Parameters.Eip2935RingBufferSize);

            // Query history storage contract via RPC
            ResultWrapper<byte[]> result = _chain.ArbitrumEthRpcModule.eth_getStorageAt(
                Eip2935Constants.BlockHashHistoryAddress,
                storageIndex,
                BlockParameter.Latest);

            result.Result.ResultType.Should().Be(ResultType.Success,
                $"Block {i}: storage query should succeed");

            // Get expected block by number
            Block? expectedBlock = _chain.BlockTree.FindBlock((long)i);
            expectedBlock.Should().NotBeNull($"Block {i} should exist");

            // Verify stored hash matches block i's hash
            result.Data.Should().NotBeNullOrEmpty($"Block {i}: storage should contain hash");
            Hash256 storedHash = new(result.Data!);
            storedHash.Should().Be(expectedBlock!.Hash!,
                $"Block {i}: stored hash should match actual block hash");
        }
    }

    [Test]
    public void EthPendingTransactions_WhenCalled_ReturnsEmptyArray()
    {
        ResultWrapper<TransactionForRpc[]> result = _chain.ArbitrumEthRpcModule.eth_pendingTransactions();

        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().BeEmpty();
    }

    [Test]
    public async Task EthPendingTransactions_AfterDigestingTransaction_ReturnsEmptyArray()
    {
        await ProduceBlockWithBaseFee(100.Wei);

        Transaction tx = Build.A.Transaction
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(10.Wei)
            .WithGasLimit(Transaction.BaseTxGasCost)
            .WithGasPrice(100.Wei)
            .WithNonce(0)
            .SignedAndResolved(_ethereumEcdsa, FullChainSimulationAccounts.AccountA)
            .TestObject;

        TestL2Transactions l2Txs = new(
            TestItem.KeccakD,
            100.Wei,
            FullChainSimulationAccounts.AccountA.Address,
            tx
        );
        await _chain.Digest(l2Txs);

        ResultWrapper<TransactionForRpc[]> result = _chain.ArbitrumEthRpcModule.eth_pendingTransactions();

        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().BeEmpty();
    }

    [Test]
    public async Task EthGetTransactionCount_Pending_ReturnsStateNonce()
    {
        await ProduceBlockWithBaseFee(100.Wei);

        Address account = FullChainSimulationAccounts.AccountA.Address;
        UInt256 expectedNonce = _chain.WorldStateAccessor.GetNonce(account);

        ResultWrapper<UInt256> result = await _chain.ArbitrumEthRpcModule.eth_getTransactionCount(account, BlockParameter.Pending);

        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().Be(expectedNonce);
    }

    [Test]
    public async Task EthGetTransactionCount_Latest_DelegatesToBase()
    {
        await ProduceBlockWithBaseFee(100.Wei);

        Address account = FullChainSimulationAccounts.AccountA.Address;
        UInt256 expectedNonce = _chain.WorldStateAccessor.GetNonce(account);

        ResultWrapper<UInt256> result = await _chain.ArbitrumEthRpcModule.eth_getTransactionCount(account, BlockParameter.Latest);

        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().Be(expectedNonce);
    }

    [Test]
    public async Task EthGetTransactionCount_SpecificBlock_DelegatesToBase()
    {
        await ProduceBlockWithBaseFee(100.Wei);

        Address account = FullChainSimulationAccounts.AccountA.Address;
        long blockNumber = _chain.BlockTree.Head!.Number;

        ResultWrapper<UInt256> result = await _chain.ArbitrumEthRpcModule.eth_getTransactionCount(account, new BlockParameter(blockNumber));

        result.Result.ResultType.Should().Be(ResultType.Success);
    }

    [Test]
    public void MaxPriorityFeePerGas_WhenCalled_ReturnsZero()
    {
        ResultWrapper<UInt256?> result = _chain.ArbitrumEthRpcModule.eth_maxPriorityFeePerGas();

        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().Be(UInt256.Zero);
    }

    [Test]
    public async Task FeeHistory_WithRewardPercentiles_ReturnsZeroRewards()
    {
        await ProduceBlockWithBaseFee(100.Wei);
        await ProduceBlockWithBaseFee(200.Wei);
        await ProduceBlockWithBaseFee(300.Wei);

        ResultWrapper<FeeHistoryResults> result = _chain.ArbitrumEthRpcModule.eth_feeHistory(3, BlockParameter.Latest, [25, 50, 75]);

        result.Result.ResultType.Should().Be(ResultType.Success);
        using FeeHistoryResults data = result.Data;
        data.Reward.Should().NotBeNull();
        data.Reward.Should().HaveCount(3);
        foreach (ArrayPoolList<UInt256> blockRewards in data.Reward!)
        {
            blockRewards.Should().HaveCount(3);
            blockRewards.Should().OnlyContain(r => r == UInt256.Zero);
        }
    }

    [Test]
    public async Task FeeHistory_GasUsedRatio_WithinValidRange()
    {
        await ProduceBlockWithBaseFee(100.Wei);
        await ProduceBlockWithBaseFee(200.Wei);

        ResultWrapper<FeeHistoryResults> result = _chain.ArbitrumEthRpcModule.eth_feeHistory(2, BlockParameter.Latest);

        result.Result.ResultType.Should().Be(ResultType.Success);
        using FeeHistoryResults data = result.Data;
        data.GasUsedRatio.Should().HaveCount(2);
        foreach (double ratio in data.GasUsedRatio)
        {
            ratio.Should().BeGreaterThanOrEqualTo(0.0);
            ratio.Should().BeLessThanOrEqualTo(1.0);
        }
    }

    [Test]
    public async Task FeeHistory_WhenBlockCountExceedsChain_ClipsToAvailableBlocks()
    {
        await ProduceBlockWithBaseFee(100.Wei);
        await ProduceBlockWithBaseFee(200.Wei);

        long headNumber = _chain.BlockTree.Head!.Number;

        ResultWrapper<FeeHistoryResults> result = _chain.ArbitrumEthRpcModule.eth_feeHistory(1000, BlockParameter.Latest);

        result.Result.ResultType.Should().Be(ResultType.Success);
        using FeeHistoryResults data = result.Data;
        data.OldestBlock.Should().Be(0);
        data.GasUsedRatio.Should().HaveCount((int)(headNumber + 1));
        data.BaseFeePerGas.Should().HaveCount((int)(headNumber + 2));
    }

    [Test]
    public async Task EthGetBlockByNumber_WhenCalled_IncludesArbitrumHeaderFields()
    {
        await ProduceBlockWithBaseFee(100.Wei);

        Block head = _chain.BlockTree.Head!;
        ArbitrumBlockHeaderInfo expectedInfo = ArbitrumBlockHeaderInfo.Deserialize(head.Header, TestLogger);

        ResultWrapper<BlockForRpc> result = _chain.ArbitrumEthRpcModule.eth_getBlockByNumber(BlockParameter.Latest, false);

        AssertArbitrumBlockFields(result, expectedInfo);
    }

    [Test]
    public async Task EthGetBlockByHash_WhenCalled_IncludesArbitrumHeaderFields()
    {
        await ProduceBlockWithBaseFee(100.Wei);

        Block head = _chain.BlockTree.Head!;
        ArbitrumBlockHeaderInfo expectedInfo = ArbitrumBlockHeaderInfo.Deserialize(head.Header, TestLogger);

        ResultWrapper<BlockForRpc> result = _chain.ArbitrumEthRpcModule.eth_getBlockByHash(head.Hash!, false);

        AssertArbitrumBlockFields(result, expectedInfo);
    }

    private static void AssertArbitrumBlockFields(ResultWrapper<BlockForRpc> result, ArbitrumBlockHeaderInfo expectedInfo)
    {
        result.Result.ResultType.Should().Be(ResultType.Success);
        ArbitrumBlockForRpc blockForRpc = result.Data.Should().BeOfType<ArbitrumBlockForRpc>().Subject;
        blockForRpc.L1BlockNumber.Should().Be(expectedInfo.L1BlockNumber);
        blockForRpc.SendRoot.Should().Be(expectedInfo.SendRoot);
        blockForRpc.SendCount.Should().Be(expectedInfo.SendCount);
    }

    private async Task ProduceBlockWithBaseFee(UInt256 baseFee)
    {
        TestEthDeposit deposit = new(
            TestItem.KeccakA,
            baseFee,
            FullChainSimulationAccounts.Owner.Address, // Any random account
            FullChainSimulationAccounts.AccountA.Address,
            1.Ether
        );

        ResultWrapper<MessageResult> result = await _chain.Digest(deposit);
        result.Result.ResultType.Should().Be(ResultType.Success);
    }

    private async Task<Address> DeployTestContract()
    {
        // Simple storage contract bytecode (just stores and retrieves values)
        byte[] bytecode = Bytes.FromHexString("0x608060405234801561001057600080fd5b5060405161011a38038061011a8339818101604052602081101561003357600080fd5b505160005560c0806100446000396000f3fe608060405260043610601f5760003560e01c806306fdde031460245780630a9059cbb14604e575b600080fd5b602a6054565b6040518082815260200191505060405180910390f35b6064607c565b6040518082815260200191505060405180910390f35b60005481565b600055565b6000548156fea26469706673582212207f2d42b9b2c2b4c02ed89d63dd13d2a13637c6ac476fc113e6c4a3e33f948f79c64736f6c63430008000033");

        TestEthDeposit deposit = new(
            TestItem.KeccakB,
            100.Wei,
            FullChainSimulationAccounts.AccountA.Address,
            FullChainSimulationAccounts.AccountA.Address,
            10.Ether
        );
        await _chain.Digest(deposit);

        Transaction deployTx = Build.A.Transaction
            .WithTo(null)
            .WithData(bytecode)
            .WithGasLimit(1000000)
            .WithGasPrice(100.Wei)
            .WithValue(0)
            .WithNonce(0)
            .SignedAndResolved(_ethereumEcdsa, FullChainSimulationAccounts.AccountA)
            .TestObject;

        TestL2Transactions l2Txs = new(
            TestItem.KeccakC,
            100.Wei,
            FullChainSimulationAccounts.AccountA.Address,
            deployTx
        );

        await _chain.Digest(l2Txs);

        return ContractAddress.From(FullChainSimulationAccounts.AccountA.Address, 0);
    }
}
