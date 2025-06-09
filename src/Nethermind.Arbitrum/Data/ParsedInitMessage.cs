using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Data
{
    public class ParsedInitMessage(
        ulong chainId,
        UInt256 initialBaseFee,
        ChainConfig? chainConfigSpec = null,
        byte[]? serializedChainConfig = null) : IArbitrumTransactionData
    {
        public ulong ChainId = chainId;

        public UInt256 InitialBaseFee = initialBaseFee;

        public ChainConfig? ChainConfigSpec = chainConfigSpec;

        public byte[]? SerializedChainConfig = serializedChainConfig;
    }
}
