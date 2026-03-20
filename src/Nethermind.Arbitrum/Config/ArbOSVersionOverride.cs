// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

namespace Nethermind.Arbitrum.Config;

/// <summary>
/// Service for overriding the ArbOS version during test reinitialization.
/// Set by debug_reinitialize, read (non-destructively) during genesis initialization.
/// </summary>
public interface IArbOSVersionOverride
{
    /// <summary>
    /// Sets the ArbOS version override for the next genesis initialization.
    /// </summary>
    void SetOverride(ulong version);

    /// <summary>
    /// Returns the current override value, or null if no override is set.
    /// </summary>
    ulong? GetOverride();
}

public class ArbOSVersionOverride : IArbOSVersionOverride
{
    private ulong? _overrideVersion;

    public void SetOverride(ulong version) => _overrideVersion = version;
    public ulong? GetOverride() => _overrideVersion;
}
