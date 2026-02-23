// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Blockchain.Tracing;
using Nethermind.Blockchain.Tracing.GethStyle;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Tracing;

public class ArbitrumGethLikeBlockTracer(GethTraceOptions options)
    : BlockTracerBase<GethLikeTxTrace, ArbitrumGethLikeTxTracer>(options.TxHash)
{
    protected override ArbitrumGethLikeTxTracer OnStart(Transaction? tx) => new(tx, options);

    protected override GethLikeTxTrace OnEnd(ArbitrumGethLikeTxTracer txTracer) => txTracer.BuildResult();
}
