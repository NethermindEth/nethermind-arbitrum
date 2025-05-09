// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Data
{
    public struct MessageResult
    {
        public Hash256 BlockHash;
        public Hash256 SendRoot;
    }
}
