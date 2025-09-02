using Nethermind.Core;
using Nethermind.Core.Collections;
using Nethermind.Core.Crypto;
using Nethermind.Core.Eip2930;
using Nethermind.Core.Specs;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.Evm.Tracing.State;
using Nethermind.Int256;
using Nethermind.Trie;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public record WorldStateSetRecord(Address Address, ValueHash256 CellHash, byte[] Value);

public class TrackingWorldState(IWorldState worldState) : IWorldState
{
    private readonly List<WorldStateSetRecord> _setRecords = new();
    public IReadOnlyList<WorldStateSetRecord> SetRecords => _setRecords;

    public ReadOnlySpan<byte> Get(in StorageCell storageCell)
    {
        return worldState.Get(in storageCell);
    }

    public void Set(in StorageCell storageCell, byte[] newValue)
    {
        _setRecords.Add(new(storageCell.Address, storageCell.Hash, newValue));

        worldState.Set(in storageCell, newValue);
    }

    public static TrackingWorldState CreateNewInMemory()
    {
        return new TrackingWorldState(TestWorldStateFactory.CreateForTest().GlobalWorldState);
    }

    #region Other wrapped methods

    public void Restore(Snapshot snapshot)
    {
        worldState.Restore(snapshot);
    }

    public bool TryGetAccount(Address address, out AccountStruct account)
    {
        return worldState.TryGetAccount(address, out account);
    }

    public IDisposable BeginScope(BlockHeader? baseBlock)
    {
        return worldState.BeginScope(baseBlock);
    }

    public bool IsInScope { get; }

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

    public Hash256 StateRoot
    {
        get => worldState.StateRoot;
        //set => worldState.StateRoot = value;
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

    public void UpdateStorageRoot(Address address, Hash256 storageRoot)
    {
        worldState.UpdateStorageRoot(address, storageRoot);
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

    public void Commit(IReleaseSpec releaseSpec, IWorldStateTracer? tracer, bool isGenesis = false, bool commitRoots = true)
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

    public byte[]? GetCode(Address address)
    {
        return worldState.GetCode(address);
    }

    public byte[]? GetCode(in ValueHash256 codeHash)
    {
        return worldState.GetCode(codeHash);
    }

    public byte[]? GetCode(Hash256 codeHash)
    {
        return worldState.GetCode(codeHash);
    }

    public byte[]? GetCode(ValueHash256 codeHash)
    {
        return worldState.GetCode(codeHash);
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
    #endregion
}
