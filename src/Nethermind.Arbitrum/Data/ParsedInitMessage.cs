using System;
using Nethermind.Arbitrum.Data.DTO;
using Nethermind.Int256;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Data
{
    public class ParsedInitMessage(
        ulong chainId,
        UInt256 initialBaseFee,
        ChainConfigDTO? chainConfigSpec = null,
        byte[]? serializedChainConfig = null)
    {
        public ulong ChainId = chainId;

        public UInt256 InitialBaseFee = initialBaseFee;

        public ChainConfigDTO? ChainConfigSpec = chainConfigSpec;

        public byte[]? SerializedChainConfig = serializedChainConfig;
    }
}
