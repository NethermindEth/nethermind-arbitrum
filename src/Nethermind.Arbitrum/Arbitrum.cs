// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Api;
using Nethermind.Api.Extensions;
using Nethermind.Arbitrum.Modules;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Modules;

namespace Nethermind.Arbitrum
{
    public class Arbitrum(IArbitrumConfig arbitrumConfig) : INethermindPlugin
    {
        private INethermindApi _api = null!;

        private IJsonRpcConfig _jsonRpcConfig = null!;

        public string Name => "Arbitrum";
        public string Description => "Nethermind Arbitrum client";
        public string Author => "Nethermind";
        public bool Enabled => arbitrumConfig.Enabled;

        public Task InitRpcModules()
        {
            ArgumentNullException.ThrowIfNull(_api.RpcModuleProvider);

            ModuleFactoryBase<IArbitrumRpcModule> arbitrumRpcModule = new ArbitrumRpcModuleFactory(arbitrumConfig);

            _api.RpcModuleProvider.RegisterBounded(arbitrumRpcModule, 1, _jsonRpcConfig.Timeout);

            return Task.CompletedTask;
        }

        public Task Init(INethermindApi api)
        {
            _api = api;
            _jsonRpcConfig = api.Config<IJsonRpcConfig>();
            _jsonRpcConfig.EnabledModules = _jsonRpcConfig.EnabledModules.Append(ModuleType.Arbitrum).ToArray();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
