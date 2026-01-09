// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Evm;
using Nethermind.Blockchain.Tracing;
using Nethermind.Blockchain.Tracing.GethStyle;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Tracing;

public class ArbitrumGethLikeBlockTracer(GethTraceOptions options)
    : BlockTracerBase<GethLikeTxTrace, ArbitrumGethLikeTxTracer, ArbitrumGas>(options.TxHash)
{
    protected override ArbitrumGethLikeTxTracer OnStart(Transaction? tx) => new(tx, options);

    protected override GethLikeTxTrace OnEnd(ArbitrumGethLikeTxTracer txTracer) => txTracer.BuildResult();
}
