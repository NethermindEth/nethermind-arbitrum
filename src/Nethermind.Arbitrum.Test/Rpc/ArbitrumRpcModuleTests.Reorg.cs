// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Modules;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Test.Rpc;

public class ArbitrumRpcModuleReorgTests
{
    private static readonly UInt256 L1BaseFee = 92;

    [Test]
    public async Task Reorg_DepositEth_Deposits()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"))
            .Build();

        Address sender = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address receiver = new(RandomNumberGenerator.GetBytes(Address.Size));
        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        UInt256 value = 1000.Ether();

        ResultWrapper<MessageResult> result = await chain.Digest(new TestEthDeposit(requestId, L1BaseFee, sender, receiver, value));
        result.Result.ResultType.Should().Be(ResultType.Success);
        UInt256 balance = chain.WorldStateManager.GlobalWorldState.GetBalance(receiver);
        balance.Should().Be(value);
        var messageToReorg = chain.LatestL2BlockIndex;

        requestId = new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size));
        result = await chain.Digest(new TestEthDeposit(requestId, L1BaseFee, sender, receiver, value));
        result.Result.ResultType.Should().Be(ResultType.Success);
        balance = chain.WorldStateManager.GlobalWorldState.GetBalance(receiver);
        balance.Should().Be(value * 2);

        requestId = new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size));
        result = await chain.Digest(new TestEthDeposit(requestId, L1BaseFee, sender, receiver, value));
        result.Result.ResultType.Should().Be(ResultType.Success);
        balance = chain.WorldStateManager.GlobalWorldState.GetBalance(receiver);
        balance.Should().Be(value * 3);

        requestId = new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size));
        ResultWrapper<MessageResult[]> resultReorg = await chain.Reorg(new TestEthDeposit(requestId, L1BaseFee, sender, receiver, value), messageToReorg);
        resultReorg.Result.ResultType.Should().Be(ResultType.Success);
        balance = chain.WorldStateManager.GlobalWorldState.GetBalance(receiver);
        balance.Should().Be(value);

        requestId = new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size));
        result = await chain.Digest(new TestEthDeposit(requestId, L1BaseFee, sender, receiver, value));
        result.Result.ResultType.Should().Be(ResultType.Success);
        balance = chain.WorldStateManager.GlobalWorldState.GetBalance(receiver);
        balance.Should().Be(value * 2);

        requestId = new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size));
        result = await chain.Digest(new TestEthDeposit(requestId, L1BaseFee, sender, receiver, value));
        result.Result.ResultType.Should().Be(ResultType.Success);
        balance = chain.WorldStateManager.GlobalWorldState.GetBalance(receiver);
        balance.Should().Be(value * 3);

        requestId = new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size));
        result = await chain.Digest(new TestEthDeposit(requestId, L1BaseFee, sender, receiver, value));
        result.Result.ResultType.Should().Be(ResultType.Success);
        balance = chain.WorldStateManager.GlobalWorldState.GetBalance(receiver);
        balance.Should().Be(value * 4);

    }
}
