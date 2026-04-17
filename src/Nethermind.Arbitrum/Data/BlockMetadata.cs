// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

namespace Nethermind.Arbitrum.Data;

// Mirrors Nitro's BlockMetadata.IsTxTimeboosted (go-ethereum/common/types.go)
public static class BlockMetadata
{
    public static bool? IsTxTimeboosted(byte[]? blockMetadata, int txIndex)
    {
        if (blockMetadata is null)
            return null;
        if (blockMetadata.Length == 0)
            return false;
        if (txIndex < 0)
            return false;

        int maxTxCount = (blockMetadata.Length - 1) * 8;
        if (txIndex >= maxTxCount)
            return false;

        return (blockMetadata[1 + txIndex / 8] & (1 << (txIndex % 8))) != 0;
    }
}
