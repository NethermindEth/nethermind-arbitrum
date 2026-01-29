using Nethermind.Consensus.Stateless;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Execution.Stateless;

public class ArbitrumWitness(Witness witness, Dictionary<ValueHash256, IReadOnlyDictionary<string, byte[]>>? userWasms)
{
    private readonly Witness _witness = witness;

    public ref readonly Witness Witness => ref _witness;

    public Dictionary<ValueHash256, IReadOnlyDictionary<string, byte[]>>? UserWasms => userWasms;
}