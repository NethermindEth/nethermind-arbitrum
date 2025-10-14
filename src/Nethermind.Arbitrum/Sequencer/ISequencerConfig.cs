// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Config;

namespace Nethermind.Arbitrum.Sequencer;

[ConfigCategory(HiddenFromDocs = true)]
public interface ISequencerConfig : IConfig
{
    [ConfigItem(Description = "Maximum number of transactions in the sequencer queue", DefaultValue = "1024")]
    int MaxQueueSize { get; set; }

    [ConfigItem(Description = "Timeout in seconds for transactions in the queue", DefaultValue = "12")]
    int QueueTimeoutSeconds { get; set; }

    [ConfigItem(Description = "Maximum size in bytes for a single transaction (0 = no limit)", DefaultValue = "95000")]
    int MaxTxDataSize { get; set; }

    // TODO: string ExpectedSurplusHardThreshold - L1 surplus validation threshold
    // TODO: Dictionary<Address, bool> SenderWhitelist - Authorized transaction senders
    // TODO: bool TimeBoostEnable - Enable TimeBoost express lane functionality
    // TODO: TimeSpan ExpressLaneAdvantage - Delay for non-express lane transactions
    // TODO: ulong QueueTimeoutInBlocks - Block-based timeout for time-boosted transactions
}
