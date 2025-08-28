using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution;
using Nethermind.Blockchain;
using Nethermind.Consensus.Processing;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Logging;
using Nethermind.State;

namespace Nethermind.Arbitrum.TransactionProcessing;

public class ArbitrumOverridableTxProcessingEnv(
    IOverridableWorldScope worldStateManager,
    IReadOnlyBlockTree readOnlyBlockTree,
    ISpecProvider specProvider,
    ILogManager logManager,
    IPrecompileChecker precompileChecker)
    : OverridableTxProcessingEnv(worldStateManager, readOnlyBlockTree, specProvider, logManager, precompileChecker)
{
    protected override ITransactionProcessor CreateTransactionProcessor()
    {
        BlockhashProvider blockhashProvider = new(BlockTree, SpecProvider, StateProvider, LogManager);
        ArbitrumVirtualMachine virtualMachine = new(blockhashProvider, SpecProvider, LogManager, PrecompileChecker);
        return new ArbitrumTransactionProcessor(SpecProvider, StateProvider, virtualMachine, readOnlyBlockTree, LogManager, CodeInfoRepository);
    }
}
