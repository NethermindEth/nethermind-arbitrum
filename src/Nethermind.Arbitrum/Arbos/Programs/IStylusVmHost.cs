// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Arbos.Programs;

public interface IStylusVmHost
{
    ref readonly BlockExecutionContext BlockExecutionContext { get; }
    ref readonly TxExecutionContext TxExecutionContext { get; }
    public IWorldState WorldState { get; }
    public IWasmStore WasmStore { get; }
    public VmState<ArbitrumGas> VmState { get; }
    public IReleaseSpec Spec { get; }

    StylusEvmResult StylusCall(ExecutionType kind, Address to, ReadOnlyMemory<byte> input, ulong gasLeftReportedByRust, ulong gasRequestedByRust,
        in UInt256 value);

    StylusEvmResult StylusCreate(ReadOnlyMemory<byte> initCode, in UInt256 endowment, UInt256? salt, ulong gasLimit);
}
