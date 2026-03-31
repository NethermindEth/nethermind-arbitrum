// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Autofac;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Sequencer.Queues;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.JsonRpc;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Test.Rpc;

public partial class ArbitrumEthRpcModuleTests
{
    [Test]
    public async Task EthSendRawTransactionConditional_ValidConditions_Enqueued()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();

        ulong headTimestamp = chain.BlockTree.Head!.Header.Timestamp;
        ConditionalOptions options = new() { TimestampMax = headTimestamp + 1000 };

        byte[] rlp = EncodeTx(chain);

        ResultWrapper<Hash256> result = await chain.ArbitrumEthRpcModule.eth_sendRawTransactionConditional(rlp, options);

        result.Should().RequestSucceed();

        TransactionQueue queue = chain.Container.Resolve<TransactionQueue>();
        List<TxQueueItem> drained = queue.DrainBatch();
        drained.Should().HaveCount(1);
        drained[0].Options.Should().BeSameAs(options);
    }

    [Test]
    public async Task EthSendRawTransactionConditional_InvalidConditions_RejectedEarly()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();

        ulong headTimestamp = chain.BlockTree.Head!.Header.Timestamp;
        ConditionalOptions options = new() { TimestampMax = headTimestamp - 1 };

        byte[] rlp = EncodeTx(chain);

        ResultWrapper<Hash256> result = await chain.ArbitrumEthRpcModule.eth_sendRawTransactionConditional(rlp, options);

        result.Should().RequestFail("TimestampMax condition not met");

        TransactionQueue queue = chain.Container.Resolve<TransactionQueue>();
        queue.DrainBatch().Should().BeEmpty();
    }

    [Test]
    public async Task EthSendRawTransactionConditional_InvalidRlp_Rejected()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        ResultWrapper<Hash256> result = await chain.ArbitrumEthRpcModule.eth_sendRawTransactionConditional([0xff, 0xfe], new ConditionalOptions());

        result.Should().RequestFail("Invalid RLP");
    }

    [Test]
    public async Task EthSendRawTransactionConditional_BlobType_Rejected()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        Transaction tx = Build.A.Transaction
            .WithType(TxType.Blob)
            .WithNonce(0)
            .WithGasLimit(21000)
            .WithMaxFeePerGas(1.GWei)
            .WithMaxPriorityFeePerGas(1.GWei)
            .WithTo(TestItem.AddressB)
            .WithChainId(chain.BlockTree.ChainId)
            .WithMaxFeePerBlobGas(1.GWei)
            .WithBlobVersionedHashes([new byte[32]])
            .SignedAndResolved(TestItem.PrivateKeyA)
            .TestObject;
        byte[] rlp = Rlp.Encode(tx).Bytes;

        ResultWrapper<Hash256> result = await chain.ArbitrumEthRpcModule.eth_sendRawTransactionConditional(rlp, new ConditionalOptions());

        result.Should().RequestFail("Invalid RLP");
    }

    [Test]
    public async Task EthSendRawTransactionConditional_Paused_Rejected()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();

        chain.NitroExecutionRpcModule.nitroexecution_pause().Should().RequestSucceed();

        ulong headTimestamp = chain.BlockTree.Head!.Header.Timestamp;
        ConditionalOptions options = new() { TimestampMax = headTimestamp + 1000 };

        byte[] rlp = EncodeTx(chain);

        ResultWrapper<Hash256> result = await chain.ArbitrumEthRpcModule.eth_sendRawTransactionConditional(rlp, options);

        result.Should().RequestFail("not available");
    }

    [Test]
    public async Task EthSendRawTransactionConditional_KnownAccountsRootMatch_Enqueued()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();

        // Prefunded EOA has EmptyTreeHash as storage root
        ConditionalOptions options = new()
        {
            KnownAccounts = new Dictionary<Address, AccountStateCondition>
            {
                [FullChainSimulationAccounts.AccountA.Address] = new() { RootHash = Keccak.EmptyTreeHash }
            }
        };

        byte[] rlp = EncodeTx(chain);

        ResultWrapper<Hash256> result = await chain.ArbitrumEthRpcModule.eth_sendRawTransactionConditional(rlp, options);

        result.Should().RequestSucceed();
    }

    [Test]
    public async Task EthSendRawTransactionConditional_KnownAccountsRootMismatch_Rejected()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();

        // Specifying a random hash that doesn't match the actual storage root
        ConditionalOptions options = new()
        {
            KnownAccounts = new Dictionary<Address, AccountStateCondition>
            {
                [FullChainSimulationAccounts.AccountA.Address] = new() { RootHash = TestItem.KeccakA }
            }
        };

        byte[] rlp = EncodeTx(chain);

        ResultWrapper<Hash256> result = await chain.ArbitrumEthRpcModule.eth_sendRawTransactionConditional(rlp, options);

        result.Should().RequestFail("Storage root hash condition not met");
    }

    private static byte[] EncodeTx(ArbitrumRpcTestBlockchain chain) =>
        Rlp.Encode(Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(TestItem.AddressB)
            .WithValue(1.Ether)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;
}
