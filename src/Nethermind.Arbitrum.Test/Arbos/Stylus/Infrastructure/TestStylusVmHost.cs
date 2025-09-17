// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;

public class TestStylusVmHost(EvmState evmState, IWorldState worldState, IReleaseSpec spec) : IStylusVmHost
{
    public IWorldState WorldState { get; private set; } = worldState;
    public EvmState EvmState { get; private set; } = evmState;
    public IReleaseSpec Spec { get; private set; } = spec;

    public StylusEvmResult StylusCall(ExecutionType kind, Address to, ReadOnlyMemory<byte> input, ulong gasLeftReportedByRust, ulong gasRequestedByRust, in UInt256 value)
    {
        throw new NotImplementedException();
    }

    public StylusEvmResult StylusCreate(ReadOnlyMemory<byte> initCode, in UInt256 endowment, UInt256? salt, ulong gasLimit)
    {
        throw new NotImplementedException();
    }
}
