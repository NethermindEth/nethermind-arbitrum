// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Core;

public class ArbitrumBlockHeader : BlockHeader
{
    public override bool IsGenesis => Number == 22207817L;
    public UInt256 OriginalBaseFee { get; set; }

    public ArbitrumBlockHeader(BlockHeader original, UInt256 originalBaseFee) : base(
        original.ParentHash ?? Hash256.Zero,
        original.UnclesHash ?? Hash256.Zero,
        original.Beneficiary ?? Address.Zero,
        original.Difficulty,
        original.Number,
        original.GasLimit,
        original.Timestamp,
        original.ExtraData,
        original.BlobGasUsed,
        original.ExcessBlobGas,
        original.ParentBeaconBlockRoot,
        original.RequestsHash)
    {
        OriginalBaseFee = originalBaseFee;

        Author = original.Author;
        StateRoot = original.StateRoot;
        TxRoot = original.TxRoot;
        ReceiptsRoot = original.ReceiptsRoot;
        Bloom = original.Bloom?.Clone();
        GasUsed = original.GasUsed;
        MixHash = original.MixHash;
        Nonce = original.Nonce;
        Hash = original.Hash;
        TotalDifficulty = original.TotalDifficulty;
        AuRaSignature = original.AuRaSignature;
        AuRaStep = original.AuRaStep;
        BaseFeePerGas = original.BaseFeePerGas;
        WithdrawalsRoot = original.WithdrawalsRoot;
        IsPostMerge = original.IsPostMerge;
    }
}
