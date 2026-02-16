// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

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
