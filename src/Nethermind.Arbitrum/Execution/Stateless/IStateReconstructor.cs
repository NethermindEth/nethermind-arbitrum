// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Execution.Stateless;

public interface IStateReconstructor
{
    void EnsureStateAvailable(BlockHeader targetParent);
    void DereferenceRoot(Hash256 parentStateRoot);
    void PreparedAddTrim(List<Hash256> stateRoots);

    /// <summary>
    /// Updates the valid candidate header, keeping the oldest eligible one pinned in the MemDb overlay.
    /// Should be called for each header prepared in <see cref="PrepareForRecord"/>.
    /// Mirrors Nitro's <c>updateValidCandidateHdr</c>.
    /// </summary>
    void UpdateValidCandidateHdr(BlockHeader header);

    /// <summary>
    /// Attempts to promote the current candidate to the confirmed valid header.
    /// Releases the candidate's MemDb pin regardless of the outcome when the candidate is non-canonical.
    /// Returns the promoted header on success, <see langword="null"/> otherwise.
    /// Mirrors Nitro's <c>MarkValid</c> candidate promotion logic.
    /// </summary>
    BlockHeader? TryPromoteValidCandidate(long validBlockNumber);

    /// <summary>
    /// Writes any MemDb-resident trie nodes reachable from the confirmed valid header's state root
    /// into the provided key-value store. Used during shutdown to persist reconstructed nodes to the
    /// main state DB so they survive restart. Nodes already on disk are not re-written.
    /// </summary>
    void PersistValidStateTo(IWriteOnlyKeyValueStore db);
}
