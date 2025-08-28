using Autofac;
using Nethermind.Api;
using Nethermind.Arbitrum.TransactionProcessing;
using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Consensus;
using Nethermind.Core;
using Nethermind.Evm;
using Nethermind.Facade;
using Nethermind.Facade.Simulate;
using static Nethermind.Api.NethermindApi;

public class ArbitrumNethermindApi(Dependencies dependencies) : NethermindApi(dependencies)
{
    public IBlockhashProvider BlockHashProvider => Context.Resolve<IBlockhashProvider>();
    public IPrecompileChecker PrecompileChecker => Context.Resolve<IPrecompileChecker>();

    public override IBlockchainBridge CreateBlockchainBridge()
    {
        ReadOnlyBlockTree readOnlyTree = BlockTree!.AsReadOnly();

        ArbitrumOverridableTxProcessingEnv txProcessingEnv = new(
            WorldStateManager!.CreateOverridableWorldScope(),
            readOnlyTree,
            SpecProvider!,
            LogManager,
            PrecompileChecker);

        SimulateReadOnlyBlocksProcessingEnvFactory simulateReadOnlyBlocksProcessingEnvFactory =
            new SimulateReadOnlyBlocksProcessingEnvFactory(
                WorldStateManager!,
                readOnlyTree,
                DbProvider!,
                SpecProvider!,
                SimulateTransactionProcessorFactory,
                PrecompileChecker,
                LogManager);

        IMiningConfig miningConfig = ConfigProvider.GetConfig<IMiningConfig>();
        IBlocksConfig blocksConfig = ConfigProvider.GetConfig<IBlocksConfig>();

        return new BlockchainBridge(
            txProcessingEnv,
            simulateReadOnlyBlocksProcessingEnvFactory,
            TxPool,
            ReceiptFinder,
            FilterStore,
            FilterManager,
            EthereumEcdsa,
            Timestamper,
            LogFinder,
            SpecProvider!,
            blocksConfig,
            miningConfig.Enabled
        );
    }
}
