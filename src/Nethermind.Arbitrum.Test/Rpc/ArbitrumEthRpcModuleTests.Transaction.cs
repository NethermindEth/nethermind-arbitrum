// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Modules;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain.Find;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Facade.Eth;
using Nethermind.Facade.Eth.RpcTransaction;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Data;

namespace Nethermind.Arbitrum.Test.Rpc;

public partial class ArbitrumEthRpcModuleTests
{
    [Test]
    public async Task EthGetBlockByNumber_WithArbitrumDepositTransaction_ReturnsBlockWithTransaction()
    {
        Hash256 requestId = TestItem.KeccakA;
        TestEthDeposit deposit = new(
            requestId,
            100.Wei(),
            TestItem.AddressA,
            TestItem.AddressB,
            1.Ether()
        );

        await _chain.Digest(deposit);

        BlockParameter blockParam = new(_chain.BlockTree.Head!.Number);
        ResultWrapper<BlockForRpc> result = _chain.ArbitrumEthRpcModule.eth_getBlockByNumber(blockParam, true);

        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().NotBeNull();
        result.Data!.Transactions.Should().NotBeEmpty();

        TransactionForRpc? tx = result.Data.Transactions![1] as TransactionForRpc;
        tx.Should().NotBeNull();
        tx!.Type.Should().Be((TxType)ArbitrumTxType.ArbitrumDeposit);
    }

    [Test]
    public async Task EthGetBlockByNumber_WithFullTransactions_IncludesArbitrumSpecificFields()
    {
        TestEthDeposit deposit = new(
            TestItem.KeccakA,
            100.Wei(),
            TestItem.AddressA,
            TestItem.AddressB,
            1.Ether()
        );

        await _chain.Digest(deposit);

        BlockParameter blockParam = new(_chain.BlockTree.Head!.Number);
        ResultWrapper<BlockForRpc> result = _chain.ArbitrumEthRpcModule.eth_getBlockByNumber(blockParam, true);

        result.Result.ResultType.Should().Be(ResultType.Success);

        ArbitrumDepositTransactionForRpc? depositTx = result.Data!.Transactions![1] as ArbitrumDepositTransactionForRpc;
        depositTx.Should().NotBeNull();
        depositTx!.RequestId.Should().Be(TestItem.KeccakA);
        depositTx.From.Should().Be(TestItem.AddressA);
        depositTx.To.Should().Be(TestItem.AddressB);
        depositTx.Value.Should().Be(1.Ether());
    }

    [Test]
    public async Task EthGetBlockByNumber_WithTransactionHashes_DoesNotThrow()
    {
        TestEthDeposit deposit = new(
            TestItem.KeccakA,
            100.Wei(),
            TestItem.AddressA,
            TestItem.AddressB,
            1.Ether()
        );

        await _chain.Digest(deposit);

        BlockParameter blockParam = new(_chain.BlockTree.Head!.Number);
        ResultWrapper<BlockForRpc> result = _chain.ArbitrumEthRpcModule.eth_getBlockByNumber(blockParam, false);

        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().NotBeNull();
        result.Data!.Transactions.Should().NotBeEmpty();
        result.Data.Transactions![0].Should().BeOfType<Hash256>();
    }

    [Test]
    public async Task EthGetTransactionByHash_WithArbitrumDepositTransaction_ReturnsTransactionWithFields()
    {
        Hash256 requestId = TestItem.KeccakA;
        TestEthDeposit deposit = new(
            requestId,
            100.Wei(),
            TestItem.AddressA,
            TestItem.AddressB,
            1.Ether()
        );

        ResultWrapper<MessageResult> digestResult = await _chain.Digest(deposit);
        digestResult.Result.ResultType.Should().Be(ResultType.Success);

        Block block = _chain.BlockTree.FindBlock(_chain.BlockTree.Head!.Number)!;

        Hash256 txHash = block.Transactions[1].Hash!;

        ResultWrapper<TransactionForRpc?> result = _chain.ArbitrumEthRpcModule.eth_getTransactionByHash(txHash);

        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().NotBeNull();

        ArbitrumDepositTransactionForRpc depositTx = result.Data.Should().BeOfType<ArbitrumDepositTransactionForRpc>().Subject;
        depositTx.RequestId.Should().Be(requestId);
        depositTx.Hash.Should().Be(txHash);
    }

