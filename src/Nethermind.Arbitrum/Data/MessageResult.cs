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

    public struct BulkMessageResult : IEquatable<BulkMessageResult>
    {
        public MessageResult[] Results;

        public bool Equals(BulkMessageResult other)
        {
            if (Results.Length != other.Results.Length)
            {
                return false;
            }
            for (int i = 0; i < Results.Length; i++)
            {
                if (!Results[i].Equals(other.Results[i]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
