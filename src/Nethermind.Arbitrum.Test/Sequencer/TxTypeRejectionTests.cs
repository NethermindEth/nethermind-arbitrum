// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.JsonRpc;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Test.Sequencer;

[TestFixture]
public class TxTypeRejectionTests
{
    [Test]
    public void BlobTx_SubmittedViaRpc_Rejected()
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

        // Blob txs are rejected at the RLP decoder level (before the type check)
        // because Arbitrum doesn't support EIP-4844. Either way, the tx should not be accepted.
        byte[] blobTxBytes = Rlp.Encode(Build.A.Transaction
            .WithType(TxType.Blob)
            .WithNonce(0)
            .WithGasLimit(21000)
            .WithMaxFeePerGas(1.GWei)
            .WithMaxPriorityFeePerGas(1.GWei)
            .WithMaxFeePerBlobGas(1.GWei)
            .WithBlobVersionedHashes(1)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        ResultWrapper<Hash256> result = chain.ArbitrumEthRpcModule.eth_sendRawTransaction(blobTxBytes).GetAwaiter().GetResult();
        result.Result.Should().NotBe(Result.Success, "blob tx should be rejected");
    }

    [Test]
    public void ArbitrumDepositTx_SubmittedViaRpc_Rejected()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        ArbitrumDepositTransaction depositTx = new()
        {
            ChainId = (ulong)chain.BlockTree.ChainId,
            SenderAddress = FullChainSimulationAccounts.AccountA.Address,
            To = FullChainSimulationAccounts.AccountB.Address,
            Value = 1.Ether,
            L1RequestId = Keccak.Zero
        };

        // Use TxDecoder directly since ArbitrumDepositTransaction doesn't have a generic Rlp.Encode registration
        int length = TxDecoder.Instance.GetLength(depositTx, RlpBehaviors.AllowUnsigned);
        RlpStream stream = new(length);
        TxDecoder.Instance.Encode(stream, depositTx, RlpBehaviors.AllowUnsigned);
        byte[] depositTxBytes = stream.Data.ToArray()!;

        // ArbitrumDeposit txs are rejected at either the RLP decode level or the type check.
        // Either way, the tx must not be accepted into the queue.
        ResultWrapper<Hash256> result = chain.ArbitrumEthRpcModule.eth_sendRawTransaction(depositTxBytes).GetAwaiter().GetResult();
        result.Result.Should().NotBe(Result.Success, "deposit tx should be rejected at RPC boundary");
    }

    [Test]
    public void RegularTx_SubmittedViaRpc_Accepted()
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

        byte[] txBytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Ether)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        chain.ArbitrumEthRpcModule.eth_sendRawTransaction(txBytes)
            .ShouldAsync().RequestSucceed();
    }

    [Test]
    public void WhitelistEnabled_AuthorizedSender_Accepted()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
                c.SequencerSenderWhitelist = FullChainSimulationAccounts.AccountA.Address.ToString();
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();

        byte[] txBytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Ether)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        chain.ArbitrumEthRpcModule.eth_sendRawTransaction(txBytes)
            .ShouldAsync().RequestSucceed();
    }

    [Test]
    public void WhitelistEnabled_UnauthorizedSender_Rejected()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
                // Only AccountB is whitelisted, but AccountA submits
                c.SequencerSenderWhitelist = FullChainSimulationAccounts.AccountB.Address.ToString();
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();

        byte[] txBytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Ether)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        chain.ArbitrumEthRpcModule.eth_sendRawTransaction(txBytes)
            .ShouldAsync().RequestFail("not on the whitelist");
    }

    [Test]
    public void WhitelistEmpty_AllSendersAccepted_NoFiltering()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
                c.SequencerSenderWhitelist = "";
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();

        byte[] txBytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Ether)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        chain.ArbitrumEthRpcModule.eth_sendRawTransaction(txBytes)
            .ShouldAsync().RequestSucceed();
    }
}
