using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Precompiles;
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

public record PrecompileTestContextBuilder: ArbitrumPrecompileExecutionContext
{
    public PrecompileTestContextBuilder(IWorldState worldState, ulong gasSupplied) : base(
        Address.Zero, gasSupplied, NullTxTracer.Instance, false, worldState, new BlockExecutionContext(), 0
    ) {}

    public PrecompileTestContextBuilder WithArbosState()
    {
        ArbosState = ArbosState.OpenArbosState(this.WorldState, this, LimboLogs.Instance.GetClassLogger());
        return this;
    }

    public PrecompileTestContextBuilder WithBlockExecutionContext(Block block)
    {
        BlockExecutionContext = new(block.Header, London.Instance);
        return this;
    }

    public PrecompileTestContextBuilder WithTransactionProcessor()
    {
        // For now, block tree, spec provider, vm arguments do not matter
        BlockTree blockTree = Build.A.BlockTree(Build.A.Block.Genesis.TestObject).OfChainLength(1).TestObject;
        ISpecProvider specProvider = new TestSpecProvider(London.Instance);
        ArbitrumVirtualMachine virtualMachine = new(
            new TestBlockhashProvider(specProvider),
            specProvider,
            LimboLogs.Instance
        );

        TxProcessor = new ArbitrumTransactionProcessor(
            specProvider, WorldState, virtualMachine, blockTree, LimboLogs.Instance, new CodeInfoRepository()
        );

        return this;
    }

    public void SetGasLeft(ulong gasLeft)
    {
        GasLeft = gasLeft;
    }
}

