// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Modules;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Facade.Eth.RpcTransaction;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Rpc;

[TestFixture]
public class ArbitrumTransactionForRpcTests
{
    private const ulong TestChainId = 42161;
    private readonly Hash256 _testHash = TestItem.KeccakA;
    private readonly Address _testAddress = TestItem.AddressA;

    [Test]
    public void ArbitrumInternalTransaction_RoundTrip_PreservesAllFields()
    {
        ArbitrumInternalTransaction tx = new()
        {
            ChainId = TestChainId,
            Data = new byte[] { 0x01, 0x02, 0x03 }
        };

        TransactionForRpc rpcTx = TransactionForRpc.FromTransaction(tx, chainId: TestChainId);
        Transaction reconstructed = rpcTx.ToTransaction();

        reconstructed.Type.Should().Be(tx.Type);
        reconstructed.ChainId.Should().Be(TestChainId);

        reconstructed.Data.ToArray().Should().BeEquivalentTo(tx.Data.ToArray());
    }

    [Test]
    public void ArbitrumDepositTransaction_WithBlockContext_IncludesBlockData()
    {
        ArbitrumDepositTransaction tx = new()
        {
            ChainId = TestChainId,
            L1RequestId = _testHash,
            SenderAddress = _testAddress,
            To = TestItem.AddressB,
            Value = 100.Wei()
        };

        Hash256 blockHash = TestItem.KeccakB;
        long blockNumber = 42;
        int txIndex = 5;

        TransactionForRpc rpcTx = TransactionForRpc.FromTransaction(tx, blockHash, blockNumber, txIndex, chainId: TestChainId);

        ArbitrumDepositTransactionForRpc depositRpc = rpcTx.Should().BeOfType<ArbitrumDepositTransactionForRpc>().Subject;
        depositRpc.BlockHash.Should().Be(blockHash);
        depositRpc.BlockNumber.Should().Be(blockNumber);
        depositRpc.TransactionIndex.Should().Be(txIndex);
        depositRpc.RequestId.Should().Be(_testHash);
    }

    [Test]
    public void ArbitrumUnsignedTransaction_RoundTrip_PreservesGasFeeCap()
    {
        ArbitrumUnsignedTransaction tx = new()
        {
            ChainId = TestChainId,
            SenderAddress = _testAddress,
            Nonce = 42,
            GasFeeCap = 1000.Wei(),
            Gas = 21000,
            To = TestItem.AddressB,
            Value = 50.Wei(),
            Data = new byte[] { 0xaa, 0xbb }
        };

        TransactionForRpc rpcTx = TransactionForRpc.FromTransaction(tx, chainId: TestChainId);
        ArbitrumUnsignedTransaction? reconstructed = rpcTx.ToTransaction() as ArbitrumUnsignedTransaction;

        reconstructed.Should().NotBeNull();
        reconstructed!.GasFeeCap.Should().Be(1000.Wei());
        reconstructed.Gas.Should().Be(21000);
        reconstructed.GasLimit.Should().Be(21000);
    }

    [Test]
    public void ArbitrumRetryTransaction_RoundTrip_PreservesRefundFields()
    {
        ArbitrumRetryTransaction tx = new()
        {
            ChainId = TestChainId,
            TicketId = _testHash,
            SenderAddress = _testAddress,
            Nonce = 10,
            GasFeeCap = 500.Wei(),
            Gas = 50000,
            To = TestItem.AddressB,
            Value = 25.Wei(),
            Data = new byte[] { 0x12, 0x34 },
            RefundTo = TestItem.AddressC,
            MaxRefund = 100.Wei(),
            SubmissionFeeRefund = 10.Wei()
        };

        TransactionForRpc rpcTx = TransactionForRpc.FromTransaction(tx, chainId: TestChainId);
        ArbitrumRetryTransaction? reconstructed = rpcTx.ToTransaction() as ArbitrumRetryTransaction;

        reconstructed.Should().NotBeNull();
        reconstructed!.TicketId.Should().Be(_testHash);
        reconstructed.RefundTo.Should().Be(TestItem.AddressC);
        reconstructed.MaxRefund.Should().Be(100.Wei());
        reconstructed.SubmissionFeeRefund.Should().Be(10.Wei());
    }

    [Test]
    public void ArbitrumSubmitRetryableTransaction_RoundTrip_PreservesRetryData()
    {
        byte[] retryData = new byte[] { 0xff, 0xee, 0xdd };
        ArbitrumSubmitRetryableTransaction tx = new()
        {
            ChainId = TestChainId,
            RequestId = _testHash,
            SenderAddress = _testAddress,
            L1BaseFee = 20.Wei(),
            DepositValue = 1.Ether(),
            GasFeeCap = 200.Wei(),
            Gas = 100000,
            RetryTo = TestItem.AddressB,
            RetryValue = 50.Wei(),
            Beneficiary = TestItem.AddressC,
            MaxSubmissionFee = 5.Wei(),
            FeeRefundAddr = TestItem.AddressD,
            RetryData = retryData
        };

        TransactionForRpc rpcTx = TransactionForRpc.FromTransaction(tx, chainId: TestChainId);
        ArbitrumSubmitRetryableTransaction? reconstructed = rpcTx.ToTransaction() as ArbitrumSubmitRetryableTransaction;

        reconstructed.Should().NotBeNull();
        reconstructed!.RequestId.Should().Be(_testHash);
        reconstructed.L1BaseFee.Should().Be(20.Wei());
        reconstructed.DepositValue.Should().Be(1.Ether());
        reconstructed.RetryData.ToArray().Should().BeEquivalentTo(retryData);
        reconstructed.Beneficiary.Should().Be(TestItem.AddressC);
        reconstructed.FeeRefundAddr.Should().Be(TestItem.AddressD);
    }

