// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Arbos;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public sealed class TestArbosVersionProvider(ulong version) : IArbosVersionProvider
{
    public ulong Get() => version;
}
