// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

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
