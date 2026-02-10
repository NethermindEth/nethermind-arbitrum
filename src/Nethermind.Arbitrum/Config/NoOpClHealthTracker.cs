// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Api;
using Nethermind.HealthChecks;

namespace Nethermind.Arbitrum.Config;

public class NoOpClHealthTracker : IClHealthTracker, IEngineRequestsTracker, IAsyncDisposable
{
    public bool CheckClAlive() => true;

    public void OnForkchoiceUpdatedCalled()
    {
    }

    public void OnNewPayloadCalled()
    {
    }

    public Task StartAsync() => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
