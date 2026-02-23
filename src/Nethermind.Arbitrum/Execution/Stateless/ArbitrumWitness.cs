using Nethermind.Consensus.Stateless;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Execution.Stateless;

public record class ArbitrumWitness(Witness Witness, Dictionary<ValueHash256, IReadOnlyDictionary<string, byte[]>>? UserWasms) : IDisposable
{
    public void Dispose() => Witness.Dispose();
}
