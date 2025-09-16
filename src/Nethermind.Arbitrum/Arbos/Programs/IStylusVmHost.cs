// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Arbos.Programs;

public interface IStylusVmHost
{
    public IWorldState WorldState { get; }
    public EvmState EvmState { get; }
    public IReleaseSpec Spec { get; }

    StylusEvmResult StylusCall(ExecutionType kind, Address to, ReadOnlySpan<byte> input, ulong gasLeftReportedByRust, ulong gasRequestedByRust,
        in UInt256 value);

    StylusEvmResult StylusCreate(ReadOnlySpan<byte> initCode, in UInt256 endowment, UInt256? salt, ulong gasLimit);
}
