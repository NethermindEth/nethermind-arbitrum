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

public class StylusExecutionGasCostTests
{
    private const string StylusCallAddress = "0x0bdad990640a488400565fe6fb1d879ffe12da37";
    private const string StylusCounterAddress = "0x9df23e34ac13a7145eba1164660e701839197b1b";

    private static readonly byte[] CounterGetCounterCalldata = KeccakHash.ComputeHashBytes("get()"u8)[..4];

    private static readonly AbiSignature ExecuteCallSignature = new("executeCall", AbiType.Address, AbiType.DynamicBytes);
    private static readonly AbiSignature ExecuteCounterSignature = new("get");
    private static readonly UInt256 L1BaseFee = 13;
    private static readonly string RecordingPath = "./Recordings/3__stylus_cost.jsonl";

    [TestCase(StylusCounterAddress, 24, 38944)]
    public async Task CallContract_CallCounterGet_CalculatesCorrectGasSpent(string counterAddress, byte contractBlock, long expectedGas)
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile(RecordingPath), contractBlock)
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;
        Address counterContract = new(counterAddress);

        Transaction callTransaction;

        using (chain.MainWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            callTransaction = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(counterContract)
                .WithData(AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, ExecuteCounterSignature))
                .WithMaxFeePerGas(10.GWei())
                .WithGasLimit(500000)
                .WithValue(0)
                .WithNonce(chain.MainWorldState.GetNonce(sender))
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;
        }

        ResultWrapper<MessageResult> callResult = await chain.Digest(new TestL2Transactions(L1BaseFee, sender, callTransaction));
        callResult.Result.Should().Be(Result.Success);
        TxReceipt txReceipt = chain.LatestReceipts()[1];
        txReceipt.StatusCode.Should().Be(StatusCode.Success);
        txReceipt.GasUsed.Should().Be(expectedGas);
    }

    [TestCase(StylusCallAddress, StylusCounterAddress, 24, 62512L)]
    public async Task CallContract_CallCounterGetViaProxy_CalculatesCorrectGasSpent(string callAddress, string counterAddress, byte contractBlock, long expectedGas)
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile(RecordingPath), contractBlock)
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;
        Address callContract = new(callAddress);
        Address counterContract = new(counterAddress);

        Transaction callTransaction;

        using (chain.MainWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            // CALL increment through the Call contract
            callTransaction = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(callContract)
                .WithData(AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, ExecuteCallSignature, counterContract, CounterGetCounterCalldata))
                .WithMaxFeePerGas(10.GWei())
                .WithGasLimit(65007)
                .WithValue(0)
                .WithNonce(chain.MainWorldState.GetNonce(sender))
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;
        }

        ResultWrapper<MessageResult> callResult = await chain.Digest(new TestL2Transactions(L1BaseFee, sender, callTransaction));
        callResult.Result.Should().Be(Result.Success);
        TxReceipt txReceipt = chain.LatestReceipts()[1];
        txReceipt.StatusCode.Should().Be(StatusCode.Success);
        txReceipt.GasUsed.Should().Be(expectedGas);
    }
}
