// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Blockchain.FullPruning;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Trie;

namespace Nethermind.Arbitrum.Execution.Stateless;

/// <summary>
/// Decorator for <see cref="IStateReader"/> that, when the visitor is a <see cref="ICopyTreeVisitor"/>
/// (i.e. full pruning is in progress), additionally runs the visitor over all validator state roots
/// that predate the pruning base block.
/// This ensures that MemDb-resident validator states are copied into the new pruning DB
/// and survive full pruning.
/// </summary>
public class ValidatorStatePreservingStateReader(
    IStateReader inner,
    StateReconstructor stateReconstructor,
    ReconstructedStateTrieStore reconStore,
    ILogManager logManager) : IStateReader
{
    private readonly StateTree _reconStateTree = new StateTree(reconStore, logManager);
    private readonly ILogger _logger = logManager.GetClassLogger<ValidatorStatePreservingStateReader>();

    public bool TryGetAccount(BlockHeader? baseBlock, Address address, out AccountStruct account)
        => inner.TryGetAccount(baseBlock, address, out account);

    public ReadOnlySpan<byte> GetStorage(BlockHeader? baseBlock, Address address, in UInt256 index)
        => inner.GetStorage(baseBlock, address, in index);

    public byte[]? GetCode(Hash256 codeHash) => inner.GetCode(codeHash);

    public byte[]? GetCode(in ValueHash256 codeHash) => inner.GetCode(in codeHash);

    public bool HasStateForBlock(BlockHeader? baseBlock) => inner.HasStateForBlock(baseBlock);

    public void RunTreeVisitor<TCtx>(ITreeVisitor<TCtx> treeVisitor, BlockHeader? baseBlock, VisitingOptions? visitingOptions = null)
        where TCtx : struct, INodeContext<TCtx>
    {
        inner.RunTreeVisitor(treeVisitor, baseBlock, visitingOptions);

        if (treeVisitor is not ICopyTreeVisitor || baseBlock is null)
            return;

        stateReconstructor.CopyAndCompactForFullPruning(validatorBlock =>
        {
            if (_logger.IsInfo)
                _logger.Info($"ValidatorStatePreservingStateReader: copying validator state for block {validatorBlock.Number} to pruning destination.");

            if (validatorBlock.StateRoot is not null)
                _reconStateTree.Accept(treeVisitor, validatorBlock.StateRoot, visitingOptions);
            else if (_logger.IsWarn)
                _logger.Warn($"ValidatorStatePreservingStateReader: skipping block {validatorBlock.Number} with null state root.");
        });
    }
}