    [Test]
    public async Task EthGetTransactionByBlockNumberAndIndex_WithArbitrumTransaction_ReturnsTransaction()
    {
        TestEthDeposit deposit = new(
            TestItem.KeccakA,
            100.Wei(),
            TestItem.AddressA,
            TestItem.AddressB,
            1.Ether()
        );

        await _chain.Digest(deposit);

        BlockParameter blockParam = new(_chain.BlockTree.Head!.Number);

        ResultWrapper<TransactionForRpc> result = _chain.ArbitrumEthRpcModule.eth_getTransactionByBlockNumberAndIndex(blockParam, 1);

        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().NotBeNull();
        result.Data!.Type.Should().Be((TxType)ArbitrumTxType.ArbitrumDeposit);
    }

    [Test]
    public async Task EthGetTransactionByBlockHashAndIndex_WithArbitrumTransaction_ReturnsTransaction()
    {
        TestEthDeposit deposit = new(
            TestItem.KeccakA,
            100.Wei(),
            TestItem.AddressA,
            TestItem.AddressB,
            1.Ether()
        );

        await _chain.Digest(deposit);

        Hash256 blockHash = _chain.BlockTree.Head!.Hash!;
        ResultWrapper<TransactionForRpc> result = _chain.ArbitrumEthRpcModule.eth_getTransactionByBlockHashAndIndex(blockHash, 0);

        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().NotBeNull();
        result.Data!.BlockHash.Should().Be(blockHash);
    }

    [Test]
    public async Task EthGetBlockByNumber_WithMultipleArbitrumTransactions_ReturnsAllTransactions()
    {
        UInt256[] depositValues = new[] { 1.Ether(), 2.Ether(), 3.Ether() };
        UInt256[] baseFees = new[] { 100.Wei(), 200.Wei(), 300.Wei() };

        for (int i = 0; i < 3; i++)
        {
            TestEthDeposit deposit = new(
                new Hash256(Enumerable.Repeat((byte)i, 32).ToArray()),
                baseFees[i],
                TestItem.AddressA,
                TestItem.AddressB,
                depositValues[i]
            );

            await _chain.Digest(deposit);
        }

        BlockParameter blockParam = new(_chain.BlockTree.Head!.Number);
        ResultWrapper<BlockForRpc> result = _chain.ArbitrumEthRpcModule.eth_getBlockByNumber(blockParam, true);

        result.Result.ResultType.Should().Be(ResultType.Success);

        result.Data!.Transactions.Should().HaveCount(2);
        result.Data.Transactions![0].Should().BeOfType<ArbitrumInternalTransactionForRpc>();
        result.Data.Transactions![1].Should().BeOfType<ArbitrumDepositTransactionForRpc>();
    }

    [Test]
    public async Task EthGetTransactionReceipt_ForArbitrumTransaction_ReturnsReceipt()
    {
        TestEthDeposit deposit = new(
            TestItem.KeccakA,
            100.Wei(),
            TestItem.AddressA,
            TestItem.AddressB,
            1.Ether()
        );

        await _chain.Digest(deposit);

        Block block = _chain.BlockTree.FindBlock(_chain.BlockTree.Head!.Number)!;

        Hash256 txHash = block.Transactions[1].Hash!;

        ResultWrapper<ReceiptForRpc?> result = _chain.ArbitrumEthRpcModule.eth_getTransactionReceipt(txHash);

        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().NotBeNull();
        result.Data!.TransactionHash.Should().Be(txHash);
        result.Data.Type.Should().Be((TxType)ArbitrumTxType.ArbitrumDeposit);
    }
}
