// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics;
using Nethermind.Arbitrum.Stylus;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumConfig : IArbitrumConfig
{
    public bool SafeBlockWaitForValidator { get; set; } = false;
    public bool FinalizedBlockWaitForValidator { get; set; } = false;
    public int BlockProcessingTimeout { get; set; } = 1000;
    public WasmRebuildMode RebuildLocalWasm { get; set; } = WasmRebuildMode.Auto;
    public int MessageLagMs { get; set; } = 1000;
    public bool ExposeMultiGas { get; set; } = false;
    public long TrieTimeLimitBeforeFlushMaintenanceMs { get; set; } = 0;
    public long TrieTimeLimitRandomOffsetMs { get; set; } = 0;
    public long TrieTimeLimitMs { get; set; } = 3600000; // 1 hour
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
