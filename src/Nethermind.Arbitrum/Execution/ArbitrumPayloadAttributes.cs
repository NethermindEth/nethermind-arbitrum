// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Data;
using Nethermind.Consensus.Producers;

namespace Nethermind.Arbitrum.Execution
{
    public class ArbitrumPayloadAttributes : PayloadAttributes
    {
        public MessageWithMetadata? MessageWithMetadata { get; set; }
        public long Number { get; set; }
        public ulong PreviousArbosVersion { get; set; }
    }
}
