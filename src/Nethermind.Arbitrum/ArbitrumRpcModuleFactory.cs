// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Modules;
using Nethermind.JsonRpc.Modules;

namespace Nethermind.Arbitrum
{
    public class ArbitrumRpcModuleFactory : ModuleFactoryBase<IArbitrumRpcModule>
    {
        private readonly IArbitrumConfig _arbitrumConfig;

        public ArbitrumRpcModuleFactory(IArbitrumConfig arbitrumConfig)
        {
            _arbitrumConfig = arbitrumConfig;
        }

        public override IArbitrumRpcModule Create()
        {
            return new ArbitrumRpcModule(_arbitrumConfig);
        }
    }
}