    [Test]
    public void ArbitrumContractTransaction_RoundTrip_PreservesRequestId()
    {
        ArbitrumContractTransaction tx = new()
        {
            ChainId = TestChainId,
            RequestId = _testHash,
            SenderAddress = _testAddress,
            GasFeeCap = 300.Wei(),
            Gas = 75000,
            To = TestItem.AddressB,
            Value = 10.Wei(),
            Data = new byte[] { 0x00, 0x11 }
        };

        TransactionForRpc rpcTx = TransactionForRpc.FromTransaction(tx, chainId: TestChainId);
        ArbitrumContractTransaction? reconstructed = rpcTx.ToTransaction() as ArbitrumContractTransaction;

        reconstructed.Should().NotBeNull();
        reconstructed!.RequestId.Should().Be(_testHash);
        reconstructed.GasFeeCap.Should().Be(300.Wei());
        reconstructed.Gas.Should().Be(75000);
    }

    [Test]
    public void ArbitrumDepositTransaction_WithNullTo_PreservesNullValue()
    {
        ArbitrumDepositTransaction tx = new()
        {
            ChainId = TestChainId,
            L1RequestId = _testHash,
            SenderAddress = _testAddress,
            To = null,
            Value = 1.Ether()
        };

        TransactionForRpc rpcTx = TransactionForRpc.FromTransaction(tx, chainId: TestChainId);
        Transaction reconstructed = rpcTx.ToTransaction();

        reconstructed.To.Should().BeNull();
        reconstructed.IsContractCreation.Should().BeTrue();
    }

    [Test]
    public void ArbitrumInternalTransaction_WhenSerialized_ReturnsCorrectType()
    {
        ArbitrumInternalTransaction tx = new()
        {
            ChainId = TestChainId,
            Data = Array.Empty<byte>()
        };

        TransactionForRpc rpcTx = TransactionForRpc.FromTransaction(tx, chainId: TestChainId);

        rpcTx.Type.Should().Be((TxType)ArbitrumTxType.ArbitrumInternal);
        rpcTx.Should().BeOfType<ArbitrumInternalTransactionForRpc>();
    }

    [Test]
    public void ArbitrumRetryTransaction_WithZeroValues_PreservesZeros()
    {
        ArbitrumRetryTransaction tx = new()
        {
            ChainId = TestChainId,
            TicketId = Hash256.Zero,
            SenderAddress = Address.Zero,
            Nonce = 0,
            GasFeeCap = UInt256.Zero,
            Gas = 0,
            To = null,
            Value = UInt256.Zero,
            Data = Array.Empty<byte>(),
            RefundTo = Address.Zero,
            MaxRefund = UInt256.Zero,
            SubmissionFeeRefund = UInt256.Zero
        };

        TransactionForRpc rpcTx = TransactionForRpc.FromTransaction(tx, chainId: TestChainId);
        ArbitrumRetryTransaction? reconstructed = rpcTx.ToTransaction() as ArbitrumRetryTransaction;

        reconstructed.Should().NotBeNull();
        reconstructed!.TicketId.Should().Be(Hash256.Zero);
        reconstructed.Value.Should().Be(UInt256.Zero);
        reconstructed.MaxRefund.Should().Be(UInt256.Zero);
    }

    [Test]
    public void ArbitrumSubmitRetryableTransaction_WithEmptyRetryData_PreservesEmptyArray()
    {
        ArbitrumSubmitRetryableTransaction tx = new()
        {
            ChainId = TestChainId,
            RequestId = _testHash,
            SenderAddress = _testAddress,
            L1BaseFee = 10.Wei(),
            DepositValue = 1.Ether(),
            GasFeeCap = 100.Wei(),
            Gas = 50000,
            RetryTo = TestItem.AddressB,
            RetryValue = UInt256.Zero,
            Beneficiary = TestItem.AddressC,
            MaxSubmissionFee = 1.Wei(),
            FeeRefundAddr = TestItem.AddressD,
            RetryData = Array.Empty<byte>()
        };

        TransactionForRpc rpcTx = TransactionForRpc.FromTransaction(tx, chainId: TestChainId);
        ArbitrumSubmitRetryableTransaction? reconstructed = rpcTx.ToTransaction() as ArbitrumSubmitRetryableTransaction;

        reconstructed.Should().NotBeNull();
        reconstructed!.RetryData.ToArray().Should().BeEmpty();
    }
}
