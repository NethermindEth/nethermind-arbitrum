using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Consensus.Producers;
using Nethermind.Consensus.Transactions;
using Nethermind.Core.Specs;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumBlockProducerTxSourceFactory(ISpecProvider chainSpec, ILogManager logManager) : IBlockProducerTxSourceFactory
{
    public ITxSource Create()
    {
        return new ArbitrumPayloadTxSource(chainSpec, logManager.GetClassLogger<ArbitrumPayloadTxSource>());
    }
}
