// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Execution.Stateless;

public interface IStateReconstructor
{
    void EnsureStateAvailable(BlockHeader targetParent);
    void DereferenceRoot(Hash256 parentStateRoot);
    void PreparedAddTrim(List<(Hash256, ulong)> stateRoots);
}
