using Nethermind.Api;
using Nethermind.Arbitrum.Execution;
using Nethermind.Consensus.Producers;
using Nethermind.Evm;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Init.Steps;
using Nethermind.State;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumInitializeBlockchainStep(INethermindApi api) : InitializeBlockchain(api)
{
    protected override IBlockProductionPolicy CreateBlockProductionPolicy() => AlwaysStartBlockProductionPolicy.Instance;

    protected override ITransactionProcessor CreateTransactionProcessor(CodeInfoRepository codeInfoRepository, IVirtualMachine virtualMachine, IWorldState worldState)
    {
        if (api.SpecProvider is null) throw new StepDependencyException(nameof(api.SpecProvider));

        return new ArbitrumTransactionProcessor(
            api.SpecProvider,
            worldState,
            virtualMachine,
            api.BlockTree,
            api.AbiEncoder,
            api.LogManager,
            codeInfoRepository
        );
    }
}
