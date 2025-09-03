// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Int256;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Test.Rpc;

public class ArbitrumRpcModuleDigestMessageTests
{
    private static readonly UInt256 L1BaseFee = 92;

    [Test]
    public async Task DigestMessage_DepositEth_Deposits()
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

        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header))
        {
            UInt256 balance = chain.WorldStateManager.GlobalWorldState.GetBalance(receiver);
            balance.Should().Be(value);
        }
    }

    [Test]
    public async Task DigestMessage_SubmitRetryable_DepositsAndSends()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"))
            .Build();

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        Address sender = FullChainSimulationAccounts.Owner.Address;
        Address receiver = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address beneficiary = new(RandomNumberGenerator.GetBytes(Address.Size));

        UInt256 depositValue = 20.Ether(); // 10 ETH to deposit to sender
        UInt256 retryValue = 10.Ether(); // 10 ETH to send to retryTo

        ulong gasLimit = 21000;
        UInt256 gasFee = 1.GWei();

        UInt256 maxSubmissionFee = 128800;

        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header))
        {
            UInt256 initialSenderBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(sender);
            (initialSenderBalance / Unit.Ether).Should().Be(100); // Initially ~100 ETH
        }

        TestSubmitRetryable retryable = new(requestId, L1BaseFee, sender, receiver, beneficiary, depositValue, retryValue, gasFee, gasLimit, maxSubmissionFee);
        ResultWrapper<MessageResult> result = await chain.Digest(retryable);
        result.Result.Should().Be(Result.Success);

        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header))
        {
            UInt256 receiverBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(receiver);
            (receiverBalance / Unit.Ether).Should().Be(10); // Receiver gets ~10 ETH

            UInt256 senderBalanceAfter = chain.WorldStateManager.GlobalWorldState.GetBalance(sender);
            (senderBalanceAfter / Unit.Ether).Should().Be(110); // Sender has ~100 - 10 + 20 ETH
        }
    }

    [Test]
    public async Task DigestMessage_L2FundedByL1Unsigned_DepositsAndExecutes()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"))
            .Build();

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        Address sponsor = FullChainSimulationAccounts.Owner.Address;
        Address sender = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address receiver = new(RandomNumberGenerator.GetBytes(Address.Size));

        UInt256 transferValue = 10.Ether();

        UInt256 maxFeePerGas = 1.GWei(); // Fits the default BlockHeader.BaseFeePerGas = ArbosState.L2PricingState.BaseFeeWeiStorage
        ulong gasLimit = 21000;

        UInt256 nonce;
        UInt256 sponsorBalanceBefore;
        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header))
        {
            nonce = chain.WorldStateManager.GlobalWorldState.GetNonce(sponsor);
            sponsorBalanceBefore = chain.WorldStateManager.GlobalWorldState.GetBalance(sponsor);
        }

        ResultWrapper<MessageResult> result = await chain.Digest(new TestL2FundedByL1Transfer(requestId, L1BaseFee, sponsor, sender, receiver,
            transferValue, maxFeePerGas, gasLimit, nonce));

        result.Result.Should().Be(Result.Success);

        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header))
        {
            UInt256 sponsorBalanceAfter = chain.WorldStateManager.GlobalWorldState.GetBalance(sponsor);
            UInt256 senderBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(sender);
            UInt256 receiverBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(receiver);

            sponsorBalanceAfter.Should().Be(sponsorBalanceBefore);
            senderBalance.Should().Be(0);
            receiverBalance.Should().Be(transferValue);
        }
    }

    [Test]
    public async Task DigestMessage_L2FundedByL1Contract_DepositsAndExecutes()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"))
            .Build();

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        Address sponsor = FullChainSimulationAccounts.Owner.Address;
        Address sender = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address contract = ArbosAddresses.ArbInfoAddress;

        UInt256 transferValue = 10.Ether();

        UInt256 maxFeePerGas = 1.GWei(); // Fits the default BlockHeader.BaseFeePerGas = ArbosState.L2PricingState.BaseFeeWeiStorage
        ulong gasLimit = GasCostOf.Transaction * 2;

        // Calldata to call getBalance(address) on ArbInfo precompile
        byte[] addressBytes = new byte[32];
        sponsor.Bytes.CopyTo(addressBytes, 12);
        byte[] calldata = [.. KeccakHash.ComputeHashBytes("getBalance(address)"u8)[..4], .. addressBytes];

        UInt256 sponsorBalanceBefore = UInt256.Zero;
        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header))
        {
            sponsorBalanceBefore = chain.WorldStateManager.GlobalWorldState.GetBalance(sponsor);
        }

        ResultWrapper<MessageResult> result = await chain.Digest(new TestL2FundedByL1Contract(requestId, L1BaseFee, sponsor, sender, contract,
            transferValue, maxFeePerGas, gasLimit, calldata));

        result.Result.Should().Be(Result.Success);

        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header))
        {
            UInt256 sponsorBalanceAfter = chain.WorldStateManager.GlobalWorldState.GetBalance(sponsor);
            UInt256 senderBalance = chain.WorldStateManager.GlobalWorldState.GetBalance(sender);
            TxReceipt[] receipts = chain.ReceiptStorage.Get(chain.BlockTree.Head!.Hash!);

            receipts.Should().HaveCount(3); // 3 transactions: internal, deposit, contract call
            receipts[2].GasUsedTotal.Should().Be(22938); // Contract call consumed gas

            sponsorBalanceAfter.Should().Be(sponsorBalanceBefore);
            senderBalance.Should().Be(0);
        }
    }

    [Test]
    public async Task DigestMessage_L2MessageCallContract_CallsContract()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"))
            .Build();

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        Address sender = FullChainSimulationAccounts.Owner.Address;

        UInt256 nonce;
        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header))
        {
            nonce = chain.WorldStateManager.GlobalWorldState.GetNonce(sender);
        }

        // Calldata to call getBalance(address) on ArbInfo precompile
        byte[] addressBytes = new byte[32];
        sender.Bytes.CopyTo(addressBytes, 12);
        byte[] calldata = [.. KeccakHash.ComputeHashBytes("getBalance(address)"u8)[..4], .. addressBytes];

        Transaction transaction = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(ArbosAddresses.ArbInfoAddress)
            .WithData(calldata)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(GasCostOf.Transaction * 2)
            .WithNonce(nonce)
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

        ResultWrapper<MessageResult> result = await chain.Digest(new TestL2Transactions(requestId, L1BaseFee, sender, transaction));
        result.Result.Should().Be(Result.Success);

        TxReceipt[] receipts = chain.ReceiptStorage.Get(chain.BlockTree.Head!.Hash!);
        receipts.Should().HaveCount(2);

        receipts[1].GasUsedTotal.Should().Be(22938); // Yeah, it's magic number. Good enough for now to prove execution.
    }

    [Test]
    public async Task DigestMessage_L2FundedByL1WithLowMaxFeePerGas_HandlesEIP1559UnderflowCorrectly()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"))
            .Build();

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        Address sponsor = FullChainSimulationAccounts.Owner.Address;
        Address sender = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address receiver = new(RandomNumberGenerator.GetBytes(Address.Size));

        UInt256 transferValue = Unit.Ether / 2; // 0.5 ETH
        ulong gasLimit = 21000;

        UInt256 maxFeePerGas = 128800;

        ResultWrapper<MessageResult> result = await chain.Digest(new TestL2FundedByL1Transfer(requestId, L1BaseFee, sponsor, sender, receiver,
            transferValue, maxFeePerGas, gasLimit, 0));

        result.Should().NotBeNull("EIP1559 underflow should be handled gracefully without throwing exceptions");
    }

    [Test]
    public async Task AddressExists_WithUnregisteredAddress_ReturnsSuccessfulExecution()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"))
            .Build();

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        Address sender = FullChainSimulationAccounts.Owner.Address;
        UInt256 nonce = chain.WorldStateManager.GlobalWorldState.GetNonce(sender);

        Address testAddress = new(RandomNumberGenerator.GetBytes(Address.Size));

        // Calldata to call addressExists(address) on ArbAddressTable precompile
        byte[] addressBytes = new byte[32];
        testAddress.Bytes.CopyTo(addressBytes, 12);
        byte[] calldata = [.. KeccakHash.ComputeHashBytes("addressExists(address)"u8)[..4], .. addressBytes];

        Transaction transaction = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(ArbosAddresses.ArbAddressTableAddress)
            .WithData(calldata)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(GasCostOf.Transaction * 2)
            .WithNonce(nonce)
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

        ResultWrapper<MessageResult> result = await chain.Digest(new TestL2Transactions(requestId, L1BaseFee, sender, transaction));
        result.Result.Should().Be(Result.Success);

        TxReceipt[] receipts = chain.ReceiptStorage.Get(chain.BlockTree.Head!.Hash!);
        receipts.Should().HaveCount(2);

        receipts[1].StatusCode.Should().Be(1);
        receipts[1].GasUsed.Should().Be(23038);
    }

    [Test]
    public async Task Register_WithNewAddress_ReturnsSuccessfulExecution()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"))
            .Build();

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        Address sender = FullChainSimulationAccounts.Owner.Address;
        UInt256 nonce = chain.WorldStateManager.GlobalWorldState.GetNonce(sender);

        Address testAddress = new(RandomNumberGenerator.GetBytes(Address.Size));

        // Calldata to call register(address) on ArbAddressTable precompile
        byte[] addressBytes = new byte[32];
        testAddress.Bytes.CopyTo(addressBytes, 12);
        byte[] calldata = [.. KeccakHash.ComputeHashBytes("register(address)"u8)[..4], .. addressBytes];

        Transaction transaction = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(ArbosAddresses.ArbAddressTableAddress)
            .WithData(calldata)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(GasCostOf.Transaction * 4) // Register needs more gas for storage operations
            .WithNonce(nonce)
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

        ResultWrapper<MessageResult> result = await chain.Digest(new TestL2Transactions(requestId, L1BaseFee, sender, transaction));
        result.Result.Should().Be(Result.Success);

        TxReceipt[] receipts = chain.ReceiptStorage.Get(chain.BlockTree.Head!.Hash!);
        receipts.Should().HaveCount(2);

        receipts[1].StatusCode.Should().Be(1);
        receipts[1].GasUsed.Should().Be(83838);
    }

    [Test]
    public async Task Lookup_WithRegisteredAddress_ReturnsSuccessfulExecution()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"))
            .Build();

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        Address sender = FullChainSimulationAccounts.Owner.Address;
        UInt256 nonce = chain.WorldStateManager.GlobalWorldState.GetNonce(sender);

        Address testAddress = new(RandomNumberGenerator.GetBytes(Address.Size));

        byte[] addressBytes = new byte[32];
        testAddress.Bytes.CopyTo(addressBytes, 12);
        byte[] registerCalldata = [.. KeccakHash.ComputeHashBytes("register(address)"u8)[..4], .. addressBytes];

        Transaction registerTx = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(ArbosAddresses.ArbAddressTableAddress)
            .WithData(registerCalldata)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(GasCostOf.Transaction * 4)
            .WithNonce(nonce)
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

        await chain.Digest(new TestL2Transactions(requestId, L1BaseFee, sender, registerTx));

        // Now lookup the registered address
        nonce = chain.WorldStateManager.GlobalWorldState.GetNonce(sender);
        byte[] lookupCalldata = [.. KeccakHash.ComputeHashBytes("lookup(address)"u8)[..4], .. addressBytes];

        Transaction lookupTx = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(ArbosAddresses.ArbAddressTableAddress)
            .WithData(lookupCalldata)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(GasCostOf.Transaction * 2)
            .WithNonce(nonce)
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

        Hash256 lookupRequestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        ResultWrapper<MessageResult> result = await chain.Digest(new TestL2Transactions(lookupRequestId, L1BaseFee, sender, lookupTx));
        result.Result.Should().Be(Result.Success);

        TxReceipt[] receipts = chain.ReceiptStorage.Get(chain.BlockTree.Head!.Hash!);
        receipts.Should().HaveCount(2);

        receipts[1].StatusCode.Should().Be(1);
        receipts[1].GasUsed.Should().Be(23038);
    }

    [Test]
    public async Task Size_WithAddressTable_ReturnsSuccessfulExecution()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"))
            .Build();

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        Address sender = FullChainSimulationAccounts.Owner.Address;
        UInt256 nonce = chain.WorldStateManager.GlobalWorldState.GetNonce(sender);

        // Calldata to call size() on ArbAddressTable precompile (no parameters)
        byte[] calldata = KeccakHash.ComputeHashBytes("size()"u8)[..4];

        Transaction transaction = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(ArbosAddresses.ArbAddressTableAddress)
            .WithData(calldata)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(GasCostOf.Transaction * 2)
            .WithNonce(nonce)
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

        ResultWrapper<MessageResult> result = await chain.Digest(new TestL2Transactions(requestId, L1BaseFee, sender, transaction));
        result.Result.Should().Be(Result.Success);

        TxReceipt[] receipts = chain.ReceiptStorage.Get(chain.BlockTree.Head!.Hash!);
        receipts.Should().HaveCount(2);

        receipts[1].StatusCode.Should().Be(1);
        receipts[1].GasUsed.Should().Be(22667);
    }

    [Test]
    public async Task Compress_WithAddress_ReturnsSuccessfulExecution()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"))
            .Build();

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        Address sender = FullChainSimulationAccounts.Owner.Address;
        UInt256 nonce = chain.WorldStateManager.GlobalWorldState.GetNonce(sender);

        Address testAddress = new(RandomNumberGenerator.GetBytes(Address.Size));

        // Calldata to call compress(address) on ArbAddressTable precompile
        byte[] addressBytes = new byte[32];
        testAddress.Bytes.CopyTo(addressBytes, 12);
        byte[] calldata = [.. KeccakHash.ComputeHashBytes("compress(address)"u8)[..4], .. addressBytes];

        Transaction transaction = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(ArbosAddresses.ArbAddressTableAddress)
            .WithData(calldata)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(GasCostOf.Transaction * 2)
            .WithNonce(nonce)
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

        ResultWrapper<MessageResult> result = await chain.Digest(new TestL2Transactions(requestId, L1BaseFee, sender, transaction));
        result.Result.Should().Be(Result.Success);

        TxReceipt[] receipts = chain.ReceiptStorage.Get(chain.BlockTree.Head!.Hash!);
        receipts.Should().HaveCount(2);

        receipts[1].StatusCode.Should().Be(1);
        receipts[1].GasUsed.Should().Be(23044);
    }
}
