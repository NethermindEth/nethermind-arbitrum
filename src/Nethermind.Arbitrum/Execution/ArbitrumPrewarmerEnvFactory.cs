// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Autofac;
using Nethermind.Blockchain;
using Nethermind.Consensus.Processing;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Core;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Execution;

public class ArbitrumPrewarmerEnvFactory(IWorldStateManager worldStateManager, ILifetimeScope parentLifetime) : IPrewarmerEnvFactory
{
    public IReadOnlyTxProcessorSource Create(IPreBlockCachesInner preBlockCaches)
    {
        var worldState = new ArbitrumPrewarmerScopeProvider(
            worldStateManager.CreateResettableWorldState(),
            preBlockCaches,
            populatePreBlockCache: true,
            parentLifetime.Resolve<ILogManager>()
        );

        ILifetimeScope childScope = parentLifetime.BeginLifetimeScope((builder) =>
        {
            builder
                .AddSingleton<IWorldStateScopeProvider>(worldState)
                .AddSingleton<AutoReadOnlyTxProcessingEnvFactory.AutoReadOnlyTxProcessingEnv>();
        });

        return childScope.Resolve<AutoReadOnlyTxProcessingEnvFactory.AutoReadOnlyTxProcessingEnv>();
    }
}
