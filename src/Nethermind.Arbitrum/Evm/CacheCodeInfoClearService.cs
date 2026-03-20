// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core.Caching;
using Nethermind.Evm;

namespace Nethermind.Arbitrum.Evm;

/// <summary>
/// Singleton service wrapping CacheCodeInfoRepository static cache for IClearableCache auto-discovery.
/// </summary>
public sealed class CacheCodeInfoClearService : IClearableCache
{
    public void ClearCache() => CacheCodeInfoRepository.Clear();
}
