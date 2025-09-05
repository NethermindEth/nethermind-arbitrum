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
using Nethermind.Evm;
using Nethermind.Int256;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Test.Stylus;

public class StylusExecutionTests
{
    private static readonly string RecordingPath = "./Recordings/2__stylus.jsonl";
    private static readonly UInt256 L1BaseFee = 13;

    private const string SolidityCounterAddress = "0x9df23e34ac13a7145eba1164660e701839197b1b";
    private const string SolidityCallAddress = "0x9f1ece352ce8d540738ccb38aa3fa3d44d00a259";
    private const string StylusCounterAddress = "0x0bdad990640a488400565fe6fb1d879ffe12da37";
    private const string StylusCallAddress = "0xa75fbfe03ac01540e1e0b6c1a48a45f10c74daa7";

    private static readonly byte[] CounterIncrementCalldata = KeccakHash.ComputeHashBytes("inc()"u8)[..4];
    private static readonly byte[] CounterGetCounterCalldata = KeccakHash.ComputeHashBytes("get()"u8)[..4];
    private static readonly byte[] CounterEmitCountCalldata = KeccakHash.ComputeHashBytes("emitCount()"u8)[..4];
    private static readonly byte[] CounterLogCountEventData = KeccakHash.ComputeHashBytes("LogCount(uint256)"u8);

    private static readonly AbiSignature ExecuteCallSignature = new("executeCall", AbiType.Address, AbiType.DynamicBytes);
    private static readonly AbiSignature ExecuteStaticCallSignature = new("executeStaticCall", AbiType.Address, AbiType.DynamicBytes);
    private static readonly AbiSignature ExecuteDelegateCallSignature = new("executeDelegateCall", AbiType.Address, AbiType.DynamicBytes);

    [TestCase(SolidityCounterAddress, 1, 19)]
    [TestCase(SolidityCounterAddress, 3, 19)]
    [TestCase(StylusCounterAddress, 1, 22)]
    [TestCase(StylusCounterAddress, 3, 22)]
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
            Transaction incTransaction = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(contract)
                .WithData(CounterIncrementCalldata)
                .WithMaxFeePerGas(10.GWei())
                .WithGasLimit(100000)
                .WithValue(0)
                .WithNonce(chain.WorldStateManager.GlobalWorldState.GetNonce(sender))
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;

            ResultWrapper<MessageResult> incResult = await chain.Digest(new TestL2Transactions(L1BaseFee, sender, incTransaction));
            incResult.Result.Should().Be(Result.Success);
            chain.LatestReceipts()[1].StatusCode.Should().Be(StatusCode.Success);
        }

        // Emit counter's value
        Transaction emitTransaction = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(contract)
            .WithData(CounterEmitCountCalldata)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(50000)
            .WithValue(0)
            .WithNonce(chain.WorldStateManager.GlobalWorldState.GetNonce(sender))
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

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

    [TestCase(SolidityCallAddress, SolidityCounterAddress, 20)]
    [TestCase(SolidityCallAddress, StylusCounterAddress, 24)]
    [TestCase(StylusCallAddress, SolidityCounterAddress, 24)]
    [TestCase(StylusCallAddress, StylusCounterAddress, 24)]
    public async Task CallContract_CallCounterIncrement_ProxiesCallToCounterContract(string callAddress, string counterAddress, byte contractBlock)
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile(RecordingPath), contractBlock)
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;
        Address callContract = new(callAddress);
        Address counterContract = new(counterAddress);

        // CALL increment through the Call contract
        Transaction callTransaction = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(callContract)
            .WithData(AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, ExecuteCallSignature, counterContract, CounterIncrementCalldata))
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(700000)
            .WithValue(0)
            .WithNonce(chain.WorldStateManager.GlobalWorldState.GetNonce(sender))
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

        ResultWrapper<MessageResult> callResult = await chain.Digest(new TestL2Transactions(L1BaseFee, sender, callTransaction));
        callResult.Result.Should().Be(Result.Success);
        chain.LatestReceipts()[1].StatusCode.Should().Be(StatusCode.Success);

        // Emit counter's value
        Transaction emitTransaction = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(counterContract)
            .WithData(CounterEmitCountCalldata)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(50000)
            .WithValue(0)
            .WithNonce(chain.WorldStateManager.GlobalWorldState.GetNonce(sender))
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

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
