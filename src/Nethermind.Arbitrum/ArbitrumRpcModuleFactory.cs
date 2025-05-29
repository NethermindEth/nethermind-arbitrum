// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Api;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Modules;
using Nethermind.Blockchain;
using Nethermind.Consensus.Producers;
using Nethermind.JsonRpc.Modules;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum;

public class ArbitrumRpcModuleFactory(
    INethermindApi api,
    IBlockTree blockTree,
    IManualBlockProductionTrigger trigger,
    ArbitrumRpcTxSource txSource,
    ChainSpec chainSpec,
    IArbitrumConfig arbitrumConfig,
    ILogger logger) : ModuleFactoryBase<IArbitrumRpcModule>
{
    public override IArbitrumRpcModule Create()
    {
        return new ArbitrumRpcModule(api, blockTree, trigger, txSource, chainSpec, arbitrumConfig, logger);
    }
}
