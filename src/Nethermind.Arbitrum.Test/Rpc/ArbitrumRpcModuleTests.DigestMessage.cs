// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Abi;
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

    // ABI signatures for ArbAggregator methods
    private static readonly AbiSignature GetPreferredAggregatorSignature = new("getPreferredAggregator", AbiType.Address);
    private static readonly AbiSignature GetDefaultAggregatorSignature = new("getDefaultAggregator");
    private static readonly AbiSignature GetBatchPostersSignature = new("getBatchPosters");
    private static readonly AbiSignature GetFeeCollectorSignature = new("getFeeCollector", AbiType.Address);
    private static readonly AbiSignature GetTxBaseFeeSignature = new("getTxBaseFee", AbiType.Address);

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

        UInt256 balance = chain.WorldStateAccessor.GetBalance(receiver, chain.BlockTree.Head!.Header);
        balance.Should().Be(value);
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

        UInt256 initialSenderBalance = chain.WorldStateAccessor.GetBalance(sender, chain.BlockTree.Head!.Header);
        (initialSenderBalance / Unit.Ether).Should().Be(100); // Initially ~100 ETH

        TestSubmitRetryable retryable = new(requestId, L1BaseFee, sender, receiver, beneficiary, depositValue, retryValue, gasFee, gasLimit, maxSubmissionFee);
        ResultWrapper<MessageResult> result = await chain.Digest(retryable);
        result.Result.Should().Be(Result.Success);

        UInt256 receiverBalance = chain.WorldStateAccessor.GetBalance(receiver, chain.BlockTree.Head!.Header);
        (receiverBalance / Unit.Ether).Should().Be(10); // Receiver gets ~10 ETH

        UInt256 senderBalanceAfter = chain.WorldStateAccessor.GetBalance(sender, chain.BlockTree.Head!.Header);
        (senderBalanceAfter / Unit.Ether).Should().Be(110); // Sender has ~100 - 10 + 20 ETH
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

        UInt256 sponsorNonce = chain.WorldStateAccessor.GetNonce(sponsor, chain.BlockTree.Head!.Header);
        UInt256 sponsorBalanceBefore = chain.WorldStateAccessor.GetBalance(sponsor, chain.BlockTree.Head!.Header);

        ResultWrapper<MessageResult> result = await chain.Digest(new TestL2FundedByL1Transfer(requestId, L1BaseFee, sponsor, sender, receiver,
            transferValue, maxFeePerGas, gasLimit, sponsorNonce));

        result.Result.Should().Be(Result.Success);

        UInt256 sponsorBalanceAfter = chain.WorldStateAccessor.GetBalance(sponsor, chain.BlockTree.Head!.Header);
        UInt256 senderBalance = chain.WorldStateAccessor.GetBalance(sender, chain.BlockTree.Head!.Header);
        UInt256 receiverBalance = chain.WorldStateAccessor.GetBalance(receiver, chain.BlockTree.Head!.Header);

        sponsorBalanceAfter.Should().Be(sponsorBalanceBefore);
        senderBalance.Should().Be(0);
        receiverBalance.Should().Be(transferValue);
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

        AbiSignature signature = new("getBalance", AbiType.Address);
        byte[] calldata = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, signature, sponsor);

        UInt256 sponsorBalanceBefore = chain.WorldStateAccessor.GetBalance(sponsor, chain.BlockTree.Head!.Header);

        ResultWrapper<MessageResult> result = await chain.Digest(new TestL2FundedByL1Contract(requestId, L1BaseFee, sponsor, sender, contract,
            transferValue, maxFeePerGas, gasLimit, calldata));

        result.Result.Should().Be(Result.Success);

        UInt256 sponsorBalanceAfter = chain.WorldStateAccessor.GetBalance(sponsor, chain.BlockTree.Head!.Header);
        UInt256 senderBalance = chain.WorldStateAccessor.GetBalance(sender, chain.BlockTree.Head!.Header);
        TxReceipt[] receipts = chain.ReceiptStorage.Get(chain.BlockTree.Head!.Hash!);

        receipts.Should().HaveCount(3); // 3 transactions: internal, deposit, contract call
        receipts[2].GasUsedTotal.Should().Be(22938); // Contract call consumed gas

        sponsorBalanceAfter.Should().Be(sponsorBalanceBefore);
        senderBalance.Should().Be(0);
    }

    [Test]
    public async Task DigestMessage_L2MessageCallContract_CallsContract()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"))
            .Build();

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        Address sender = FullChainSimulationAccounts.Owner.Address;
        UInt256 nonce = chain.WorldStateAccessor.GetNonce(sender);

        AbiSignature signature = new("getBalance", AbiType.Address);
        byte[] calldata = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, signature, sender);

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
        UInt256 nonce = chain.WorldStateAccessor.GetNonce(sender, chain.BlockTree.Head!.Header);

        AbiSignature signature = new("addressExists", AbiType.Address);
        byte[] calldata = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, signature, FullChainSimulationAccounts.AccountA.Address);

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
        UInt256 nonce = chain.WorldStateAccessor.GetNonce(sender);

        AbiSignature registerSignature = new("register", AbiType.Address);
        byte[] calldata = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, registerSignature, FullChainSimulationAccounts.AccountA.Address);

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
        Address testAddress = FullChainSimulationAccounts.AccountA.Address;
        UInt256 registerNonce = chain.WorldStateAccessor.GetNonce(sender);

        AbiSignature registerSignature = new("register", AbiType.Address);
        byte[] registerCalldata = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, registerSignature, testAddress);

        Transaction registerTx = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(ArbosAddresses.ArbAddressTableAddress)
            .WithData(registerCalldata)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(GasCostOf.Transaction * 4)
            .WithNonce(registerNonce)
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

        await chain.Digest(new TestL2Transactions(requestId, L1BaseFee, sender, registerTx));

        // Now lookup the registered address
        UInt256 lookupNonce = chain.WorldStateAccessor.GetNonce(sender);

        AbiSignature lookupSignature = new("lookup", AbiType.Address);
        byte[] lookupCalldata = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, lookupSignature, testAddress);

        Transaction lookupTx = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(ArbosAddresses.ArbAddressTableAddress)
            .WithData(lookupCalldata)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(GasCostOf.Transaction * 2)
            .WithNonce(lookupNonce)
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
        UInt256 nonce = chain.WorldStateAccessor.GetNonce(sender);

        byte[] calldata = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, new("size"));

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
        UInt256 nonce = chain.WorldStateAccessor.GetNonce(sender);

        AbiSignature compressSignature = new("compress", AbiType.Address);
        byte[] calldata = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, compressSignature, FullChainSimulationAccounts.AccountA.Address);

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

    [Test]
    public async Task ArbAggregator_GetPreferredAggregator_ReturnsSuccessfulExecution()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"))
            .Build();

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        Address sender = FullChainSimulationAccounts.Owner.Address;
        UInt256 nonce = chain.WorldStateAccessor.GetNonce(sender);

        Address batchPoster = new(RandomNumberGenerator.GetBytes(Address.Size));

        // Call data to call getPreferredAggregator(address) on ArbAggregator precompile
        byte[] callData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, GetPreferredAggregatorSignature, batchPoster);

        Transaction transaction = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(ArbosAddresses.ArbAggregatorAddress)
            .WithData(callData)
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
        receipts[1].GasUsed.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task ArbAggregator_GetDefaultAggregator_ReturnsSuccessfulExecution()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"))
            .Build();

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        Address sender = FullChainSimulationAccounts.Owner.Address;
        UInt256 nonce = chain.WorldStateAccessor.GetNonce(sender);

        // Call data to call getDefaultAggregator() on ArbAggregator precompile (no parameters)
        byte[] callData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, GetDefaultAggregatorSignature);

        Transaction transaction = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(ArbosAddresses.ArbAggregatorAddress)
            .WithData(callData)
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
        receipts[1].GasUsed.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task ArbAggregator_GetBatchPosters_ReturnsSuccessfulExecution()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"))
            .Build();

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        Address sender = FullChainSimulationAccounts.Owner.Address;
        UInt256 nonce = chain.WorldStateAccessor.GetNonce(sender);

        // Call data to call getBatchPosters() on ArbAggregator precompile (no parameters)
        byte[] callData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, GetBatchPostersSignature);

        Transaction transaction = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(ArbosAddresses.ArbAggregatorAddress)
            .WithData(callData)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(GasCostOf.Transaction * 3) // More gas for array operations
            .WithNonce(nonce)
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

        ResultWrapper<MessageResult> result = await chain.Digest(new TestL2Transactions(requestId, L1BaseFee, sender, transaction));
        result.Result.Should().Be(Result.Success);

        TxReceipt[] receipts = chain.ReceiptStorage.Get(chain.BlockTree.Head!.Hash!);
        receipts.Should().HaveCount(2);

        receipts[1].StatusCode.Should().Be(1);
        receipts[1].GasUsed.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task ArbAggregator_GetFeeCollector_ReturnsSuccessfulExecution()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"))
            .Build();

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        Address sender = FullChainSimulationAccounts.Owner.Address;
        UInt256 nonce = chain.WorldStateAccessor.GetNonce(sender);

        Address batchPoster = ArbosAddresses.BatchPosterAddress; // Use default batch poster that exists

        // Call data to call getFeeCollector(address) on ArbAggregator precompile
        byte[] callData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, GetFeeCollectorSignature, batchPoster);

        Transaction transaction = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(ArbosAddresses.ArbAggregatorAddress)
            .WithData(callData)
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
        receipts[1].GasUsed.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task ArbAggregator_DeprecatedTxBaseFee_ReturnsZero()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"))
            .Build();

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        Address sender = FullChainSimulationAccounts.Owner.Address;
        UInt256 nonce = chain.WorldStateAccessor.GetNonce(sender);

        Address aggregator = new(RandomNumberGenerator.GetBytes(Address.Size));

        // Call data to call getTxBaseFee(address) on ArbAggregator precompile
        byte[] callData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, GetTxBaseFeeSignature, aggregator);

        Transaction transaction = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(ArbosAddresses.ArbAggregatorAddress)
            .WithData(callData)
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
        receipts[1].GasUsed.Should().BeGreaterThan(0);

        // The return data should be 32 bytes of zeros (UInt256 zero)
        receipts[1].Logs.Should().BeEmpty(); // No events expected for view functions
    }
}
