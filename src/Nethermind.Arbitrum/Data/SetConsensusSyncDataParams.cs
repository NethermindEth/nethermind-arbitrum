// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Arbitrum.Data;

public class SetConsensusSyncDataParams
{
    public ulong MaxMessageCount { get; set; }
    public bool Synced { get; set; }
    public Dictionary<string, object>? SyncProgressMap { get; set; }
    public DateTime UpdatedAt { get; set; }
}
