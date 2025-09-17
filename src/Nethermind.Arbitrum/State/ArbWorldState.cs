// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;
using Nethermind.Core.Collections;
using Nethermind.Core.Crypto;
using Nethermind.Core.Eip2930;
using Nethermind.Core.Specs;
using Nethermind.Evm.State;
using Nethermind.Evm.Tracing.State;
using Nethermind.Int256;
using Nethermind.State;

namespace Nethermind.Arbitrum.State;

public class ArbWorldState(IWorldState worldState): IWorldState, IPreBlockCaches
{
    private ArbosState? _arbosState = null;

    public ArbosState ArbosState
    {
        get
        {
            return _arbosState ?? throw new InvalidOperationException("ArbosState uninitialized in ArbWorldState. Please initialize before using it.");
        }
    }

    public void InitializeArbosState(ArbosState arbosState)
    {
        _arbosState = arbosState;
    }

    public void Restore(Snapshot snapshot)
    {
        worldState.Restore(snapshot);
    }

    public bool TryGetAccount(Address address, out AccountStruct account)
    {
        return worldState.TryGetAccount(address, out account);
    }

    public ref readonly UInt256 GetBalance(Address address)
    {
        return ref worldState.GetBalance(address);
    }

    public ref readonly ValueHash256 GetCodeHash(Address address)
    {
        return ref worldState.GetCodeHash(address);
    }

    public bool HasStateForBlock(BlockHeader? baseBlock)
    {
        return worldState.HasStateForBlock(baseBlock);
    }

    public byte[] GetOriginal(in StorageCell storageCell)
    {
        return worldState.GetOriginal(in storageCell);
    }

    public ReadOnlySpan<byte> Get(in StorageCell storageCell)
    {
        return worldState.Get(in storageCell);
    }

    public void Set(in StorageCell storageCell, byte[] newValue)
    {
        worldState.Set(in storageCell, newValue);
    }

    public ReadOnlySpan<byte> GetTransientState(in StorageCell storageCell)
    {
        return worldState.GetTransientState(in storageCell);
    }

    public void SetTransientState(in StorageCell storageCell, byte[] newValue)
    {
        worldState.SetTransientState(in storageCell, newValue);
    }

    public void Reset(bool resetBlockChanges = true)
    {
        worldState.Reset(resetBlockChanges);
    }

    public Snapshot TakeSnapshot(bool newTransactionStart = false)
    {
        return worldState.TakeSnapshot(newTransactionStart);
    }

    public void WarmUp(AccessList? accessList)
    {
        worldState.WarmUp(accessList);
    }

    public void WarmUp(Address address)
    {
        worldState.WarmUp(address);
    }

    public void ClearStorage(Address address)
    {
        worldState.ClearStorage(address);
    }

    public void RecalculateStateRoot()
    {
        worldState.RecalculateStateRoot();
    }

    public void DeleteAccount(Address address)
    {
        worldState.DeleteAccount(address);
    }

    public void CreateAccount(Address address, in UInt256 balance, in UInt256 nonce = default)
    {
        worldState.CreateAccount(address, in balance, in nonce);
    }

    public void CreateAccountIfNotExists(Address address, in UInt256 balance, in UInt256 nonce = default)
    {
        worldState.CreateAccountIfNotExists(address, in balance, in nonce);
    }

    public bool InsertCode(Address address, in ValueHash256 codeHash, ReadOnlyMemory<byte> code, IReleaseSpec spec, bool isGenesis = false)
    {
        return worldState.InsertCode(address, in codeHash, code, spec, isGenesis);
    }

    public void AddToBalance(Address address, in UInt256 balanceChange, IReleaseSpec spec)
    {
        worldState.AddToBalance(address, in balanceChange, spec);
    }

    public bool AddToBalanceAndCreateIfNotExists(Address address, in UInt256 balanceChange, IReleaseSpec spec)
    {
        return worldState.AddToBalanceAndCreateIfNotExists(address, in balanceChange, spec);
    }

    public void SubtractFromBalance(Address address, in UInt256 balanceChange, IReleaseSpec spec)
    {
        worldState.SubtractFromBalance(address, in balanceChange, spec);
    }

    public void IncrementNonce(Address address, UInt256 delta)
    {
        worldState.IncrementNonce(address, delta);
    }

    public void DecrementNonce(Address address, UInt256 delta)
    {
        worldState.DecrementNonce(address, delta);
    }

    public void SetNonce(Address address, in UInt256 nonce)
    {
        worldState.SetNonce(address, in nonce);
    }

    public void Commit(IReleaseSpec releaseSpec, bool isGenesis = false, bool commitRoots = true)
    {
        worldState.Commit(releaseSpec, isGenesis, commitRoots);
    }

    public void Commit(IReleaseSpec releaseSpec, IWorldStateTracer tracer, bool isGenesis = false, bool commitRoots = true)
    {
        worldState.Commit(releaseSpec, tracer, isGenesis, commitRoots);
    }

    public void CommitTree(long blockNumber)
    {
        worldState.CommitTree(blockNumber);
    }

    public ArrayPoolList<AddressAsKey>? GetAccountChanges()
    {
        return worldState.GetAccountChanges();
    }

    public void ResetTransient()
    {
        worldState.ResetTransient();
    }

    public Hash256 StateRoot => worldState.StateRoot;

    public byte[]? GetCode(Address address)
    {
        return worldState.GetCode(address);
    }

    public byte[]? GetCode(in ValueHash256 codeHash)
    {
        return worldState.GetCode(in codeHash);
    }

    public bool IsContract(Address address)
    {
        return worldState.IsContract(address);
    }

    public bool AccountExists(Address address)
    {
        return worldState.AccountExists(address);
    }

    public bool IsDeadAccount(Address address)
    {
        return worldState.IsDeadAccount(address);
    }

    public IDisposable BeginScope(BlockHeader? baseBlock)
    {
        return worldState.BeginScope(baseBlock);
    }

    public bool IsInScope => worldState.IsInScope;


    // for IPreBlockCaches
    public PreBlockCaches? Caches { get; } = (worldState as IPreBlockCaches)?.Caches;
    public bool IsWarmWorldState { get; } = (worldState as IPreBlockCaches)?.IsWarmWorldState ?? false;
}
