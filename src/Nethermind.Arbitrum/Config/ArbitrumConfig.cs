// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

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
    public bool SequencerEnabled { get; set; } = false;
    public int SequencerNonceCacheSize { get; set; } = 1024;
    public int SequencerMaxTxDataSize { get; set; } = 95000;
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
