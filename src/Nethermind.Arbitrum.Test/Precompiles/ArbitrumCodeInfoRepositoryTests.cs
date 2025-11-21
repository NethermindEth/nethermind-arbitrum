// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.CodeAnalysis;
using NSubstitute;
using PrecompileInfo = Nethermind.Arbitrum.Precompiles.PrecompileInfo;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public class ArbitrumCodeInfoRepositoryTests
{
    [Test]
    public void GetCachedCodeInfo_RegularAddress_DelegatesToBase()
    {
        ICodeInfoRepository baseRepository = Substitute.For<ICodeInfoRepository>();
        ArbitrumCodeInfoRepository repository = new(baseRepository);

        Address regularAddress = new("0x1234567890123456789012345678901234567890");
        ArbitrumReleaseSpec spec = new()
        {
            ArbOsVersion = ArbosVersion.ThirtyTwo
        };

        ICodeInfo expectedCodeInfo = Substitute.For<ICodeInfo>();
        baseRepository.GetCachedCodeInfo(regularAddress, false, spec, out Arg.Any<Address?>())
            .Returns(x =>
            {
                x[3] = null;
                return expectedCodeInfo;
            });

        ICodeInfo result = repository.GetCachedCodeInfo(regularAddress, false, spec, out Address? delegationAddress);

        result.Should().BeSameAs(expectedCodeInfo, "Regular address should delegate to base repository");
        delegationAddress.Should().BeNull();
        baseRepository.Received(1).GetCachedCodeInfo(regularAddress, false, spec, out Arg.Any<Address?>());
    }

    [Test]
    public void GetCachedCodeInfo_InactiveArbitrumPrecompile_DelegatesToBase()
    {
        ICodeInfoRepository baseRepository = Substitute.For<ICodeInfoRepository>();
        ArbitrumCodeInfoRepository repository = new(baseRepository);

        Address arbWasmAddress = ArbosAddresses.ArbWasmAddress; // 0x71 - requires version 30+
        ArbitrumReleaseSpec spec = new();
        IReleaseSpec specInterface = spec;
        spec.ArbOsVersion = 29; // Before Stylus activation

        ICodeInfo expectedCodeInfo = Substitute.For<ICodeInfo>();
        baseRepository.GetCachedCodeInfo(arbWasmAddress, false, spec, out Arg.Any<Address?>())
            .Returns(x =>
            {
                x[3] = null;
                return expectedCodeInfo;
            });

        ICodeInfo result = repository.GetCachedCodeInfo(arbWasmAddress, false, spec, out Address? delegationAddress);

        specInterface.IsPrecompile(arbWasmAddress).Should().BeFalse("ArbWasm should be inactive at version 29");
        result.Should().BeSameAs(expectedCodeInfo, "Inactive precompile should be treated as regular account");
        delegationAddress.Should().BeNull();
        baseRepository.Received(1).GetCachedCodeInfo(arbWasmAddress, false, spec, out Arg.Any<Address?>());
    }

    [Test]
    public void GetCachedCodeInfo_ActiveArbitrumPrecompile_ReturnsArbitrumCodeInfo()
    {
        ICodeInfoRepository baseRepository = Substitute.For<ICodeInfoRepository>();
        ArbitrumCodeInfoRepository repository = new(baseRepository);

        Address arbSysAddress = ArbosAddresses.ArbSysAddress; // 0x64 - available from version 0
        ArbitrumReleaseSpec spec = new();
        IReleaseSpec specInterface = spec;
        spec.ArbOsVersion = 0;

        ICodeInfo result = repository.GetCachedCodeInfo(arbSysAddress, false, spec, out Address? delegationAddress);

        specInterface.IsPrecompile(arbSysAddress).Should().BeTrue("ArbSys should be active at version 0");
        result.Should().NotBeNull("Active Arbitrum precompile should return CodeInfo");
        result.Should().BeOfType<PrecompileInfo>("Should return Arbitrum precompile CodeInfo");
        delegationAddress.Should().BeNull();
        baseRepository.DidNotReceive().GetCachedCodeInfo(Arg.Any<Address>(), Arg.Any<bool>(), Arg.Any<ArbitrumReleaseSpec>(), out Arg.Any<Address?>());
    }

    [Test]
    public void GetCachedCodeInfo_EthereumPrecompile_DelegatesToBase()
    {
        ICodeInfoRepository baseRepository = Substitute.For<ICodeInfoRepository>();
        ArbitrumCodeInfoRepository repository = new(baseRepository);

        Address ecRecoverAddress = new("0x0000000000000000000000000000000000000001"); // EcRecover
        ArbitrumReleaseSpec spec = new();
        IReleaseSpec specInterface = spec;
        spec.ArbOsVersion = ArbosVersion.ThirtyTwo;

        ICodeInfo expectedCodeInfo = Substitute.For<ICodeInfo>();
        baseRepository.GetCachedCodeInfo(ecRecoverAddress, false, spec, out Arg.Any<Address?>())
            .Returns(x =>
            {
                x[3] = null;
                return expectedCodeInfo;
            });

        ICodeInfo result = repository.GetCachedCodeInfo(ecRecoverAddress, false, spec, out Address? delegationAddress);

        specInterface.IsPrecompile(ecRecoverAddress).Should().BeTrue("EcRecover is Ethereum precompile");
        result.Should().BeSameAs(expectedCodeInfo, "Ethereum precompile should delegate to base repository");
        delegationAddress.Should().BeNull();
        baseRepository.Received(1).GetCachedCodeInfo(ecRecoverAddress, false, spec, out Arg.Any<Address?>());
    }

    [Test]
    public void GetCachedCodeInfo_KzgAtVersion30_DelegatesToBase()
    {
        ICodeInfoRepository baseRepository = Substitute.For<ICodeInfoRepository>();
        ArbitrumCodeInfoRepository repository = new(baseRepository);

        Address kzgAddress = new("0x000000000000000000000000000000000000000a"); // KZG point evaluation
        ArbitrumReleaseSpec spec = new();
        IReleaseSpec specInterface = spec;
        spec.ArbOsVersion = ArbosVersion.Stylus; // Version 30
        spec.IsEip4844Enabled = false; // Arbitrum doesn't enable EIP-4844 but includes KZG

        ICodeInfo expectedCodeInfo = Substitute.For<ICodeInfo>();
        baseRepository.GetCachedCodeInfo(kzgAddress, false, spec, out Arg.Any<Address?>())
            .Returns(x =>
            {
                x[3] = null;
                return expectedCodeInfo;
            });

        ICodeInfo result = repository.GetCachedCodeInfo(kzgAddress, false, spec, out Address? delegationAddress);

        specInterface.IsPrecompile(kzgAddress).Should().BeTrue("KZG should be in spec at version 30");
        result.Should().BeSameAs(expectedCodeInfo, "KZG is Ethereum precompile, should delegate to base");
        delegationAddress.Should().BeNull();
        baseRepository.Received(1).GetCachedCodeInfo(kzgAddress, false, spec, out Arg.Any<Address?>());
    }

    [Test]
    public void GetCachedCodeInfo_GapAddress_DelegatesToBase()
    {
        ICodeInfoRepository baseRepository = Substitute.For<ICodeInfoRepository>();
        ArbitrumCodeInfoRepository repository = new(baseRepository);

        Address gapAddress = new("0x000000000000000000000000000000000000006a"); // Gap in Arbitrum precompiles
        ArbitrumReleaseSpec spec = new();
        IReleaseSpec specInterface = spec;
        spec.ArbOsVersion = ArbosVersion.ThirtyTwo;

        ICodeInfo expectedCodeInfo = Substitute.For<ICodeInfo>();
        baseRepository.GetCachedCodeInfo(gapAddress, false, spec, out Arg.Any<Address?>())
            .Returns(x =>
            {
                x[3] = null;
                return expectedCodeInfo;
            });

        ICodeInfo result = repository.GetCachedCodeInfo(gapAddress, false, spec, out Address? delegationAddress);

        specInterface.IsPrecompile(gapAddress).Should().BeFalse("0x6a is not a precompile");
        result.Should().BeSameAs(expectedCodeInfo, "Non-precompile address should delegate to base");
        delegationAddress.Should().BeNull();
        baseRepository.Received(1).GetCachedCodeInfo(gapAddress, false, spec, out Arg.Any<Address?>());
    }

    [Test]
    public void GetCachedCodeInfo_StylusPrecompile_ActivatesAtVersion30()
    {
        ICodeInfoRepository baseRepository = Substitute.For<ICodeInfoRepository>();
        ArbitrumCodeInfoRepository repository = new(baseRepository);

        Address arbWasmAddress = ArbosAddresses.ArbWasmAddress; // 0x71
        ArbitrumReleaseSpec spec = new();
        IReleaseSpec specInterface = spec;
        spec.ArbOsVersion = ArbosVersion.Stylus; // Version 30

        ICodeInfo result = repository.GetCachedCodeInfo(arbWasmAddress, false, spec, out Address? delegationAddress);

        specInterface.IsPrecompile(arbWasmAddress).Should().BeTrue("ArbWasm should be active at version 30");
        result.Should().NotBeNull("Active Stylus precompile should return CodeInfo");
        result.Should().BeOfType<PrecompileInfo>("Should return Arbitrum precompile CodeInfo");
        delegationAddress.Should().BeNull();
        baseRepository.DidNotReceive().GetCachedCodeInfo(Arg.Any<Address>(), Arg.Any<bool>(), Arg.Any<ArbitrumReleaseSpec>(), out Arg.Any<Address?>());
    }
}
