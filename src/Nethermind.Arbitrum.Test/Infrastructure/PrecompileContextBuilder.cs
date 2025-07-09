using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Core;
using Nethermind.Evm;
using Nethermind.Logging;
using Nethermind.Specs.Forks;
using Nethermind.State;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public record PrecompileTestContextBuilder : ArbitrumPrecompileExecutionContext
{
    public PrecompileTestContextBuilder(IWorldState worldState, ulong gasSupplied) : base(
        Address.Zero, gasSupplied, false, worldState, new BlockExecutionContext(), 0, null
    )
    { }

    public PrecompileTestContextBuilder WithArbosState()
    {
        ArbosState = ArbosState.OpenArbosState(WorldState, this, LimboLogs.Instance.GetClassLogger());
        return this;
    }

    public PrecompileTestContextBuilder WithBlockExecutionContext(BlockHeader blockHeader)
    {
        BlockExecutionContext = new(blockHeader, London.Instance);
        return this;
    }

    public PrecompileTestContextBuilder WithReleaseSpec()
    {
        ReleaseSpec = London.Instance;
        return this;
    }

    public PrecompileTestContextBuilder WithCaller(Address caller)
    {
        Caller = caller;
        return this;
    }

    public void ResetGasLeft(ulong gasLeft = 0)
    {
        GasLeft = gasLeft == 0 ? GasSupplied : gasLeft;
    }
}

