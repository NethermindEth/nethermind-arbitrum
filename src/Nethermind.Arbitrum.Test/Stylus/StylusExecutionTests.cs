// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Db;
using Nethermind.Evm;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Trie;
using Nethermind.Trie.Pruning;

namespace Nethermind.Arbitrum.Test.Stylus;

public class StylusExecutionTests
{
    private const string SolidityCounterAddressInRecordingPath = "0x9df23e34ac13a7145eba1164660e701839197b1b";
    private const string SolidityCallAddressInRecordingPath = "0x9f1ece352ce8d540738ccb38aa3fa3d44d00a259";
    private const string StylusCounterAddressInRecordingPath = "0x0bdad990640a488400565fe6fb1d879ffe12da37";
    private const string StylusCallAddressInRecordingPath = "0xa75fbfe03ac01540e1e0b6c1a48a45f10c74daa7";
    private const string StylusCallAddressInRecordingPath3 = "0xe1080224b632a93951a7cfa33eeea9fd81558b5e";
    private const string StylusMsgSenderAddressInRecordingPath3 = "0x1294b86822ff4976bfe136cb06cf43ec7fcf2574";

    private static readonly string RecordingPath = "./Recordings/2__stylus.jsonl";
    private static readonly string RecordingPath3 = "./Recordings/3__stylus.jsonl";
    private static readonly UInt256 L1BaseFee = 13;

    private static readonly byte[] CounterIncrementCalldata = KeccakHash.ComputeHashBytes("inc()"u8)[..4];
    private static readonly byte[] CounterGetCounterCalldata = KeccakHash.ComputeHashBytes("get()"u8)[..4];
    private static readonly byte[] CounterEmitCountCalldata = KeccakHash.ComputeHashBytes("emitCount()"u8)[..4];
    private static readonly byte[] CounterLogCountEventData = KeccakHash.ComputeHashBytes("LogCount(uint256)"u8);

    private static readonly AbiSignature ExecuteCallSignature = new("executeCall", AbiType.Address, AbiType.DynamicBytes);
    private static readonly AbiSignature ExecuteStaticCallSignature = new("executeStaticCall", AbiType.Address, AbiType.DynamicBytes);
    private static readonly AbiSignature ExecuteDelegateCallSignature = new("executeDelegateCall", AbiType.Address, AbiType.DynamicBytes);

