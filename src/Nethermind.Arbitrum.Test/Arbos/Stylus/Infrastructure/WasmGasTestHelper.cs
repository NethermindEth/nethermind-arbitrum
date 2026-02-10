// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Core.Test;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Specs.Forks;

namespace Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;

/// <summary>
/// Test helper for creating mock IStylusVmHost instances for WasmGas unit tests.
/// </summary>
public class WasmGasTestHelper : IDisposable
{
    private readonly StackAccessTracker _accessTracker;
    private readonly IWorldState _worldState;
    private readonly IDisposable _worldStateScope;
    private readonly IWasmStore _wasmStore;
    private VmState<ArbitrumGasPolicy> _vmState;
    private readonly TestStylusVmHost _vmHost;
    private ExecutionEnvironment _executionEnvironment;

    public IStylusVmHost VmHost => _vmHost;
    public IWorldState WorldState => _worldState;

    public WasmGasTestHelper(long gasAvailable = 1_000_000, IReleaseSpec? spec = null)
    {
        spec ??= Cancun.Instance;
        _accessTracker = new StackAccessTracker();
        _worldState = TestWorldStateFactory.CreateForTest();
        _worldStateScope = _worldState.BeginScope(IWorldState.PreGenesis);
        _wasmStore = TestWasmStore.Create();

        // Create minimal execution environment
        _executionEnvironment = ExecutionEnvironment.Rent(
            CodeInfo.Empty,
            Address.Zero,
            Address.Zero,
            Address.Zero,
            0, 0, 0,
            Array.Empty<byte>());

        _vmState = VmState<ArbitrumGasPolicy>.RentTopLevel(
            ArbitrumGasPolicy.FromLong(gasAvailable),
            ExecutionType.TRANSACTION,
            _executionEnvironment,
            _accessTracker,
            Snapshot.Empty);

        // Create execution contexts
        BlockHeader testHeader = Build.A.BlockHeader.TestObject;
        BlockExecutionContext blockContext = new(testHeader, spec);
        TxExecutionContext txContext = new(Address.Zero, null!, null, UInt256.Zero);

        _vmHost = new TestStylusVmHost(blockContext, txContext, _vmState, _worldState, _wasmStore, spec);
    }

    /// <summary>
    /// Pre-warms a storage cell so it's not cold.
    /// </summary>
    public void WarmUpSlot(Address address, in UInt256 index)
    {
        StorageCell cell = new(address, index);
        _accessTracker.WarmUp(in cell);
    }

    /// <summary>
    /// Pre-warms an address so it's not cold.
    /// </summary>
    public void WarmUpAddress(Address address)
    {
        _accessTracker.WarmUp(address);
    }

    /// <summary>
    /// Creates an account with balance.
    /// </summary>
    public void CreateAccount(Address address, UInt256 balance = default)
    {
        _worldState.CreateAccountIfNotExists(address, balance);
    }

    /// <summary>
    /// Sets a storage slot value.
    /// </summary>
    public void SetStorageValue(Address address, in UInt256 index, byte[] value)
    {
        _worldState.CreateAccountIfNotExists(address, 0);
        StorageCell cell = new(address, index);
        _worldState.Set(in cell, value);
    }

    public void Dispose()
    {
        _vmState.Dispose();
        _accessTracker.Dispose();
        _worldStateScope.Dispose();
    }
}
