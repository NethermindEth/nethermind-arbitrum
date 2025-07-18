// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Evm.Tracing.GethStyle;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Tracing;

public class ArbitrumGethLikeBlockTracer(GethTraceOptions options)
    : BlockTracerBase<GethLikeTxTrace, ArbitrumGethLikeTxTracer>(options.TxHash)
{
    protected override ArbitrumGethLikeTxTracer OnStart(Transaction? tx) => new(tx, options);

    protected override GethLikeTxTrace OnEnd(ArbitrumGethLikeTxTracer txTracer) => txTracer.BuildResult();
}
