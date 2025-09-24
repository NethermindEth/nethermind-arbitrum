// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.IO.Abstractions;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Blocks;
using Nethermind.Blockchain.Receipts;
using Nethermind.Blockchain.Tracing.GethStyle;
using Nethermind.Blockchain.Tracing.GethStyle.Custom.JavaScript;
using Nethermind.Blockchain.Tracing.GethStyle.Custom.Native;
using Nethermind.Consensus.Tracing;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm.State;
using Nethermind.Evm.Tracing;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.State.OverridableEnv;

namespace Nethermind.Arbitrum.Tracing;

public class ArbGethStyleTracer(
    IReceiptStorage receiptStorage,
    IBlockTree blockTree,
    IBadBlockStore badBlockStore,
    ISpecProvider specProvider,
    ChangeableTransactionProcessorAdapter transactionProcessorAdapter,
    IFileSystem fileSystem,
    IOverridableEnv<GethStyleTracer.BlockProcessingComponents> blockProcessingEnv
    ) : GethStyleTracerBase(receiptStorage, blockTree, badBlockStore, specProvider, transactionProcessorAdapter, fileSystem, blockProcessingEnv)
{

    public static IBlockTracer<GethLikeTxTrace> CreateOptionsTracer(BlockHeader block, GethTraceOptions options, IWorldState worldState, ISpecProvider specProvider) =>
        options switch
        {
            { Tracer: var t } when GethLikeNativeTracerFactory.IsNativeTracer(t) => new GethLikeBlockNativeTracer(options.TxHash, (b, tx) => GethLikeNativeTracerFactory.CreateTracer(options, b, tx, worldState)),
            { Tracer.Length: > 0 } => new GethLikeBlockJavaScriptTracer(worldState, specProvider.GetSpec(block), options),
            _ => new ArbitrumGethLikeBlockTracer(options),
        };

    protected override IBlockTracer<GethLikeTxTrace> CreateOptionsTracerInternal(BlockHeader block,
        GethTraceOptions options, IWorldState worldState, ISpecProvider specProvider) =>
        CreateOptionsTracer(block, options, worldState, specProvider);
}
