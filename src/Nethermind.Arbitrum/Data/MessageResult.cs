// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Data
{
    public struct MessageResult : IEquatable<MessageResult>
    {
        public Hash256 BlockHash;
        public Hash256 SendRoot;

        public bool Equals(MessageResult other) => BlockHash.Equals(other.BlockHash) && SendRoot.Equals(other.SendRoot);
    }
}
