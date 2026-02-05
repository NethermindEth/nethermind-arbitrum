// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;

namespace Nethermind.Arbitrum.Test.Execution;

public class StylusLogTests
{
    [Test]
    public void EmitLog_ZeroTopics_LogEmittedCorrectly()
    {
        TestContext context = SetupTestContext();

        byte[] data = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        byte[] callData = LogContractCallData.CreateLogCallData([], data);

        Transaction tx = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(context.LogContractAddress)
            .WithData(callData)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(1_000_000)
            .WithNonce(context.Chain.WorldStateAccessor.GetNonce(context.Sender))
            .WithValue(0)
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

        context.Chain.Digest(new TestL2Transactions(context.Chain.InitialL1BaseFee, context.Sender, tx)).ShouldAsync()
            .RequestSucceed().And
            .TransactionStatusesBe(context.Chain, [StatusCode.Success, StatusCode.Success]);

        TxReceipt receipt = context.Chain.LatestReceipts()[1];
        receipt.Logs.Should().HaveCount(1, "Exactly one log should be emitted");

        LogEntry log = receipt.Logs![0];
        log.Address.Should().Be(context.LogContractAddress, "Log address should be the log contract");
        log.Topics.Should().BeEmpty("Zero topics were requested");
        log.Data.Should().BeEquivalentTo(data, "Log data should match input");

        context.Chain.BlockTree.Head!.GasUsed.Should().Be(37710, "Gas used should match expected value for zero-topic log");
    }

    [TestCase(1, 38717)]
    [TestCase(2, 39309)]
    [TestCase(3, 39902)]
    [TestCase(4, 40482)]
    public void EmitLog_OneToFourTopics_LogsEmittedCorrectly(int numTopics, long expectedGasUsed)
    {
        TestContext context = SetupTestContext();

        byte[] data = Enumerable.Range(0, 48).Select(i => (byte)i).ToArray();
        byte[] callData = LogContractCallData.CreateLogCallData(numTopics, data, out Hash256[] expectedTopics);

        Transaction tx = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(context.LogContractAddress)
            .WithData(callData)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(1_000_000)
            .WithNonce(context.Chain.WorldStateAccessor.GetNonce(context.Sender))
            .WithValue(0)
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

        context.Chain.Digest(new TestL2Transactions(context.Chain.InitialL1BaseFee, context.Sender, tx)).ShouldAsync()
            .RequestSucceed().And
            .TransactionStatusesBe(context.Chain, [StatusCode.Success, StatusCode.Success]);

        TxReceipt receipt = context.Chain.LatestReceipts()[1];
        receipt.Logs.Should().HaveCount(1, "Exactly one log should be emitted");

        LogEntry log = receipt.Logs![0];
        log.Address.Should().Be(context.LogContractAddress, "Log address should be the log contract");
        log.Topics.Should().HaveCount(numTopics, $"Expected {numTopics} topics");
        log.Topics.Should().BeEquivalentTo(expectedTopics, "Topics should match input");
        log.Data.Should().BeEquivalentTo(data, "Log data should match input");

        context.Chain.BlockTree.Head!.GasUsed.Should().Be(expectedGasUsed, $"Gas used should match expected value for {numTopics}-topics log");
    }

    [Test]
    public void EmitLog_FiveTopics_TransactionFails()
    {
        TestContext context = SetupTestContext();

        Hash256[] topics = new Hash256[5];
        for (int i = 0; i < 5; i++)
        {
            byte[] bytes = new byte[Hash256.Size];
            bytes[^1] = (byte)(i + 1);
            topics[i] = Keccak.Compute(bytes);
        }

        byte[] callData = LogContractCallData.CreateLogCallData(topics, []);

        Transaction tx = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(context.LogContractAddress)
            .WithData(callData)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(1_000_000)
            .WithNonce(context.Chain.WorldStateAccessor.GetNonce(context.Sender))
            .WithValue(0)
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

        context.Chain.Digest(new TestL2Transactions(context.Chain.InitialL1BaseFee, context.Sender, tx)).ShouldAsync()
            .RequestSucceed().And
            .TransactionStatusesBe(context.Chain, [StatusCode.Success, StatusCode.Failure]);

        TxReceipt receipt = context.Chain.LatestReceipts()[1];
        receipt.StatusCode.Should().Be(StatusCode.Failure, "Transaction with 5 topics should fail");
    }

    [Test]
    public void EmitLog_ViaDelegateCall_LogAddressIsCallerAddress()
    {
        TestContext context = SetupTestContext();

        byte[] logCallData = LogContractCallData.CreateLogCallData([], [0x00]);
        byte[] multicallData = MulticallCallData.CreateDelegateCall(context.LogContractAddress, logCallData);

        Transaction tx = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(context.MulticallContractAddress)
            .WithData(multicallData)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(2_000_000)
            .WithNonce(context.Chain.WorldStateAccessor.GetNonce(context.Sender))
            .WithValue(0)
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

        context.Chain.Digest(new TestL2Transactions(context.Chain.InitialL1BaseFee, context.Sender, tx)).ShouldAsync()
            .RequestSucceed().And
            .TransactionStatusesBe(context.Chain, [StatusCode.Success, StatusCode.Success]);

        TxReceipt receipt = context.Chain.LatestReceipts()[1];
        receipt.Logs.Should().NotBeEmpty("At least one log should be emitted");

        LogEntry log = receipt.Logs![0];
        log.Address.Should().Be(context.MulticallContractAddress,
            "When called via DELEGATECALL, log address should be the caller (multicall) address, not the log contract address");
    }

    private static TestContext SetupTestContext()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;

        chain.PrefundAccount(sender, 1000.Ether()).Should()
            .RequestSucceed().And
            .TransactionStatusesBe(chain, [StatusCode.Success, StatusCode.Success]);

        chain.DeployStylusContract(sender, "Arbos/Stylus/Resources/log.wat", out _, out Address logContractAddress).Should()
            .RequestSucceed().And
            .TransactionStatusesBe(chain, [StatusCode.Success, StatusCode.Success]);

        chain.ActivateStylusContract(sender, logContractAddress).Should()
            .RequestSucceed().And
            .TransactionStatusesBe(chain, [StatusCode.Success, StatusCode.Success]);

        chain.DeployStylusContract(sender, "Arbos/Stylus/Resources/multicall.wat", out _, out Address multicallContractAddress).Should()
            .RequestSucceed().And
            .TransactionStatusesBe(chain, [StatusCode.Success, StatusCode.Success]);

        chain.ActivateStylusContract(sender, multicallContractAddress).Should()
            .RequestSucceed().And
            .TransactionStatusesBe(chain, [StatusCode.Success, StatusCode.Success]);

        return new TestContext(chain, sender, logContractAddress, multicallContractAddress);
    }

    private sealed record TestContext(
        ArbitrumRpcTestBlockchain Chain,
        Address Sender,
        Address LogContractAddress,
        Address MulticallContractAddress);
}
