// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

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
