// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.IO;
using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Core;
using Nethermind.Logging;
using Nethermind.Serialization.Json;
using Nethermind.Specs.ChainSpecStyle;
using NUnit.Framework;

namespace Nethermind.Arbitrum.Test.Config;

public class ArbitrumChainSpecEngineParametersTests
{
    [Test]
    public void Load_FromChainSpecJson_LoadsEngineParamsCorrectly()
    {
        string chainSpecJson = @"{
  ""name"": ""Arbitrum Full Chain Simulation"",
  ""dataDir"": ""arbitrum-local"",
  ""engine"": {
    ""Arbitrum"": {
      ""enabled"": true,
      ""initialArbOSVersion"": 32,
      ""initialChainOwner"": ""0x5E1497dD1f08C87b2d8FE23e9AAB6c1De833D927"",
      ""genesisBlockNum"": 0,
      ""enableArbOS"": true,
      ""allowDebugPrecompiles"": true,
      ""dataAvailabilityCommittee"": false
    }
  },
  ""params"": {
    ""gasLimitBoundDivisor"": ""0x400"",
    ""accountStartNonce"": ""0x0"",
    ""networkID"": ""0x64aba""
  },
  ""genesis"": {
    ""seal"": {
      ""ethereum"": {
        ""nonce"": ""0x1"",
        ""mixHash"": ""0x0000000000000000000000000000000000000000000000200000000000000000""
      }
    },
    ""number"": ""0x0"",
    ""difficulty"": ""0x1"",
    ""author"": ""0x0000000000000000000000000000000000000000"",
    ""timestamp"": ""0x0"",
    ""parentHash"": ""0x0000000000000000000000000000000000000000000000000000000000000000"",
    ""extraData"": ""0x0000000000000000000000000000000000000000000000000000000000000000"",
    ""gasLimit"": ""0x4000000000000"",
    ""baseFeePerGas"": ""0x5f5e100""
  },
  ""nodes"": [],
  ""accounts"": {
    ""0000000000000000000000000000000000000000"": {
      ""balance"": ""0x1""
    }
  }
}";

        ChainSpec chainSpec = LoadChainSpecFromJson(chainSpecJson);

        chainSpec.SealEngineType.Should().Be(ArbitrumChainSpecEngineParameters.ArbitrumEngineName);

        ArbitrumChainSpecEngineParameters parameters = chainSpec.EngineChainSpecParametersProvider
            .GetChainSpecParameters<ArbitrumChainSpecEngineParameters>();

        ArbitrumChainSpecEngineParameters expected = new()
        {
            Enabled = true,
            InitialArbOSVersion = 32,
            InitialChainOwner = new Address("0x5E1497dD1f08C87b2d8FE23e9AAB6c1De833D927"),
            GenesisBlockNum = 0,
            EnableArbOS = true,
            AllowDebugPrecompiles = true,
            DataAvailabilityCommittee = false
        };

        parameters.Should().BeEquivalentTo(expected);
    }

    [Test]
    public void Create_WithChainSpecParameters_ReturnsCorrectValues()
    {
        ArbitrumChainSpecEngineParameters parameters = new()
        {
            Enabled = false,
            InitialArbOSVersion = 42,
            InitialChainOwner = new Address("0x1234567890123456789012345678901234567890"),
            GenesisBlockNum = 100,
            EnableArbOS = false,
            AllowDebugPrecompiles = false,
            DataAvailabilityCommittee = true,
            MaxCodeSize = 24576,
            MaxInitCodeSize = 49152
        };

        ArbitrumSpecHelper specHelper = new(parameters);

        ArbitrumSpecHelper expected = new(new ArbitrumChainSpecEngineParameters
        {
            Enabled = false,
            InitialArbOSVersion = 42,
            InitialChainOwner = new Address("0x1234567890123456789012345678901234567890"),
            GenesisBlockNum = 100,
            EnableArbOS = false,
            AllowDebugPrecompiles = false,
            DataAvailabilityCommittee = true,
            MaxCodeSize = 24576,
            MaxInitCodeSize = 49152
        });

        specHelper.Should().BeEquivalentTo(expected);
    }

    [Test]
    public void Create_WithNullParameters_UsesDefaultValues()
    {
        ArbitrumChainSpecEngineParameters parameters = new();

        ArbitrumSpecHelper specHelper = new(parameters);

        ArbitrumSpecHelper expected = new(new ArbitrumChainSpecEngineParameters
        {
            Enabled = true,
            InitialArbOSVersion = 32,
            InitialChainOwner = new Address("0x5E1497dD1f08C87b2d8FE23e9AAB6c1De833D927"),
            GenesisBlockNum = 0,
            EnableArbOS = true,
            AllowDebugPrecompiles = true,
            DataAvailabilityCommittee = false,
            MaxCodeSize = null,
            MaxInitCodeSize = null
        });

        specHelper.Should().BeEquivalentTo(expected);
    }

    private static ChainSpec LoadChainSpecFromJson(string json)
    {
        ChainSpecLoader loader = new(new EthereumJsonSerializer(), LimboLogs.Instance);

        using MemoryStream stream = new(System.Text.Encoding.UTF8.GetBytes(json));
        return loader.Load(stream);
    }
}
