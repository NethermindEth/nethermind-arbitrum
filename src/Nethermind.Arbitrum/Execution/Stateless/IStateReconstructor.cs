using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Execution.Stateless;

public interface IStateReconstructor
{
    void EnsureStateAvailable(BlockHeader targetParent);
    void DereferenceRoot(Hash256 parentStateRoot);
    void PreparedAddTrim(List<Hash256> stateRoots);
}
