// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Evm;

namespace Nethermind.Arbitrum.Evm;

/// <summary>
/// Arbitrum-specific gas constants for MultiGas breakdown.
/// Derived from GasCostOf, matching Nitro's protocol_params.go.
/// </summary>
public static class ArbitrumGasCostOf
{
    // Log topic breakdown (matching Nitro protocol_params.go:88-91)
    public const long LogTopicBytes = 32;
    public const long LogTopicComputationGas = GasCostOf.LogTopic - LogTopicHistoryGas; // 375 - 256 = 119
    public const long LogTopicHistoryGas = GasCostOf.LogData * LogTopicBytes; // 375 - 256 = 119
}
