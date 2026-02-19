// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Core;

namespace Nethermind.Arbitrum.Arbos.Compression;

/// <summary>
/// Singleton service wrapping TransactionExtensions static cache for ICacheAware auto-discovery.
/// </summary>
public sealed class CalldataUnitsCacheService : ICacheAware
{
    public void ClearCaches() => TransactionExtensions.ClearCache();
}
