// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumConfig : IArbitrumConfig
{
    public bool SafeBlockWaitForValidator { get; set; } = false;
    public bool FinalizedBlockWaitForValidator { get; set; } = false;
    public int BlockProcessingTimeout { get; set; } = 1000;
}

public static class ArbitrumConfigExtensions
{
    public static CancellationTokenSource BuildProcessingTimeoutTokenSource(this IArbitrumConfig config)
    {
        return Debugger.IsAttached ? new CancellationTokenSource() : new CancellationTokenSource(config.BlockProcessingTimeout);
    }
}
