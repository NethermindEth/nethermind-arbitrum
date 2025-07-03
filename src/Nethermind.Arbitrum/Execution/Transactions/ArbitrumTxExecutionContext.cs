// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Execution.Transactions
{
    public class ArbitrumTxExecutionContext(
        Hash256? currentRetryable,
        Address? currentRefundTo,
        UInt256 posterFee = default,
        ulong posterGas = 0)
    {
        public Hash256? CurrentRetryable { get; } = currentRetryable;
        public Address? CurrentRefundTo { get; } = currentRefundTo;
        public UInt256 PosterFee { get; set; } = posterFee;
        public ulong PosterGas { get; set; } = posterGas;
    }
}
