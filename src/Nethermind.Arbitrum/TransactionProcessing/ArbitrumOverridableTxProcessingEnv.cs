using Nethermind.Arbitrum.Evm;
using Nethermind.Blockchain;
using Nethermind.Consensus.Processing;
using Nethermind.Core.Specs;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Logging;
using Nethermind.State;

namespace Nethermind.Arbitrum.TransactionProcessing;

public class ArbitrumOverridableTxProcessingEnv(
    IOverridableWorldScope worldStateManager,
    IReadOnlyBlockTree readOnlyBlockTree,
    ISpecProvider specProvider,
    ILogManager logManager)
    : OverridableTxProcessingEnv(worldStateManager, readOnlyBlockTree, specProvider, logManager)
{
    protected override ITransactionProcessor CreateTransactionProcessor()
    {
        BlockhashProvider blockhashProvider = new(BlockTree, SpecProvider, StateProvider, LogManager);
        ArbVirtualMachine virtualMachine = new(blockhashProvider, SpecProvider, LogManager);
        return new ArbitrumTransactionProcessor(SpecProvider, StateProvider, virtualMachine, CodeInfoRepository, LogManager);
    }
}