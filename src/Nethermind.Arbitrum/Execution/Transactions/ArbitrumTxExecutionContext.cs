// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Execution.Transactions
{
    public struct ArbitrumTxExecutionContext(
        Hash256? currentRetryable,
        Address? currentRefundTo,
        UInt256 posterFee = default,
        ulong posterGas = 0)
    {

        public readonly Hash256? CurrentRetryable = currentRetryable;
        public readonly Address? CurrentRefundTo = currentRefundTo;
        public UInt256 PosterFee = posterFee;
        public readonly ulong PosterGas = posterGas;
    }
}
