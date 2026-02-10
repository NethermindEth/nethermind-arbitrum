// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

namespace Nethermind.Arbitrum.Config;

public class VerifyBlockHashConfig : IVerifyBlockHashConfig
{
    public bool Enabled { get; set; }
    public ulong VerifyEveryNBlocks { get; set; } = 10000;
    public string? ArbNodeRpcUrl { get; set; }
}
