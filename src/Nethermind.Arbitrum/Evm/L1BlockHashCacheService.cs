// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core.Caching;

namespace Nethermind.Arbitrum.Evm;

/// <summary>
/// Singleton service wrapping L1BlockCache static cache for IClearableCache auto-discovery.
/// </summary>
//public sealed class L1BlockHashCacheService : IClearableCache
//{
//    public void ClearCache() => L1BlockCache.ClearStaticCache();
//}
