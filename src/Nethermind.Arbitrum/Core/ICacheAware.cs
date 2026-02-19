// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

namespace Nethermind.Arbitrum.Core;

/// <summary>
/// Interface for services with caches that need clearing during test reinitialization.
/// All implementations are auto-discovered by DI and cleared via debug_reinitialize.
/// </summary>
public interface ICacheAware
{
    /// <summary>
    /// Clears all caches owned by this service.
    /// Called during debug_reinitialize for test isolation.
    /// </summary>
    void ClearCaches();
}
