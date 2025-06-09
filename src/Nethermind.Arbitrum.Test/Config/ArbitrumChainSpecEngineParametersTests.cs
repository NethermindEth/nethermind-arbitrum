// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.IO;
using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Core;
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

        parameters.Should().NotBeNull();
        parameters.Enabled.Should().Be(true);
        parameters.InitialArbOSVersion.Should().Be(32);
        parameters.InitialChainOwner.Should().Be(new Address("0x5E1497dD1f08C87b2d8FE23e9AAB6c1De833D927"));
        parameters.GenesisBlockNum.Should().Be(0);
        parameters.EnableArbOS.Should().Be(true);
        parameters.AllowDebugPrecompiles.Should().Be(true);
        parameters.DataAvailabilityCommittee.Should().Be(false);
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

        specHelper.Enabled.Should().Be(false);
        specHelper.InitialArbOSVersion.Should().Be(42);
        specHelper.InitialChainOwner.Should().Be(new Address("0x1234567890123456789012345678901234567890"));
        specHelper.GenesisBlockNum.Should().Be(100);
        specHelper.EnableArbOS.Should().Be(false);
        specHelper.AllowDebugPrecompiles.Should().Be(false);
        specHelper.DataAvailabilityCommittee.Should().Be(true);
        specHelper.MaxCodeSize.Should().Be(24576);
        specHelper.MaxInitCodeSize.Should().Be(49152);
    }

    [Test]
    public void Create_WithNullParameters_UsesDefaultValues()
    {
        ArbitrumChainSpecEngineParameters parameters = new();

        ArbitrumSpecHelper specHelper = new(parameters);

        specHelper.Enabled.Should().Be(true);
        specHelper.InitialArbOSVersion.Should().Be(32);
        specHelper.InitialChainOwner.Should().Be(new Address("0x5E1497dD1f08C87b2d8FE23e9AAB6c1De833D927"));
        specHelper.GenesisBlockNum.Should().Be(0);
        specHelper.EnableArbOS.Should().Be(true);
        specHelper.AllowDebugPrecompiles.Should().Be(true);
        specHelper.DataAvailabilityCommittee.Should().Be(false);
        specHelper.MaxCodeSize.Should().BeNull();
        specHelper.MaxInitCodeSize.Should().BeNull();
    }

    private static ChainSpec LoadChainSpecFromJson(string json)
    {
        ChainSpecLoader loader = new(new EthereumJsonSerializer());

        using MemoryStream stream = new(System.Text.Encoding.UTF8.GetBytes(json));
        return loader.Load(stream);
    }
}
