// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.Gas;
using Nethermind.Evm.State;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;

public class TestStylusVmHost(VmState<EthereumGasPolicy> vmState, IWorldState worldState, IReleaseSpec spec) : IStylusVmHost
{
    public IWorldState WorldState { get; private set; } = worldState;
    public VmState<EthereumGasPolicy> VmState { get; private set; } = vmState;
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
