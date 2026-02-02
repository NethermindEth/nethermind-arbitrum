// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Stylus;
using Nethermind.Db;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public static class TestWasmStore
{
    public static IWasmStore Create()
    {
        return new WasmStore(new WasmDb(new MemDb()), new StylusTargetConfig(), cacheTag: 1);
    }
}
