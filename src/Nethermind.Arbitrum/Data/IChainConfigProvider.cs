// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Evm.State;
using Nethermind.Logging;
using System.Text.Json;

namespace Nethermind.Arbitrum.Data
{
    /// <summary>
    /// Abstraction for accessing chain configuration.
    /// </summary>
    public interface IChainConfigProvider
    {
        /// <summary>
        /// Retrieves the chain configuration.
        /// </summary>
        /// <param name="stateProvider">World state to read configuration from</param>
        /// <returns>The chain configuration</returns>
        ChainConfig GetChainConfig(IWorldState stateProvider);

        /// <summary>
        /// Invalidates the cache, forcing reload on next access.
        /// </summary>
        void InvalidateCache();
    }

    /// <summary>
    /// Cached implementation of chain config provider.
    /// </summary>
    public sealed class CachedChainConfigProvider : IChainConfigProvider
    {
        private readonly ILogManager _logManager;
        private readonly object _cacheLock = new();
        private volatile ChainConfig? _cachedConfig;

        public CachedChainConfigProvider(ILogManager logManager)
        {
            _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
        }

        public ChainConfig GetChainConfig(IWorldState stateProvider)
        {
            if (stateProvider == null)
                throw new ArgumentNullException(nameof(stateProvider));

            if (_cachedConfig is not null)
                return _cachedConfig;

            lock (_cacheLock)
            {
                if (_cachedConfig is not null)
                    return _cachedConfig;

                _cachedConfig = LoadFromStorage(stateProvider);

                if (_logManager.GetClassLogger().IsDebug)
                    _logManager.GetClassLogger().Debug($"ChainConfig cached: GenesisBlock={_cachedConfig.ArbitrumChainParams.GenesisBlockNum}");

                return _cachedConfig;
            }
        }

        /// <summary>
        /// Invalidates the cache, forcing reload on next access.
        /// </summary>
        public void InvalidateCache()
        {
            lock (_cacheLock)
            {
                _cachedConfig = null;
                if (_logManager.GetClassLogger().IsDebug)
                    _logManager.GetClassLogger().Debug("ChainConfig cache invalidated");
            }
        }

        private ChainConfig LoadFromStorage(IWorldState stateProvider)
        {
            ArbosState arbosState = ArbosState.OpenArbosState(
                stateProvider,
                new SystemBurner(),
                _logManager.GetClassLogger<ArbosState>()
            );

            byte[] serializedConfig = arbosState.ChainConfigStorage.Get();

            if (serializedConfig == null || serializedConfig.Length == 0)
                throw new InvalidOperationException("ChainConfig storage is empty");

            ChainConfig? chainConfig = JsonSerializer.Deserialize<ChainConfig>(serializedConfig);

            if (chainConfig?.ArbitrumChainParams == null)
                throw new InvalidOperationException("Invalid ChainConfig: missing ArbitrumChainParams");

            return chainConfig;
        }
    }
}
