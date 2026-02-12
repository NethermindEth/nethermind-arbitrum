// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Consensus;
using Nethermind.Core;
using Nethermind.Core.Collections;
using Nethermind.Core.Crypto;
using Nethermind.Core.Eip2930;
using Nethermind.Core.Specs;
using Nethermind.Evm.State;
using Nethermind.Evm.Tracing.State;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Execution;

public class PrefetchAwareWorldState(IWorldState baseWorldState, IPrefetchManager prefetchManager) : IWorldState
{
    private readonly ArbitrumPrefetchManager? _prefetchManager = prefetchManager as ArbitrumPrefetchManager;

    public void Restore(Snapshot snapshot)
    {
        baseWorldState.Restore(snapshot);
    }

    public bool TryGetAccount(Address address, out AccountStruct account)
    {
        return baseWorldState.TryGetAccount(address, out account);
    }

    public ref readonly UInt256 GetBalance(Address address)
    {
        return ref baseWorldState.GetBalance(address);
    }

    public ref readonly ValueHash256 GetCodeHash(Address address)
    {
        return ref baseWorldState.GetCodeHash(address);
    }

    public bool HasStateForBlock(BlockHeader? baseBlock)
    {
        return baseWorldState.HasStateForBlock(baseBlock);
    }

    public byte[] GetOriginal(in StorageCell storageCell)
    {
        return baseWorldState.GetOriginal(in storageCell);
    }

    public ReadOnlySpan<byte> Get(in StorageCell storageCell)
    {
        return baseWorldState.Get(in storageCell);
    }

    public void Set(in StorageCell storageCell, byte[] newValue)
    {
        baseWorldState.Set(in storageCell, newValue);
    }

    public ReadOnlySpan<byte> GetTransientState(in StorageCell storageCell)
    {
        return baseWorldState.GetTransientState(in storageCell);
    }

    public void SetTransientState(in StorageCell storageCell, byte[] newValue)
    {
        baseWorldState.SetTransientState(in storageCell, newValue);
    }

    public void Reset(bool resetBlockChanges = true)
    {
        baseWorldState.Reset(resetBlockChanges);
    }

    public Snapshot TakeSnapshot(bool newTransactionStart = false)
    {
        return baseWorldState.TakeSnapshot(newTransactionStart);
    }

    public void WarmUp(AccessList? accessList)
    {
        baseWorldState.WarmUp(accessList);
    }

    public void WarmUp(Address address)
    {
        baseWorldState.WarmUp(address);
    }

    public void ClearStorage(Address address)
    {
        baseWorldState.ClearStorage(address);
    }

    public void RecalculateStateRoot()
    {
        baseWorldState.RecalculateStateRoot();
    }

    public void DeleteAccount(Address address)
    {
        baseWorldState.DeleteAccount(address);
    }

    public void CreateAccount(Address address, in UInt256 balance, in UInt256 nonce = default)
    {
        baseWorldState.CreateAccount(address, in balance, in nonce);
    }

    public void CreateAccountIfNotExists(Address address, in UInt256 balance, in UInt256 nonce = default)
    {
        baseWorldState.CreateAccountIfNotExists(address, in balance, in nonce);
    }

    public void CreateEmptyAccountIfDeleted(Address address)
    {
        baseWorldState.CreateEmptyAccountIfDeleted(address);
    }

    public bool InsertCode(Address address, in ValueHash256 codeHash, ReadOnlyMemory<byte> code, IReleaseSpec spec, bool isGenesis = false)
    {
        return baseWorldState.InsertCode(address, in codeHash, code, spec, isGenesis);
    }

    public void AddToBalance(Address address, in UInt256 balanceChange, IReleaseSpec spec)
    {
        baseWorldState.AddToBalance(address, in balanceChange, spec);
    }

    public bool AddToBalanceAndCreateIfNotExists(Address address, in UInt256 balanceChange, IReleaseSpec spec)
    {
        return baseWorldState.AddToBalanceAndCreateIfNotExists(address, in balanceChange, spec);
    }

    public void SubtractFromBalance(Address address, in UInt256 balanceChange, IReleaseSpec spec)
    {
        baseWorldState.SubtractFromBalance(address, in balanceChange, spec);
    }

    public void IncrementNonce(Address address, UInt256 delta)
    {
        baseWorldState.IncrementNonce(address, delta);
    }

    public void DecrementNonce(Address address, UInt256 delta)
    {
        baseWorldState.DecrementNonce(address, delta);
    }

    public void SetNonce(Address address, in UInt256 nonce)
    {
        baseWorldState.SetNonce(address, in nonce);
    }

    public void Commit(IReleaseSpec releaseSpec, IWorldStateTracer tracer, bool isGenesis = false, bool commitRoots = true)
    {
        //need to cancel prefetching before flushing any modified data to avoid prefetcher reading stale data from (data modified currently processed block)
        if (commitRoots)
            _prefetchManager?.CancelAndWait();

        baseWorldState.Commit(releaseSpec, tracer, isGenesis, commitRoots);
    }

    public void CommitTree(long blockNumber)
    {
        baseWorldState.CommitTree(blockNumber);
    }

    public ArrayPoolList<AddressAsKey>? GetAccountChanges()
    {
        return baseWorldState.GetAccountChanges();
    }

    public void ResetTransient()
    {
        baseWorldState.ResetTransient();
    }

    public Hash256 StateRoot => baseWorldState.StateRoot;

    public byte[]? GetCode(Address address)
    {
        return baseWorldState.GetCode(address);
    }

    public byte[]? GetCode(in ValueHash256 codeHash)
    {
        return baseWorldState.GetCode(in codeHash);
    }

    public bool IsContract(Address address)
    {
        return baseWorldState.IsContract(address);
    }

    public bool AccountExists(Address address)
    {
        return baseWorldState.AccountExists(address);
    }

    public bool IsDeadAccount(Address address)
    {
        return baseWorldState.IsDeadAccount(address);
    }

    public IDisposable BeginScope(BlockHeader? baseBlock)
    {
        return baseWorldState.BeginScope(baseBlock);
    }

    public bool IsInScope => baseWorldState.IsInScope;

    public IWorldStateScopeProvider ScopeProvider => baseWorldState.ScopeProvider;
}
