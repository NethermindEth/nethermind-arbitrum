// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Core;
using Nethermind.Specs.ChainSpecStyle;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nethermind.Arbitrum.Genesis;

public class ChainSpecInitMessageProvider(
    ChainSpec chainSpec,
    IArbitrumSpecHelper specHelper) : IInitMessageProvider
{
    public ParsedInitMessage GetInitMessage()
    {
        ChainConfig chainConfig = new()
        {
            ChainId = chainSpec.ChainId,
            HomesteadBlock = 0,
            DaoForkBlock = null,
            DaoForkSupport = true,
            Eip150Block = 0,
            Eip150Hash = specHelper.Eip150Hash,
            Eip155Block = 0,
            Eip158Block = 0,
            ByzantiumBlock = 0,
            ConstantinopleBlock = 0,
            PetersburgBlock = 0,
            IstanbulBlock = 0,
            MuirGlacierBlock = 0,
            BerlinBlock = 0,
            LondonBlock = 0,
            DepositContractAddress = specHelper.DepositContractAddress,
            Clique = new CliqueConfigDTO { Period = 0, Epoch = 0 },
            ArbitrumChainParams = new ArbitrumChainParams
            {
                EnableArbOS = specHelper.EnableArbOS,
                AllowDebugPrecompiles = specHelper.AllowDebugPrecompiles,
                DataAvailabilityCommittee = specHelper.DataAvailabilityCommittee,
                InitialArbOSVersion = specHelper.InitialArbOSVersion,
                InitialChainOwner = specHelper.InitialChainOwner ?? throw new InvalidOperationException("InitialChainOwner not found in chainspec"),
                GenesisBlockNum = specHelper.GenesisBlockNum
            }
        };

        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = specHelper.IncludeNullFields
                ? JsonIgnoreCondition.Never
                : JsonIgnoreCondition.WhenWritingNull,
            Converters = { new ChecksummedAddressConverter() }
        };

        byte[] serializedChainConfig = JsonSerializer.SerializeToUtf8Bytes(chainConfig, options);

        return new ParsedInitMessage(
            chainId: chainSpec.ChainId,
            initialBaseFee: specHelper.InitialL1BaseFee,
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
        writer.WriteStringValue(value.ToString(true, true));
    }
}
