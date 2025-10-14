// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Arbitrum.Sequencer;

public class SequencerConfig : ISequencerConfig
{
    public int MaxQueueSize { get; set; } = 1024;
    public int QueueTimeoutSeconds { get; set; } = 12;
    public int MaxTxDataSize { get; set; } = 95000;
}
