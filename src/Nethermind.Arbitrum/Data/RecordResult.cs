// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Execution.Stateless;
using Nethermind.Core.Crypto;
using System.Text.Json.Serialization;

namespace Nethermind.Arbitrum.Data;

public sealed class RecordResult
{
    public ulong Index { get; }
    public Hash256 BlockHash { get; }
    public Dictionary<Hash256, byte[]> Preimages { get; }
    public Dictionary<Hash256, IReadOnlyDictionary<string, byte[]>>? UserWasms { get; }

    [JsonIgnore]
    public ArbitrumWitness Witness { get; }

    public RecordResult(ulong messageIndex, Hash256 blockHash, ArbitrumWitness arbWitness)
    {
        Index = messageIndex;
        BlockHash = blockHash;
        Witness = arbWitness;
        UserWasms = arbWitness.UserWasms?.ToDictionary(
            kvp => kvp.Key.ToHash256(),
            kvp => kvp.Value);

        Preimages = new();
        foreach (byte[] code in arbWitness.Witness.Codes)
            Preimages.Add(Keccak.Compute(code), code);
        foreach (byte[] state in arbWitness.Witness.State)
            Preimages.Add(Keccak.Compute(state), state);
        foreach (byte[] header in arbWitness.Witness.Headers)
            Preimages.Add(Keccak.Compute(header), header);
    }
}
