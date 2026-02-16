// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

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
using Nethermind.State;

namespace Nethermind.Arbitrum.Test.Stylus;

/// <summary>
/// These tests deploy 2 Stylus contracts with additional payable version of methods to transfer value when called
/// StylusCallAddress - Stylus general proxy smart contract
/// StylusCounterAddress - Stylus simple counter implementation
/// </summary>
public class StylusExecutionPayable
{
    private const string StylusCallAddress = "0x9df23e34ac13a7145eba1164660e701839197b1b";
    private const string StylusCounterAddress = "0x0bdad990640a488400565fe6fb1d879ffe12da37";
    private static readonly string RecordingPath = "./Recordings/8__stylus_payable.jsonl";
    private static readonly UInt256 L1BaseFee = 13;

    private static readonly byte[] GetPayableCalldata = KeccakHash.ComputeHashBytes("getPayable()"u8)[..4];

    private static readonly AbiSignature ExecuteCallPayableSignature = new("executeCallPayable", AbiType.Address, AbiType.DynamicBytes);
    //private static readonly AbiSignature CounterGetPayableSignature = new("getPayable");

    [Test]
    public async Task CallStylus_PayableMethod_CorrectlyTransfersValue()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile(RecordingPath), 23)
            .Build();

        Address sender = FullChainSimulationAccounts.Dev.Address;
        Address counterContract = new(StylusCounterAddress);
        Address callContract = new(StylusCallAddress);

        Transaction callTransaction;

        using (chain.MainWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            //create a call to Stylus proxy contract to call Stylus counter contract payable method
            //this ensures that the value is correctly parsed (big endian vs little endian) and transferred through the call chain
            callTransaction = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(callContract)
                .WithData(AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, ExecuteCallPayableSignature, counterContract, GetPayableCalldata))
                .WithMaxFeePerGas(10.GWei())
                .WithGasLimit(650007)
                .WithValue(66)
                .WithNonce(chain.MainWorldState.GetNonce(sender))
                .SignedAndResolved(FullChainSimulationAccounts.Dev)
                .TestObject;
        }

        ResultWrapper<MessageResult> callResult = await chain.Digest(new TestL2Transactions(L1BaseFee, sender, callTransaction));
        callResult.Result.Should().Be(Result.Success);
        TxReceipt txReceipt = chain.LatestReceipts()[1];
        txReceipt.StatusCode.Should().Be(StatusCode.Success);

        UInt256 counterBalance = chain.WorldStateManager.GlobalStateReader.GetBalance(chain.BlockTree.Head?.Header, counterContract);
        UInt256 callBalance = chain.WorldStateManager.GlobalStateReader.GetBalance(chain.BlockTree.Head?.Header, callContract);

        counterBalance.Should().Be(66);
        callBalance.Should().Be(0);
    }
}
