// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Arbitrum.Config;

public class VerifyBlockHashConfig : IVerifyBlockHashConfig
{
    public string? ArbNodeRpcUrl { get; set; }
    public bool Enabled { get; set; }
    public ulong VerifyEveryNBlocks { get; set; } = 10000;
}
