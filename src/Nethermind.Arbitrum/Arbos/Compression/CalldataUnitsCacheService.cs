// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core.Caching;

namespace Nethermind.Arbitrum.Arbos.Compression;

/// <summary>
/// Singleton service wrapping TransactionExtensions static cache for IClearableCache auto-discovery.
/// </summary>
public sealed class CalldataUnitsCacheService : IClearableCache
{
    public void ClearCache() => TransactionExtensions.ClearCache();
}
