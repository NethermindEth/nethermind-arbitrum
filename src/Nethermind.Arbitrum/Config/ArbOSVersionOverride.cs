// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

namespace Nethermind.Arbitrum.Config;

/// <summary>
/// Service for overriding the ArbOS version during test reinitialization.
/// Used by debug_reinitialize to specify the ArbOS version for genesis initialization.
/// </summary>
public interface IArbOSVersionOverride
{
    /// <summary>
    /// Gets or sets the ArbOS version override. Null means use chainspec default.
    /// </summary>
    ulong? OverrideVersion { get; set; }
}

/// <summary>
/// Default implementation of <see cref="IArbOSVersionOverride"/>.
/// </summary>
public class ArbOSVersionOverride : IArbOSVersionOverride
{
    public ulong? OverrideVersion { get; set; }
}
