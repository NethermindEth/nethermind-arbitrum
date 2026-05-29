// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.State;
using Nethermind.Trie;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public sealed class FakeStateReader : IStateReader
{
    private readonly Dictionary<Address, AccountStruct> _accounts = new();
    private readonly Dictionary<(Address, UInt256), byte[]> _storage = new();

    public void SetAccount(Address address, AccountStruct account)
        => _accounts[address] = account;

    public void SetStorage(Address address, UInt256 slot, byte[] value)
        => _storage[(address, slot)] = value;

    public bool TryGetAccount(BlockHeader? baseBlock, Address address, out AccountStruct account)
        => _accounts.TryGetValue(address, out account);

    public ReadOnlySpan<byte> GetStorage(BlockHeader? baseBlock, Address address, in UInt256 index)
        => _storage.TryGetValue((address, index), out byte[]? value) ? value : ReadOnlySpan<byte>.Empty;

    public byte[]? GetCode(Hash256 codeHash) => throw new NotImplementedException();
    public byte[]? GetCode(in ValueHash256 codeHash) => throw new NotImplementedException();

    public void RunTreeVisitor<TCtx>(ITreeVisitor<TCtx> treeVisitor, BlockHeader? baseBlock, VisitingOptions? visitingOptions = null, VisitingStats? diagnostics = null)
        where TCtx : struct, INodeContext<TCtx>
        => throw new NotImplementedException();

    public bool HasStateForBlock(BlockHeader? baseBlock) => throw new NotImplementedException();
}
