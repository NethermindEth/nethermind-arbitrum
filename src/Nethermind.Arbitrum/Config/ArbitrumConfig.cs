// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Diagnostics;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumConfig : IArbitrumConfig
{
    public bool SafeBlockWaitForValidator { get; set; } = false;
    public bool FinalizedBlockWaitForValidator { get; set; } = false;
    public int BlockProcessingTimeout { get; set; } = 1000;
    public WasmRebuildMode RebuildLocalWasm { get; set; } = WasmRebuildMode.Auto;
    public int MessageLagMs { get; set; } = 1000;
    public bool ExposeMultiGas { get; set; } = false;
    public int ValidatorMaxStateRootsInMem { get; set; } = 1000;
    public bool SequencerEnabled { get; set; } = false;
    public int SequencerNonceCacheSize { get; set; } = 1024;
    public int SequencerMaxTxQueueSize { get; set; } = 1024;
    public int SequencerMaxTxDataSize { get; set; } = 95000;
    public int SequencerMaxAcceptableTimestampDelta { get; set; } = 3600;
    public int SequencerMaxBlockSpeedMs { get; set; } = 250;
    public int SequencerInactiveWaitMs { get; set; } = 50;
    public bool SequencerAwaitTxResult { get; set; } = false;
    public int SequencerQueueTimeoutMs { get; set; } = 12000;
    public string SequencerSenderWhitelist { get; set; } = "";
    public bool TimeboostEnabled { get; set; } = false;
    public int TimeboostExpressLaneAdvantageMs { get; set; } = 200;
    public ulong TimeboostQueueTimeoutInBlocks { get; set; } = 5;
    public string TimeboostAuctionContractAddress { get; set; } = "";
    public int TimeboostAuctionContractPollIntervalMs { get; set; } = 1000;
    public string TimeboostAuctioneerAddress { get; set; } = "";
    public int TimeboostEarlySubmissionGraceMs { get; set; } = 2000;
    public int TimeboostRoundDurationSeconds { get; set; } = 60;
    public int TimeboostAuctionClosingWindowSeconds { get; set; } = 15;
}

public static class ArbitrumConfigExtensions
{
    private const int FallbackTimeoutMs = 5 * 60 * 1000; // 5 minutes

    public static CancellationTokenSource BuildProcessingTimeoutTokenSource(this IArbitrumConfig config)
    {
        return Debugger.IsAttached
            ? new CancellationTokenSource(FallbackTimeoutMs)
            : new CancellationTokenSource(config.BlockProcessingTimeout);
    }
}
