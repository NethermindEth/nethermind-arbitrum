// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;

public class TestStylusVmHost(
    BlockExecutionContext blockExecutionContext,
    TxExecutionContext txExecutionContext,
    VmState<ArbitrumGasPolicy> vmState,
    IWorldState worldState,
    IWasmStore wasmStore,
    IReleaseSpec spec,
    ulong currentArbosVersion = ArbosVersion.Forty) : IStylusVmHost
{
    private readonly BlockExecutionContext _blockExecutionContext = blockExecutionContext;
    private readonly TxExecutionContext _txExecutionContext = txExecutionContext;

    public ref readonly BlockExecutionContext BlockExecutionContext => ref _blockExecutionContext;
    public ref readonly TxExecutionContext TxExecutionContext => ref _txExecutionContext;
    public IWorldState WorldState { get; } = worldState;
    public IWasmStore WasmStore { get; } = wasmStore;
    public VmState<ArbitrumGasPolicy> VmState { get; } = vmState;
    public IReleaseSpec Spec { get; } = spec;
    public ulong CurrentArbosVersion { get; } = currentArbosVersion;

    public StylusEvmResult StylusCall(ExecutionType kind, Address to, ReadOnlyMemory<byte> input, ulong gasLeftReportedByRust, ulong gasRequestedByRust, in UInt256 value)
    {
        throw new NotImplementedException();
    }

    public StylusEvmResult StylusCreate(ReadOnlyMemory<byte> initCode, in UInt256 endowment, UInt256? salt, ulong gasLimit)
    {
        throw new NotImplementedException();
    }
}
