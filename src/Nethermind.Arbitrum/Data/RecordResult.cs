// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Consensus.Stateless;
using Nethermind.Core.Crypto;
using System.Text.Json.Serialization;

using WasmTarget = string;

namespace Nethermind.Arbitrum.Data;

public sealed class RecordResult
{
    public ulong Index { get; }
    public Hash256 BlockHash { get; }
    public Dictionary<Hash256, byte[]> Preimages { get; }
    public UserWasms? UserWasms { get; }

    [JsonIgnore]
    public Witness Witness { get; }

    public RecordResult(ulong messageIndex, Hash256 blockHash, Witness witness)
    {
        Index = messageIndex;
        BlockHash = blockHash;
        Witness = witness;
        UserWasms = null!; // TODO: add wasms

        Preimages = new();
        foreach (byte[] code in witness.Codes)
            Preimages.Add(Keccak.Compute(code), code);
        foreach (byte[] state in witness.State)
            Preimages.Add(Keccak.Compute(state), state);
        foreach (byte[] header in witness.Headers)
            Preimages.Add(Keccak.Compute(header), header);
    }
}

public sealed record class ActivatedWasm(
    Dictionary<WasmTarget, byte[]> Value
);

public sealed record class UserWasms(
    Dictionary<Hash256, ActivatedWasm> Value
);

