// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Execution.Transactions
{
    public readonly struct ArbitrumTxExecutionContext(
        Hash256? currentRetryable,
        Address? currentRefundTo)
    {
        public readonly Hash256? CurrentRetryable = currentRetryable;
        public readonly Address? CurrentRefundTo = currentRefundTo;
    }
}
