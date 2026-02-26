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

    [ConfigItem(Description = "Whether to enable sequencer mode", DefaultValue = "false")]
    bool SequencerEnabled { get; set; }

    [ConfigItem(Description = "Number of addresses to cache nonces for in sequencer mode", DefaultValue = "1024")]
    int SequencerNonceCacheSize { get; set; }

    [ConfigItem(Description = "Maximum transaction data size in bytes for sequencer mode", DefaultValue = "95000")]
    int SequencerMaxTxDataSize { get; set; }

    [ConfigItem(Description = "Maximum acceptable time difference between local time and L1 block timestamp in seconds", DefaultValue = "3600")]
    int SequencerMaxAcceptableTimestampDelta { get; set; }

    [ConfigItem(Description = "Maximum wait time in ms when there is nothing to sequence", DefaultValue = "5000")]
    int SequencerMaxBlockSpeedMs { get; set; }

    [ConfigItem(Description = "Wait time in ms returned when sequencer is inactive (paused/forwarding)", DefaultValue = "50")]
    int SequencerInactiveWaitMs { get; set; }

    [ConfigItem(Description = "Whether eth_sendRawTransaction should block until the transaction is sequenced", DefaultValue = "false")]
    bool SequencerAwaitTxResult { get; set; }
}
