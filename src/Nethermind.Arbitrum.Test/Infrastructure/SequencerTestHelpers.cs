// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Security.Cryptography;
using Autofac;
using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Modules;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Config;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.Logging;
using Nethermind.State;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public static class SequencerTestHelpers
{
    public static ArbitrumExecutionEngine CreateEngineWithSequencer(
        ArbitrumRpcTestBlockchain chain,
        out DelayedMessageQueue delayedMessageQueue,
        out TransactionQueue transactionQueue,
        string? useForwarder = null)
    {
        delayedMessageQueue = new DelayedMessageQueue();
        SequencerState sequencerState = new(LimboLogs.Instance);

        if (useForwarder is not null)
            sequencerState.ForwardTo(useForwarder);
        else
            sequencerState.Activate();

        ArbitrumExecutionEngine engine = new(
            chain.Container.Resolve<ArbitrumBlockTreeInitializer>(),
            chain.BlockTree,
            chain.BlockProductionTrigger,
            chain.ChainSpec,
            chain.SpecHelper,
            chain.LogManager,
            chain.CachedL1PriceData,
            chain.BlockProcessingQueue,
            chain.Container.Resolve<IArbitrumConfig>(),
            chain.Container.Resolve<IBlocksConfig>(),
            chain.Container.Resolve<IStateReader>());

        engine.InitializeSequencer(delayedMessageQueue, sequencerState);
        transactionQueue = engine.TransactionQueue!;

        return engine;
    }

    public static L1IncomingMessage CreateEthDepositMessage(
        Hash256 requestId, UInt256 l1BaseFee, Address sender, Address receiver, UInt256 value)
    {
        ArbitrumDepositTransaction deposit = new()
        {
            SourceHash = requestId,
            Nonce = UInt256.Zero,
            GasPrice = UInt256.Zero,
            DecodedMaxFeePerGas = UInt256.Zero,
            GasLimit = 0,
            IsOPSystemTransaction = false,
            Mint = value,
            ChainId = 412346,
            L1RequestId = requestId,
            Value = value,
            SenderAddress = sender,
            To = receiver
        };

        L1IncomingMessageHeader header = new(
            ArbitrumL1MessageKind.EthDeposit,
            sender,
            1,
            (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            requestId,
            l1BaseFee);

        byte[] l2Msg = NitroL2MessageSerializer.SerializeTransactions([deposit], header);

        return new L1IncomingMessage(header, l2Msg, null, null);
    }

    public static L1IncomingMessage CreateSubmitRetryableMessage(
        Hash256 requestId, UInt256 l1BaseFee, Address sender, Address receiver, Address beneficiary,
        UInt256 depositValue, UInt256 retryValue, UInt256 gasFee, ulong gasLimit, UInt256 maxSubmissionFee)
    {
        ArbitrumSubmitRetryableTransaction retryable = new()
        {
            SourceHash = requestId,
            Nonce = UInt256.Zero,
            GasPrice = UInt256.Zero,
            DecodedMaxFeePerGas = gasFee,
            GasLimit = (long)gasLimit,
            Value = 0,
            Data = Array.Empty<byte>(),
            IsOPSystemTransaction = false,
            Mint = depositValue,
            ChainId = 412346,
            RequestId = requestId,
            SenderAddress = sender,
            L1BaseFee = l1BaseFee,
            DepositValue = depositValue,
            GasFeeCap = gasFee,
            Gas = gasLimit,
            RetryTo = receiver,
            RetryValue = retryValue,
            Beneficiary = beneficiary,
            MaxSubmissionFee = maxSubmissionFee,
            FeeRefundAddr = beneficiary,
            RetryData = Array.Empty<byte>()
        };

        L1IncomingMessageHeader header = new(
            ArbitrumL1MessageKind.SubmitRetryable,
            sender,
            1,
            (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            requestId,
            l1BaseFee);

        byte[] l2Msg = NitroL2MessageSerializer.SerializeTransactions([retryable], header);

        return new L1IncomingMessage(header, l2Msg, null, null);
    }

    public static async Task FundAccountAsync(
        ArbitrumRpcTestBlockchain chain, ArbitrumExecutionEngine engine, Address recipient)
    {
        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        L1IncomingMessage depositMsg = CreateEthDepositMessage(requestId, 92, TestItem.AddressA, recipient, 10.Ether());

        ulong delayedMsgRead = chain.BlockTree.Head!.Header.Nonce;
        engine.EnqueueDelayedMessages([depositMsg], delayedMsgRead);

        ResultWrapper<StartSequencingResult> depositResult = await engine.StartSequencingAsync();
        depositResult.Result.Should().Be(Result.Success);
        engine.EndSequencing(null);
        await engine.AppendLastSequencedBlockAsync();
    }

    public static Transaction CreateUserTx(ulong nonce, Address to, UInt256 value)
    {
        return Build.A.Transaction
            .WithNonce(nonce)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei())
            .WithTo(to)
            .WithValue(value)
            .WithChainId(412346)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;
    }

    public static (ArbitrumRpcModule RpcModule, DelayedMessageQueue Queue) CreateRpcModuleWithSequencer(
        ArbitrumRpcTestBlockchain chain)
    {
        ArbitrumExecutionEngine engine = CreateEngineWithSequencer(
            chain, out DelayedMessageQueue delayedMessageQueue, out _);

        ArbitrumRpcModule rpcModule = new(engine);
        return (rpcModule, delayedMessageQueue);
    }

    public static void InitGenesis(ArbitrumRpcTestBlockchain chain, ArbitrumRpcModule rpcModule)
    {
        ResultWrapper<MessageResult> genesisResult = rpcModule.DigestInitMessage(
            FullChainSimulationInitMessage.CreateDigestInitMessage(92));
        genesisResult.Result.Should().Be(Result.Success, $"genesis init should succeed, error: {genesisResult.Result.Error}");
    }
}
