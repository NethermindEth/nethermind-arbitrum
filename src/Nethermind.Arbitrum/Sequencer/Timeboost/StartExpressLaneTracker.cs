// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Api.Steps;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Init.Steps;

namespace Nethermind.Arbitrum.Sequencer.Timeboost;

[RunnerStepDependencies([typeof(ArbitrumInitializeWasmDb)], [typeof(StartBlockProcessor)])]
public sealed class StartExpressLaneTracker(IExpressLaneTracker tracker) : IStep
{
    public Task Execute(CancellationToken cancellationToken)
    {
        tracker.Start(cancellationToken);
        return Task.CompletedTask;
    }
}
