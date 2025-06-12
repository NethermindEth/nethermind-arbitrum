using System.Text;
using Nethermind.Arbitrum.Data;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public class FullChainSimulationInitMessage
{
    public static string SerializedChainConfig =
        "{\"chainId\":412346," +
        "\"homesteadBlock\":0," +
        "\"daoForkSupport\":true," +
        "\"eip150Block\":0," +
        "\"eip150Hash\":\"0x0000000000000000000000000000000000000000000000000000000000000000\"," +
        "\"eip155Block\":0," +
        "\"eip158Block\":0," +
        "\"byzantiumBlock\":0," +
        "\"constantinopleBlock\":0," +
        "\"petersburgBlock\":0," +
        "\"istanbulBlock\":0," +
        "\"muirGlacierBlock\":0," +
        "\"berlinBlock\":0," +
        "\"londonBlock\":0," +
        "\"clique\":{\"period\":0,\"epoch\":0}," +
        "\"arbitrum\":{" +
        "\"EnableArbOS\":true," +
        "\"AllowDebugPrecompiles\":true," +
        "\"DataAvailabilityCommittee\":false," +
        "\"InitialArbOSVersion\":32," +
        "\"InitialChainOwner\":\"0x5E1497dD1f08C87b2d8FE23e9AAB6c1De833D927\"," +
        "\"GenesisBlockNum\":0}}";

    public static DigestInitMessage CreateDigestInitMessage(UInt256 initialL1BaseFee)
    {
        return new DigestInitMessage(initialL1BaseFee, Convert.ToBase64String(Encoding.UTF8.GetBytes(SerializedChainConfig)));
    }
}
