// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Config;

namespace Nethermind.Arbitrum.Config;

[ConfigCategory(HiddenFromDocs = true)]
public interface IArbitrumConfig : IConfig
{
    [ConfigItem(Description = "Whether safe blocks should wait for validator", DefaultValue = "false")]
    bool SafeBlockWaitForValidator { get; set; }

    [ConfigItem(Description = "Whether finalized blocks should wait for validator", DefaultValue = "false")]
    bool FinalizedBlockWaitForValidator { get; set; }

    [ConfigItem(Description = "Timeout in seconds for block processing operations", DefaultValue = "1000")]
    int BlockProcessingTimeout { get; set; }

    [ConfigItem(Description = "Rebuild local WASM store mode: 'false' to disable, 'force' to force rebuild, or other value to continue from last position", DefaultValue = "auto")]
    WasmRebuildMode RebuildLocalWasm { get; set; }

    [ConfigItem(DefaultValue = "1000", Description = "Allowed message lag in milliseconds while still considered in sync")]
    int MessageLagMs { get; set; }

    [ConfigItem(Description = "Experimental: Expose multi-dimensional gas in transaction receipts", DefaultValue = "false")]
    bool ExposeMultiGas { get; set; }

    [ConfigItem(Description = "Maximum number of state roots to keep pinned in the MemDb overlay simultaneously", DefaultValue = "1000")]
    int ValidatorMaxStateRootsInMem { get; set; }

    [ConfigItem(Description = "Maximum size in MB of the reconstructed state MemDb overlay before oldest roots are spilled to disk", DefaultValue = "1024")]
    int ValidatorReconstructedStateMemDBMaxSizeMb { get; set; }

    [ConfigItem(Description = "Whether validation is enabled", DefaultValue = "false")]
    bool ValidationEnabled { get; set; }

    [ConfigItem(Description = "Whether to enable sequencer mode", DefaultValue = "false")]
    bool SequencerEnabled { get; set; }

    [ConfigItem(Description = "Number of addresses to cache nonces for in sequencer mode", DefaultValue = "1024")]
    int SequencerNonceCacheSize { get; set; }

    [ConfigItem(Description = "Maximum number of transactions in a sequencer's queue", DefaultValue = "1024")]
    int SequencerMaxTxQueueSize { get; set; }

    [ConfigItem(Description = "Maximum transaction data size in bytes for sequencer mode", DefaultValue = "95000")]
    int SequencerMaxTxDataSize { get; set; }

    [ConfigItem(Description = "Maximum acceptable time difference between local time and L1 block timestamp in seconds", DefaultValue = "3600")]
    int SequencerMaxAcceptableTimestampDelta { get; set; }

    [ConfigItem(Description = "Maximum wait time in ms when there is nothing to sequence", DefaultValue = "250")]
    int SequencerMaxBlockSpeedMs { get; set; }

    [ConfigItem(Description = "Wait time in ms returned when sequencer is inactive (paused/forwarding)", DefaultValue = "50")]
    int SequencerInactiveWaitMs { get; set; }

    [ConfigItem(Description = "Whether eth_sendRawTransaction should block until the transaction is sequenced", DefaultValue = "false")]
    bool SequencerAwaitTxResult { get; set; }

    [ConfigItem(Description = "Maximum time in milliseconds a transaction can wait in the sequencer queue before timing out", DefaultValue = "12000")]
    int SequencerQueueTimeoutMs { get; set; }

    [ConfigItem(Description = "Comma-separated list of addresses allowed to submit transactions. Empty means all senders are allowed.", DefaultValue = "")]
    string SequencerSenderWhitelist { get; set; }

    [ConfigItem(Description = "Whether to enable Timeboost express lane auction priority", DefaultValue = "false")]
    bool TimeboostEnabled { get; set; }

    [ConfigItem(Description = "Express lane advantage over regular transactions in milliseconds", DefaultValue = "200")]
    int TimeboostExpressLaneAdvantageMs { get; set; }

    [ConfigItem(Description = "Number of blocks after which a timeboosted transaction expires from the queue", DefaultValue = "5")]
    ulong TimeboostQueueTimeoutInBlocks { get; set; }

    [ConfigItem(Description = "Address of the ExpressLaneAuction contract", DefaultValue = "")]
    string TimeboostAuctionContractAddress { get; set; }

    [ConfigItem(Description = "Auction contract winning controller poll interval in milliseconds", DefaultValue = "1000")]
    int TimeboostAuctionContractPollIntervalMs { get; set; }

    [ConfigItem(Description = "Address of the Timeboost autonomous auctioneer", DefaultValue = "")]
    string TimeboostAuctioneerAddress { get; set; }

    [ConfigItem(Description = "Grace period in milliseconds before the next round during which next-round express lane submissions are accepted", DefaultValue = "2000")]
    int TimeboostEarlySubmissionGraceMs { get; set; }

    [ConfigItem(Description = "Duration of a Timeboost express lane auction round in seconds", DefaultValue = "60")]
    int TimeboostRoundDurationSeconds { get; set; }

    [ConfigItem(Description = "Duration of the auction closing window at the end of each round in seconds", DefaultValue = "15")]
    int TimeboostAuctionClosingWindowSeconds { get; set; }
}
