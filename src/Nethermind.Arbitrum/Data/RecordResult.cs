// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Consensus.Stateless;
using Nethermind.Core.Crypto;

using WasmTarget = string;

namespace Nethermind.Arbitrum.Data
{
    // TODO: check type correctness (maybe class, record, etc.)
    public sealed class RecordResult
    {
        public ulong Index { get;  }
        public Hash256 BlockHash { get; }
        public Dictionary<Hash256, byte[]> Preimages { get; }
        public UserWasms? UserWasms { get; }

        public RecordResult(ulong messageIndex, Hash256 blockHash, Witness witness)
        {
            Index = messageIndex;
            BlockHash = blockHash;
            // UserWasms = new(new()); // TODO: add wasms
            UserWasms = null!; // TODO: add wasms

            Preimages = new();
            foreach (byte[] code in witness.Codes)
                Preimages.Add(Keccak.Compute(code), code);
            foreach (byte[] state in witness.State)
                Preimages.Add(Keccak.Compute(state), state);
            foreach (byte[] header in witness.Headers)
                Preimages.Add(Keccak.Compute(header), header);
            // foreach (byte[] key in witness.Keys)
            //     Preimages.Add(Keccak.Compute(key), key);
        }
    }

    public sealed record class ActivatedWasm(
        Dictionary<WasmTarget, byte[]> Value
    );

    public sealed record class UserWasms(
        Dictionary<Hash256, ActivatedWasm> Value
    );
}
