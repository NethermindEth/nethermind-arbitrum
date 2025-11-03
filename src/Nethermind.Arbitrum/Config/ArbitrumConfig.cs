// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumConfig : IArbitrumConfig
{
    public bool SafeBlockWaitForValidator { get; set; } = false;
    public bool FinalizedBlockWaitForValidator { get; set; } = false;
    public int BlockProcessingTimeout { get; set; } = 1000;
    public string RebuildLocalWasm { get; set; } = "auto";
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
