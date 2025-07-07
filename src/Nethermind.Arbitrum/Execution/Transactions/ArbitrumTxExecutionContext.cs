// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Execution.Transactions
{
    public class ArbitrumTxExecutionContext
    {
        public Hash256? CurrentRetryable { get; set; }
        public Address? CurrentRefundTo { get; set; }
        public UInt256 PosterFee { get; set; }
        public ulong PosterGas { get; set; }
        public ulong ComputeHoldGas { get; set; }
    }
}
