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
    public string? GenesisConfig { get; set; } = null;
    public ulong InitialL1BaseFee { get; set; } = 0;
    public int MessageLagMs { get; set; } = 1000;
    public bool ExposeMultiGas { get; set; } = false;
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
