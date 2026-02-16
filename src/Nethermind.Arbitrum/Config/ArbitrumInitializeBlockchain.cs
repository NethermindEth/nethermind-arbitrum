// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Consensus.Producers;
using Nethermind.Init.Steps;
using Nethermind.TxPool;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumInitializeBlockchain(ArbitrumNethermindApi api, IChainHeadInfoProvider chainHeadInfoProvider)
    : InitializeBlockchain(api, chainHeadInfoProvider)
{
    protected override IBlockProductionPolicy CreateBlockProductionPolicy() => AlwaysStartBlockProductionPolicy.Instance;
}
