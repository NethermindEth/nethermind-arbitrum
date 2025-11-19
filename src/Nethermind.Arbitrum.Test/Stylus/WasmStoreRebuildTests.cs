// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Int256;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Test.Stylus;

[TestFixture]
public class WasmStoreRebuildTests
{
    private const string StylusCounterAddress = "0x0bdad990640a488400565fe6fb1d879ffe12da37";
    private static readonly string RecordingPath = "./Recordings/2__stylus.jsonl";
    private static readonly UInt256 L1BaseFee = 13;
    private static readonly byte[] CounterIncrementCalldata = KeccakHash.ComputeHashBytes("inc()"u8)[..4];

    [Test]
    public void RebuildWasmStore_FromBeginning_CompletesSuccessfully()
    {
        ArbitrumRpcTestBlockchain chain = CreateChainWithRecording();

        chain.WasmDB.SetRebuildingPosition(Keccak.Zero);

        Action rebuild = () => chain.RebuildWasmStore();

        rebuild.Should().NotThrow();
        chain.WasmDB.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);
    }

    [Test]
    public async Task RebuildWasmStore_AfterRebuild_AllowsStylusContractExecution()
    {
        ArbitrumRpcTestBlockchain chain = CreateChainWithRecording();

        chain.WasmDB.SetRebuildingPosition(Keccak.Zero);
        chain.RebuildWasmStore();

        Address sender = FullChainSimulationAccounts.Owner.Address;
        Address contract = new(StylusCounterAddress);

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

        ResultWrapper<MessageResult> result = await chain.Digest(new TestL2Transactions(L1BaseFee, sender, incTransaction));

        result.Result.Should().Be(Result.Success);
        chain.LatestReceipts()[1].StatusCode.Should().Be(StatusCode.Success);
    }

    [Test]
    public void RebuildWasmStore_WhenAlreadyCompleted_RemainsCompleted()
    {
        ArbitrumRpcTestBlockchain chain = CreateChainWithRecording();

        chain.WasmDB.SetRebuildingPosition(WasmStoreSchema.RebuildingDone);

        chain.RebuildWasmStore();

        chain.WasmDB.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);
    }

    [Test]
    public void RebuildWasmStore_WhenCancelled_SavesProgress()
    {
        ArbitrumRpcTestBlockchain chain = CreateChainWithRecording();
        using CancellationTokenSource cts = new();

        chain.WasmDB.SetRebuildingPosition(Keccak.Zero);
        cts.Cancel();

        Action rebuild = () => chain.RebuildWasmStore(cancellationToken: cts.Token);

        rebuild.Should().Throw<OperationCanceledException>();
        chain.WasmDB.GetRebuildingPosition().Should().NotBe(WasmStoreSchema.RebuildingDone);
    }

    [Test]
    public void RebuildWasmStore_FromMidpoint_CompletesSuccessfully()
    {
        ArbitrumRpcTestBlockchain chain = CreateChainWithRecording();

        Hash256 midPoint = new("0x8000000000000000000000000000000000000000000000000000000000000000");
        chain.WasmDB.SetRebuildingPosition(midPoint);

        Action rebuild = () => chain.RebuildWasmStore();

        rebuild.Should().NotThrow();
        chain.WasmDB.GetRebuildingPosition().Should().Be(WasmStoreSchema.RebuildingDone);
    }

    private static ArbitrumRpcTestBlockchain CreateChainWithRecording()
    {
        return new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile(RecordingPath), 22)
            .Build();
    }
}
