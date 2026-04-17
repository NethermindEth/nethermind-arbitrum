// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Execution.Stateless;

/// <summary>
/// No-op <see cref="IStateReconstructor"/> used when validation is disabled.
/// Produces no logs and performs no work.
/// </summary>
public sealed class NullStateReconstructor : IStateReconstructor
{
    public void EnsureStateAvailable(BlockHeader targetParent) { }
    public void UpdateValidCandidateHeader(BlockHeader header) { }
    public BlockHeader? TryPromoteValidCandidate(long validBlockNumber) => null;
    public void DereferenceRoot(Hash256 parentStateRoot) { }
    public Task WaitForPruningGateAsync() => Task.CompletedTask;
    public void WaitForPruningGate() { }
    public void PreparedAddTrim(List<BlockHeader> headers) { }
    public void ReorgTo(BlockHeader header) { }
    public void CopyLastValidStateForFullPruning(long pruningBaseBlock, Action<BlockHeader> copyToNewDb) { }
}
