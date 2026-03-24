// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Buffers.Binary;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Sequencer.Queues;
using Nethermind.Arbitrum.Sequencer.Timeboost;
using Nethermind.Core;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;
using Nethermind.Facade;
using Nethermind.Logging;
using NSubstitute;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public class TestExpressLane
{
    public static readonly Address TestAuctionContract = TestItem.AddressF;

    public static ExpressLaneTracker CreateTracker(
        out TestExpressLaneTrackerContext context,
        Action<ArbitrumConfig>? setup = null,
        ulong currentRound = 1,
        int intoRoundSeconds = 30)
    {
        ArbitrumConfig config = TestSequencer.DefaultConfig(setup);
        setup?.Invoke(config);

        ManualRoundTimingInfo timing = new(config, DateTimeOffset.UtcNow, currentRound, TimeSpan.FromSeconds(intoRoundSeconds));
        FakeAuctionContract auctionContract = new() { Address = TestAuctionContract };

        context = new(timing, auctionContract, config, currentRound, intoRoundSeconds);

        ExpressLaneTracker tracker = new(timing, auctionContract, config, LimboLogs.Instance);
        tracker.Start(CancellationToken.None).GetAwaiter().GetResult();

        return tracker;
    }

    public static ExpressLaneService CreateService(ExpressLaneTracker tracker, TestExpressLaneTrackerContext trackerContext, out TestExpressLaneServiceContext context)
    {
        TransactionQueue txQueue = new(trackerContext.Config, tracker, trackerContext.Timing.TimeProvider);

        context = new(txQueue);

        return new ExpressLaneService(
            trackerContext.Timing,
            tracker,
            trackerContext.Config,
            txQueue,
            new EthereumEcdsa(FullChainSimulationChainSpecProvider.ChainId),
            FullChainSimulationChainSpecProvider.Create(),
            LimboLogs.Instance);
    }
}

public record TestExpressLaneTrackerContext(
    ManualRoundTimingInfo Timing,
    FakeAuctionContract AuctionContract,
    ArbitrumConfig Config,
    ulong CurrentRound,
    int IntoRoundSeconds)
{
    public void AdvanceTime(TimeSpan delta) => Timing.Advance(delta);

    public void AdvanceToNextRound()
    {
        Timing.Advance(TimeSpan.FromSeconds(Config.TimeboostRoundDurationSeconds));
    }

    public async Task AdvanceLoop(ResolvedRound resolvedRound)
    {
        AuctionContract.Result = resolvedRound;
        await Task.Delay(5); // Let ExpressLaneTracker loop to advance
        Timing.Advance(TimeSpan.FromMilliseconds(Config.TimeboostAuctionContractPollIntervalMs)); // Advance time to trigger next poll
        await Task.Delay(5); // Let ExpressLaneTracker loop to process poll result
    }
}

public record TestExpressLaneServiceContext(TransactionQueue TxQueue);

public sealed class FakeAuctionContract : IAuctionContract
{
    public Address Address { get; set; } = Address.Zero;

    public ResolvedRound Result { get; set; } = new(Address.Zero, 0);

    public ResolvedRound ResolveRounds() => Result;

    public static byte[] AbiEncode(ResolvedRound resolvedRound)
    {
        byte[] output = new byte[128];
        resolvedRound.Controller.Bytes.CopyTo(output.AsSpan(12, 20));
        BinaryPrimitives.WriteUInt64BigEndian(output.AsSpan(56, 8), resolvedRound.Round);
        return output;
    }
}

public class FakeAuctionContractBlockchainBridgeFactory(byte[] callOutputData) : IBlockchainBridgeFactory
{
    public IBlockchainBridge CreateBlockchainBridge()
    {
        IBlockchainBridge bridge = Substitute.For<IBlockchainBridge>();
        bridge.Call(Arg.Any<BlockHeader>(), Arg.Any<Transaction>()).Returns(new CallOutput { OutputData = callOutputData });
        return bridge;
    }
}
