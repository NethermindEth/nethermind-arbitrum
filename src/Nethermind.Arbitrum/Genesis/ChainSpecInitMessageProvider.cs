using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Core;
using Nethermind.Specs.ChainSpecStyle;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Genesis;

public class ChainSpecInitMessageProvider(
    ChainSpec chainSpec,
    IArbitrumSpecHelper specHelper) : IInitMessageProvider
{
    public ParsedInitMessage GetInitMessage()
    {
        ulong initialArbOSVersion = specHelper.InitialArbOSVersion;
        Address initialChainOwner = specHelper.InitialChainOwner
                                    ?? throw new InvalidOperationException("InitialChainOwner not found in chainspec");
        ulong genesisBlockNum = specHelper.GenesisBlockNum;
        UInt256 initialL1BaseFee = specHelper.InitialL1BaseFee;

        ChainConfig chainConfig = new()
        {
            ChainId = chainSpec.ChainId,
            HomesteadBlock = 0,
            DaoForkBlock = null,
            DaoForkSupport = true,
            Eip150Block = 0,
            Eip150Hash = "0x0000000000000000000000000000000000000000000000000000000000000000",
            Eip155Block = 0,
            Eip158Block = 0,
            ByzantiumBlock = 0,
            ConstantinopleBlock = 0,
            PetersburgBlock = 0,
            IstanbulBlock = 0,
            MuirGlacierBlock = 0,
            BerlinBlock = 0,
            LondonBlock = 0,
            Clique = new CliqueConfigDTO { Period = 0, Epoch = 0 },
            ArbitrumChainParams = new ArbitrumChainParams
            {
                EnabledArbOS = true,
                AllowDebugPrecompiles = false,
                DataAvailabilityCommittee = false,
                InitialArbOSVersion = initialArbOSVersion,
                InitialChainOwner = initialChainOwner,
                GenesisBlockNum = genesisBlockNum
            }
        };

        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
            Converters = { new ChecksummedAddressConverter() }
        };

        byte[] serializedChainConfig = JsonSerializer.SerializeToUtf8Bytes(chainConfig, options);

        string jsonString = System.Text.Encoding.UTF8.GetString(serializedChainConfig);
        Console.WriteLine($"Generated ChainConfig JSON: {jsonString}");

        return new ParsedInitMessage(
            chainId: chainSpec.ChainId,
            initialBaseFee: initialL1BaseFee,
            chainConfigSpec: chainConfig,
            serializedChainConfig: serializedChainConfig
        );
    }
}

public class ChecksummedAddressConverter : JsonConverter<Address>
{
    public override Address Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? value = reader.GetString();
        return value is null ? Address.Zero : new Address(value);
    }

    public override void Write(Utf8JsonWriter writer, Address value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(true, true)); // checksummed
    }
}
