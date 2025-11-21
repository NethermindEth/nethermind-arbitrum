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

/// <summary>
/// These tests deploy a set of Stylus and Solidity contracts that call each other and verify
/// StylusPrecompileTest - Stylus contract that uses Arb precompiles to get network fee balance
/// SolidityCallStylus - Solidity contract that calls the StylusPrecompileTest
/// StylusCallAddress - Stylus general proxy smart contract
/// </summary>
public class StylusExecutionNestedTests
{
    private const string StylusPrecompileTestAddress = "0xe1080224b632a93951a7cfa33eeea9fd81558b5e";
    private const string StylusCallAddress = "0x1294b86822ff4976bfe136cb06cf43ec7fcf2574";
    private const string SolidityCallStylusAddress = "0x0bdad990640A488400565fe6fB1D879fFE12DA37";
    private static readonly string RecordingPath = "./Recordings/4__stylus_nested.jsonl";
    private static readonly UInt256 L1BaseFee = 13;

    private static readonly byte[] GetNetworkFeeBalanceCalldata = KeccakHash.ComputeHashBytes("getNetworkFeeBalance()"u8)[..4];

    private static readonly AbiSignature ExecuteCallSignature = new("executeCall", AbiType.Address, AbiType.DynamicBytes);
    private static readonly AbiSignature ExecuteGetNetworkFeeBalance = new("getNetworkFeeBalance");

    [TestCase(StylusCallAddress, SolidityCallStylusAddress, 28, 71511)]
    public async Task CallStylus_Solidity_Stylus_Precompile_CalculatesCorrectGasSpent(string callAddress, string counterAddress, byte contractBlock, long expectedGas)
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile(RecordingPath), contractBlock)
            .Build();

        Address sender = FullChainSimulationAccounts.Owner.Address;
        Address callContract = new(callAddress);
        Address counterContract = new(counterAddress);

        Transaction callTransaction;

        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head?.Header))
        {
            //Call stylus proxy -> solidity contract -> stylus contract -> precompile
            callTransaction = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(callContract)
                .WithData(AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, ExecuteCallSignature, counterContract, GetNetworkFeeBalanceCalldata))
                .WithMaxFeePerGas(10.GWei())
                .WithGasLimit(650007)
                .WithValue(0)
                .WithNonce(chain.WorldStateManager.GlobalWorldState.GetNonce(sender))
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
