// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Evm;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Logging;
using Nethermind.State;

namespace Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;

public class TestStylusVirtualMachine(IBlockhashProvider? blockHashProvider, ISpecProvider? specProvider, ILogManager? logManager) : ArbitrumVirtualMachine(blockHashProvider, specProvider, logManager)
{
    public void InitVm(EvmState state, IWorldState worldState)
    {
        _worldState = worldState;
        EvmState = state;
    }
}
