// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

namespace Nethermind.Arbitrum.Data;

public class SetConsensusSyncDataParams
{
    public bool Synced { get; set; }
    public ulong MaxMessageCount { get; set; }
    public Dictionary<string, object>? SyncProgressMap { get; set; }
    public DateTime UpdatedAt { get; set; }
}
