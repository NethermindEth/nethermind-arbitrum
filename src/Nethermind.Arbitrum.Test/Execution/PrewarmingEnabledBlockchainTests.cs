// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Test.Execution;

/// <summary>
/// Regression coverage for the prewarming-enabled configuration — the production default that the
/// test infrastructure otherwise keeps off. Without the PreBlockCaches clear after genesis ArbOS
/// state initialization (see ArbitrumGenesisStateInitializer.InitializeAndBuildGenesisBlock), genesis
/// processing reads stale pre-genesis state from the prewarmer cache and fails with
/// "ArbOS uninitialized".
/// </summary>
[TestFixture]
public class PrewarmingEnabledBlockchainTests
{
    private const ulong InitialArbosVersion = 32;
    private const ulong InitialBaseFee = 92;
    private static readonly UInt256 L1BaseFee = InitialBaseFee;

    [Test]
    public void Genesis_WithPrewarmingEnabled_ProcessesSuccessfully()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: InitialBaseFee, arbosVersion: InitialArbosVersion)
            .WithPrewarming()
            .Build();

        chain.BlockTree.Head.Should().NotBeNull();
        chain.BlockTree.Head!.Number.Should().Be((long)chain.GenesisBlockNumber);

        ulong arbosVersion = chain.WorldStateAccessor.UseArbosStorage(storage =>
            new ArbosStorageBackedULong(storage, ArbosStateOffsets.VersionOffset).Get());
        arbosVersion.Should().Be(InitialArbosVersion,
            "genesis processing must observe the committed ArbOS version, not a stale prewarmer cache entry");
    }

    [Test]
    public async Task FirstBlock_WithPrewarmingEnabled_ExecutesTransactions()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: InitialBaseFee, arbosVersion: InitialArbosVersion)
            .WithPrewarming()
            .Build();

        Address sender = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address receiver = new(RandomNumberGenerator.GetBytes(Address.Size));
        UInt256 value = 1000.Ether;

        ResultWrapper<MessageResult> result = await chain.Digest(new TestEthDeposit(
            new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));

        result.Should().RequestSucceed();
        chain.BlockTree.Head!.Number.Should().Be((long)chain.LatestL2BlockNumber);
        chain.WorldStateAccessor.GetBalance(receiver).Should().Be(value,
            "the deposit-touched balance must read back the committed value through the prewarmer-decorated state");
    }
}
