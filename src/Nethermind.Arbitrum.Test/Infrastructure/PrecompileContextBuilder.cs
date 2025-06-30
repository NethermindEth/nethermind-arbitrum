using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.Test;
using Nethermind.Evm.Tracing;
using Nethermind.Logging;
using Nethermind.Specs;
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

    public PrecompileTestContextBuilder WithBlockExecutionContext(Block block)
    {
        BlockExecutionContext = new(block.Header, London.Instance);
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

