// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.IO.Abstractions;
using Nethermind.Arbitrum.Evm;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Blocks;
using Nethermind.Blockchain.Receipts;
using Nethermind.Blockchain.Tracing.GethStyle;
using Nethermind.Consensus.Tracing;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm.State;
using Nethermind.Evm.Tracing;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.State.OverridableEnv;

namespace Nethermind.Arbitrum.Tracing;

/// <summary>
/// Arbitrum-specific GethStyleTracer that uses ArbitrumGas and custom Arbitrum tracers.
/// This enables debug_traceTransaction to properly capture L1 gas information.
/// </summary>
public class ArbitrumGethStyleTracer(
    IReceiptStorage receiptStorage,
    IBlockTree blockTree,
    IBadBlockStore badBlockStore,
    ISpecProvider specProvider,
    ChangeableTransactionProcessorAdapter<ArbitrumGas> transactionProcessorAdapter,
    IFileSystem fileSystem,
    IOverridableEnv<GethStyleTracer<ArbitrumGas>.BlockProcessingComponents> blockProcessingEnv
) : GethStyleTracer<ArbitrumGas>(receiptStorage, blockTree, badBlockStore, specProvider, transactionProcessorAdapter, fileSystem, blockProcessingEnv)
{
    protected override IBlockTracer<ArbitrumGas, GethLikeTxTrace> CreateOptionsTracer(BlockHeader block, GethTraceOptions options, IWorldState worldState) =>
        new ArbitrumGethLikeBlockTracer(options);
}
