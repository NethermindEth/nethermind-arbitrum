// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Core;
using Nethermind.Core.Collections;
using Nethermind.Core.Crypto;
using Nethermind.Core.Eip2930;
using Nethermind.Core.Specs;
using Nethermind.Evm.State;
using Nethermind.Evm.Tracing.State;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.State;

namespace Nethermind.Arbitrum.State;

public class ArbWorldState(IWorldState worldState, ILogManager logManager) : IWorldState, IPreBlockCaches
{
    private readonly ArbosStateFactory _arbosStateFactory = new(new ArbosStorageFactory(worldState, ArbosAddresses.ArbosSystemAccount), logManager);
    private readonly IWorldState _worldState = worldState;

    public ArbosState BuildArbosState(IBurner burner)
    {
        return _arbosStateFactory.Build(burner);
    }

    public void Restore(Snapshot snapshot)
    {
        _worldState.Restore(snapshot);
    }

    public bool TryGetAccount(Address address, out AccountStruct account)
    {
        return _worldState.TryGetAccount(address, out account);
    }

    public ref readonly UInt256 GetBalance(Address address)
    {
        return ref _worldState.GetBalance(address);
    }

    public ref readonly ValueHash256 GetCodeHash(Address address)
    {
        return ref _worldState.GetCodeHash(address);
    }

    public bool HasStateForBlock(BlockHeader? baseBlock)
    {
        return _worldState.HasStateForBlock(baseBlock);
    }

    public byte[] GetOriginal(in StorageCell storageCell)
    {
        return _worldState.GetOriginal(in storageCell);
    }

    public ReadOnlySpan<byte> Get(in StorageCell storageCell)
    {
        return _worldState.Get(in storageCell);
    }

    public void Set(in StorageCell storageCell, byte[] newValue)
    {
        _worldState.Set(in storageCell, newValue);
    }

    public ReadOnlySpan<byte> GetTransientState(in StorageCell storageCell)
    {
        return _worldState.GetTransientState(in storageCell);
    }

    public void SetTransientState(in StorageCell storageCell, byte[] newValue)
    {
        _worldState.SetTransientState(in storageCell, newValue);
    }

    public void Reset(bool resetBlockChanges = true)
    {
        _worldState.Reset(resetBlockChanges);
    }

    public Snapshot TakeSnapshot(bool newTransactionStart = false)
    {
        return _worldState.TakeSnapshot(newTransactionStart);
    }

    public void WarmUp(AccessList? accessList)
    {
        _worldState.WarmUp(accessList);
    }

    public void WarmUp(Address address)
    {
        _worldState.WarmUp(address);
    }

    public void ClearStorage(Address address)
    {
        _worldState.ClearStorage(address);
    }

    public void RecalculateStateRoot()
    {
        _worldState.RecalculateStateRoot();
    }

    public void DeleteAccount(Address address)
    {
        _worldState.DeleteAccount(address);
    }

    public void CreateAccount(Address address, in UInt256 balance, in UInt256 nonce = default)
    {
        _worldState.CreateAccount(address, in balance, in nonce);
    }

    public void CreateAccountIfNotExists(Address address, in UInt256 balance, in UInt256 nonce = default)
    {
        _worldState.CreateAccountIfNotExists(address, in balance, in nonce);
    }

    public bool InsertCode(Address address, in ValueHash256 codeHash, ReadOnlyMemory<byte> code, IReleaseSpec spec, bool isGenesis = false)
    {
        return _worldState.InsertCode(address, in codeHash, code, spec, isGenesis);
    }

    public void AddToBalance(Address address, in UInt256 balanceChange, IReleaseSpec spec)
    {
        _worldState.AddToBalance(address, in balanceChange, spec);
    }

    public bool AddToBalanceAndCreateIfNotExists(Address address, in UInt256 balanceChange, IReleaseSpec spec)
    {
        return _worldState.AddToBalanceAndCreateIfNotExists(address, in balanceChange, spec);
    }

    public void SubtractFromBalance(Address address, in UInt256 balanceChange, IReleaseSpec spec)
    {
        _worldState.SubtractFromBalance(address, in balanceChange, spec);
    }

    public void IncrementNonce(Address address, UInt256 delta)
    {
        _worldState.IncrementNonce(address, delta);
    }

    public void DecrementNonce(Address address, UInt256 delta)
    {
        _worldState.DecrementNonce(address, delta);
    }

    public void SetNonce(Address address, in UInt256 nonce)
    {
        _worldState.SetNonce(address, in nonce);
    }

    public void Commit(IReleaseSpec releaseSpec, bool isGenesis = false, bool commitRoots = true)
    {
        _worldState.Commit(releaseSpec, isGenesis, commitRoots);
    }

    public void Commit(IReleaseSpec releaseSpec, IWorldStateTracer tracer, bool isGenesis = false, bool commitRoots = true)
    {
        _worldState.Commit(releaseSpec, tracer, isGenesis, commitRoots);
    }

    public void CommitTree(long blockNumber)
    {
        _worldState.CommitTree(blockNumber);
    }

    public ArrayPoolList<AddressAsKey>? GetAccountChanges()
    {
        return _worldState.GetAccountChanges();
    }

    public void ResetTransient()
    {
        _worldState.ResetTransient();
    }

    public Hash256 StateRoot => _worldState.StateRoot;

    public byte[]? GetCode(Address address)
    {
        return _worldState.GetCode(address);
    }

    public byte[]? GetCode(in ValueHash256 codeHash)
    {
        return _worldState.GetCode(in codeHash);
    }

    public bool IsContract(Address address)
    {
        return _worldState.IsContract(address);
    }

    public bool AccountExists(Address address)
    {
        return _worldState.AccountExists(address);
    }

    public bool IsDeadAccount(Address address)
    {
        return _worldState.IsDeadAccount(address);
    }

    public IDisposable BeginScope(BlockHeader? baseBlock)
    {
        return _worldState.BeginScope(baseBlock);
    }

    public bool IsInScope => _worldState.IsInScope;


    // for IPreBlockCaches
    public PreBlockCaches? Caches { get; } = (worldState as IPreBlockCaches)?.Caches;
    public bool IsWarmWorldState { get; } = (worldState as IPreBlockCaches)?.IsWarmWorldState ?? false;
}
