// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Execution.Stateless;

public interface IStateReconstructor
{
    void EnsureStateAvailable(BlockHeader targetParent);
    void UpdateValidCandidateHeader(BlockHeader header);
    BlockHeader? TryPromoteValidCandidate(long validBlockNumber);
    void DereferenceRoot(Hash256 parentStateRoot);
    Task WaitForPruningGateAsync();
    void PreparedAddTrim(List<BlockHeader> headers);
    void ReorgTo(BlockHeader header);
    void CopyLastValidStateForFullPruning(long pruningBaseBlock, Action<BlockHeader> copyToNewDb);
}
