// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Execution.Stateless;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Data;

public sealed class RecordResult
{
    public ulong Index { get; }
    public Hash256 BlockHash { get; }
    public Dictionary<Hash256, byte[]> Preimages { get; }
    public Dictionary<Hash256, IReadOnlyDictionary<string, byte[]>>? UserWasms { get; }

    public RecordResult(ulong messageIndex, Hash256 blockHash, ArbitrumWitness arbWitness)
    {
        Index = messageIndex;
        BlockHash = blockHash;
        UserWasms = arbWitness.UserWasms?.ToDictionary(
            kvp => kvp.Key.ToHash256(),
            kvp => kvp.Value);

        // Witness codes, states and headers should all be unique, so, using Add() is safe here
        Preimages = new();
        foreach (byte[] code in arbWitness.Witness.Codes)
            Preimages.Add(Keccak.Compute(code), code);
        foreach (byte[] state in arbWitness.Witness.State)
            Preimages.Add(Keccak.Compute(state), state);
        foreach (byte[] header in arbWitness.Witness.Headers)
            Preimages.Add(Keccak.Compute(header), header);
    }
}
