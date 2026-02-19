// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Core;

namespace Nethermind.Arbitrum.Evm;

/// <summary>
/// Singleton service wrapping L1BlockCache static cache for ICacheAware auto-discovery.
/// </summary>
public sealed class L1BlockHashCacheService : ICacheAware
{
    public void ClearCaches() => L1BlockCache.ClearStaticCache();
}