    [TestCase(SolidityCounterAddressInRecordingPath, 1, 19)]
    [TestCase(SolidityCounterAddressInRecordingPath, 3, 19)]
    [TestCase(StylusCounterAddressInRecordingPath, 1, 22)]
    [TestCase(StylusCounterAddressInRecordingPath, 3, 22)]
    public async Task CounterContract_IncrementNTimes_ReturnsIncrementedValue(string address, byte incrementLoops, byte contractBlock)
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile(RecordingPath), contractBlock)
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;
        Address contract = new(address);

        // Call increment N times
        for (byte i = 0; i < incrementLoops; i++)
        {
            Transaction incTransaction;
            using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head?.Header))
            {
                incTransaction = Build.A.Transaction
                    .WithType(TxType.EIP1559)
                    .WithTo(contract)
                    .WithData(CounterIncrementCalldata)
                    .WithMaxFeePerGas(10.GWei())
                    .WithGasLimit(500000)
                    .WithValue(0)
                    .WithNonce(chain.WorldStateManager.GlobalWorldState.GetNonce(sender))
                    .SignedAndResolved(FullChainSimulationAccounts.Owner)
                    .TestObject;
            }

            ResultWrapper<MessageResult> incResult = await chain.Digest(new TestL2Transactions(L1BaseFee, sender, incTransaction));
            incResult.Result.Should().Be(Result.Success);
            chain.LatestReceipts()[1].StatusCode.Should().Be(StatusCode.Success);
        }

        Transaction emitTransaction;
        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            emitTransaction = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(contract)
                .WithData(CounterEmitCountCalldata)
                .WithMaxFeePerGas(10.GWei())
                .WithGasLimit(50000)
                .WithValue(0)
                .WithNonce(chain.WorldStateManager.GlobalWorldState.GetNonce(sender))
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;
        }


        ResultWrapper<MessageResult> emitResult = await chain.Digest(new TestL2Transactions(L1BaseFee, sender, emitTransaction));
        emitResult.Result.Should().Be(Result.Success);
        chain.LatestReceipts()[1].StatusCode.Should().Be(StatusCode.Success);

        // Ensure the event was emitted with value N
        LogEntry logs = chain.LatestReceipts()[1].Logs![0];
        logs.Should().BeEquivalentTo(new LogEntry(contract, [], [
            new Hash256(CounterLogCountEventData),
            new Hash256(new UInt256(incrementLoops).ToValueHash())
        ]));
    }

    [TestCase(SolidityCallAddressInRecordingPath, SolidityCounterAddressInRecordingPath, 20)]
    [TestCase(SolidityCallAddressInRecordingPath, StylusCounterAddressInRecordingPath, 24)]
    [TestCase(StylusCallAddressInRecordingPath, SolidityCounterAddressInRecordingPath, 24)]
    [TestCase(StylusCallAddressInRecordingPath, StylusCounterAddressInRecordingPath, 24)]
    public async Task CallContract_CallCounterIncrement_ProxiesCallToCounterContract(string callAddress, string counterAddress, byte contractBlock)
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile(RecordingPath), contractBlock)
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;
        Address callContract = new(callAddress);
        Address counterContract = new(counterAddress);

        Transaction callTransaction, emitTransaction;

        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            // CALL increment through the Call contract
            callTransaction = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(callContract)
                .WithData(AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, ExecuteCallSignature, counterContract, CounterIncrementCalldata))
                .WithMaxFeePerGas(10.GWei())
                .WithGasLimit(500000)
                .WithValue(0)
                .WithNonce(chain.WorldStateManager.GlobalWorldState.GetNonce(sender))
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;
        }

        ResultWrapper<MessageResult> callResult = await chain.Digest(new TestL2Transactions(L1BaseFee, sender, callTransaction));
        callResult.Result.Should().Be(Result.Success);
        chain.LatestReceipts()[1].StatusCode.Should().Be(StatusCode.Success);

        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            // Emit counter's value
            emitTransaction = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(counterContract)
                .WithData(CounterEmitCountCalldata)
                .WithMaxFeePerGas(10.GWei())
                .WithGasLimit(500000)
                .WithValue(0)
                .WithNonce(chain.WorldStateManager.GlobalWorldState.GetNonce(sender))
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;
        }

        ResultWrapper<MessageResult> emitResult = await chain.Digest(new TestL2Transactions(L1BaseFee, sender, emitTransaction));
        emitResult.Result.Should().Be(Result.Success);
        chain.LatestReceipts()[1].StatusCode.Should().Be(StatusCode.Success);

        // Ensure the event was emitted with value 1
        LogEntry logs = chain.LatestReceipts()[1].Logs![0];
        logs.Should().BeEquivalentTo(new LogEntry(counterContract, [], [
            new Hash256(CounterLogCountEventData),
            new Hash256(new UInt256(1).ToValueHash())
        ]));
    }

    [TestCase(StylusCallAddressInRecordingPath, StylusCounterAddressInRecordingPath, 24)]
    public async Task CallContract_CallCounterIncrement_StorageRootIsCorrect(string callAddress, string counterAddress, byte contractBlock)
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile(RecordingPath), contractBlock)
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;
        Address callContract = new(callAddress);
        Address counterContract = new(counterAddress);

        Transaction callTransaction;

        //reference storage tree
        TrieStore memTrieStore = new(new NodeStorage(new MemDb()), NoPruning.Instance, Persist.EveryBlock, new PruningConfig(), LimboLogs.Instance);
        StorageTree storageTree = new(memTrieStore.GetTrieStore(counterContract), LimboLogs.Instance);
        storageTree.Set(0, [1]); //counter should be 1 after increment
        using (memTrieStore.BeginBlockCommit(contractBlock))
            storageTree.Commit();

        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            // CALL increment through the Call contract
            callTransaction = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(callContract)
                .WithData(AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, ExecuteCallSignature, counterContract, CounterIncrementCalldata))
                .WithMaxFeePerGas(10.GWei())
                .WithGasLimit(500000)
                .WithValue(0)
                .WithNonce(chain.WorldStateManager.GlobalWorldState.GetNonce(sender))
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;
        }

        ResultWrapper<MessageResult> callResult = await chain.Digest(new TestL2Transactions(L1BaseFee, sender, callTransaction));
        callResult.Result.Should().Be(Result.Success);
        chain.LatestReceipts()[1].StatusCode.Should().Be(StatusCode.Success);

        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            chain.WorldStateManager.GlobalWorldState.TryGetAccount(counterContract, out AccountStruct callAccountStruct);
            callAccountStruct.StorageRoot.Should().Be(storageTree.RootHash);
        }
    }

    [TestCase(StylusCallAddressInRecordingPath3, StylusMsgSenderAddressInRecordingPath3, 29)]
    public Task MsgSenderTest_RegularCall_StoresCorrectCaller(string proxyAddress, string msgSenderTestAddress, byte contractBlock)
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile(RecordingPath3), contractBlock)
            .Build();

        Address proxy = new(proxyAddress);
        Address msgSenderTest = new(msgSenderTestAddress);

        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            StorageCell cell = new(msgSenderTest, UInt256.Zero);
            ReadOnlySpan<byte> storedValue = chain.WorldStateManager.GlobalWorldState.Get(cell);

            Address storedSender = storedValue.Length >= 20
                ? new Address(storedValue.Slice(storedValue.Length - 20, 20).ToArray())
                : new Address(storedValue.PadLeft(20).ToArray());

            storedSender.Should().Be(proxy,
                "In a regular CALL, msg.sender should be the immediate caller (proxy address)");
        }

        return Task.CompletedTask;
    }

    // TODO: implement STATICCALL test when EthRpcModule support is added to Test Blockchain
    /*[TestCase(SolidityCallAddress, SolidityCounterAddress, 20)]
    [TestCase(StylusCallAddress, SolidityCounterAddress, 24)]
    public async Task CallContract_StaticCallCounterIncrement_ProxiesCallToCounterContract(string callAddress, string counterAddress, byte contractBlock)
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile(RecordingPath), contractBlock)
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;
        Address callContract = new(callAddress);
        Address counterContract = new(counterAddress);

        // Increment counter directly to 1
        Transaction incTransaction = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(counterContract)
            .WithData(CounterIncrementCalldata)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(50000)
            .WithValue(0)
            .WithNonce(chain.WorldStateManager.GlobalWorldState.GetNonce(sender))
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

        ResultWrapper<MessageResult> incResult = await chain.Digest(new TestL2Transactions(L1BaseFee, sender, incTransaction));
        incResult.Result.Should().Be(Result.Success);
        chain.LatestReceipts()[1].StatusCode.Should().Be(StatusCode.Success);

        // STATICCALL emit event through the Call contract
        byte[] calldata = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, ExecuteStaticCallSignature, counterContract, CounterGetCounterCalldata);

        Console.WriteLine(calldata.ToHexString());

        Transaction staticcallTransaction = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(callContract)
            .WithData(calldata)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(50000)
            .WithValue(0)
            .WithNonce(chain.WorldStateManager.GlobalWorldState.GetNonce(sender))
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

        ResultWrapper<MessageResult> emitResult = await chain.Digest(new TestL2Transactions(L1BaseFee, sender, staticcallTransaction));
        emitResult.Result.Should().Be(Result.Success);
        chain.LatestReceipts()[1].StatusCode.Should().Be(StatusCode.Success);
    }*/
}
