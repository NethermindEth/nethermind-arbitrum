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
    public void GetCachedCodeInfo_WithRegularAddress_DelegatesToBase()
    {
        ArbitrumCodeInfoRepository repository = CreateRepository(ArbosVersion.ThirtyTwo, out ICodeInfoRepository baseRepository);
        Address regularAddress = new("0x1234567890123456789012345678901234567890");
        ArbitrumReleaseSpec spec = CreateSpec(ArbosVersion.ThirtyTwo);
        CodeInfo expectedCodeInfo = new(new byte[] { 0x00 });

        ConfigureBaseRepository(baseRepository, regularAddress, false, spec, expectedCodeInfo, delegationAddress: null);

        CodeInfo result = repository.GetCachedCodeInfo(regularAddress, false, spec, out Address? delegationAddress);

        result.Should().BeSameAs(expectedCodeInfo);
        delegationAddress.Should().BeNull();
        baseRepository.Received(1).GetCachedCodeInfo(regularAddress, false, spec, out Arg.Any<Address?>());
    }

    [Test]
    public void GetCachedCodeInfo_WithInactiveArbitrumPrecompile_DelegatesToBase()
    {
        ArbitrumCodeInfoRepository repository = CreateRepository(29, out ICodeInfoRepository baseRepository);
        Address arbWasmAddress = ArbosAddresses.ArbWasmAddress;
        ArbitrumReleaseSpec spec = CreateSpec(29);
        CodeInfo expectedCodeInfo = new(new byte[] { 0x00 });

        ConfigureBaseRepository(baseRepository, arbWasmAddress, false, spec, expectedCodeInfo, delegationAddress: null);

        CodeInfo result = repository.GetCachedCodeInfo(arbWasmAddress, false, spec, out Address? delegationAddress);

        ((IReleaseSpec)spec).IsPrecompile(arbWasmAddress).Should().BeFalse();
        result.Should().BeSameAs(expectedCodeInfo);
        delegationAddress.Should().BeNull();
    }

    [Test]
    public void GetCachedCodeInfo_WithActiveArbitrumPrecompile_ReturnsArbitrumCodeInfo()
    {
        ArbitrumCodeInfoRepository repository = CreateRepository(0, out _);
        Address arbSysAddress = ArbosAddresses.ArbSysAddress;
        ArbitrumReleaseSpec spec = CreateSpec(0);

        CodeInfo result = repository.GetCachedCodeInfo(arbSysAddress, false, spec, out Address? delegationAddress);

        ((IReleaseSpec)spec).IsPrecompile(arbSysAddress).Should().BeTrue();
        result.Should().BeOfType<PrecompileInfo>();
        delegationAddress.Should().BeNull();
    }

    [Test]
    public void GetCachedCodeInfo_WithEthereumPrecompile_DelegatesToBase()
    {
        ArbitrumCodeInfoRepository repository = CreateRepository(ArbosVersion.ThirtyTwo, out ICodeInfoRepository baseRepository);
        Address ecRecoverAddress = new("0x0000000000000000000000000000000000000001");
        ArbitrumReleaseSpec spec = CreateSpec(ArbosVersion.ThirtyTwo);
        CodeInfo expectedCodeInfo = new(new byte[] { 0x00 });

        ConfigureBaseRepository(baseRepository, ecRecoverAddress, false, spec, expectedCodeInfo, delegationAddress: null);

        CodeInfo result = repository.GetCachedCodeInfo(ecRecoverAddress, false, spec, out Address? delegationAddress);

        ((IReleaseSpec)spec).IsPrecompile(ecRecoverAddress).Should().BeTrue();
        result.Should().BeSameAs(expectedCodeInfo);
        delegationAddress.Should().BeNull();
    }

    [Test]
    public void GetCachedCodeInfo_WithKzgAtVersion30_DelegatesToBase()
    {
        ArbitrumCodeInfoRepository repository = CreateRepository(ArbosVersion.Stylus, out ICodeInfoRepository baseRepository);
        Address kzgAddress = new("0x000000000000000000000000000000000000000a");
        ArbitrumReleaseSpec spec = CreateSpec(ArbosVersion.Stylus);
        spec.IsEip4844Enabled = false;
        CodeInfo expectedCodeInfo = new(new byte[] { 0x00 });

        ConfigureBaseRepository(baseRepository, kzgAddress, false, spec, expectedCodeInfo, delegationAddress: null);

        CodeInfo result = repository.GetCachedCodeInfo(kzgAddress, false, spec, out Address? delegationAddress);

        ((IReleaseSpec)spec).IsPrecompile(kzgAddress).Should().BeTrue();
        result.Should().BeSameAs(expectedCodeInfo);
    }

    [Test]
    public void GetCachedCodeInfo_WithGapAddress_DelegatesToBase()
    {
        ArbitrumCodeInfoRepository repository = CreateRepository(ArbosVersion.ThirtyTwo, out ICodeInfoRepository baseRepository);
        Address gapAddress = new("0x000000000000000000000000000000000000006a");
        ArbitrumReleaseSpec spec = CreateSpec(ArbosVersion.ThirtyTwo);
        CodeInfo expectedCodeInfo = new(new byte[] { 0x00 });

        ConfigureBaseRepository(baseRepository, gapAddress, false, spec, expectedCodeInfo, delegationAddress: null);

        CodeInfo result = repository.GetCachedCodeInfo(gapAddress, false, spec, out Address? delegationAddress);

        ((IReleaseSpec)spec).IsPrecompile(gapAddress).Should().BeFalse();
        result.Should().BeSameAs(expectedCodeInfo);
    }

    [Test]
    public void GetCachedCodeInfo_WithStylusPrecompileAtVersion30_ReturnsArbitrumCodeInfo()
    {
        ArbitrumCodeInfoRepository repository = CreateRepository(ArbosVersion.Stylus, out _);
        Address arbWasmAddress = ArbosAddresses.ArbWasmAddress;
        ArbitrumReleaseSpec spec = CreateSpec(ArbosVersion.Stylus);

        CodeInfo result = repository.GetCachedCodeInfo(arbWasmAddress, false, spec, out Address? delegationAddress);

        ((IReleaseSpec)spec).IsPrecompile(arbWasmAddress).Should().BeTrue();
        result.Should().BeOfType<PrecompileInfo>();
        delegationAddress.Should().BeNull();
    }

    [Test]
    public void GetCachedCodeInfo_WithEip7702DelegationToPrecompileBeforeArbOS50_ReturnsPrecompileCode()
    {
        ArbitrumCodeInfoRepository repository = CreateRepository(ArbosVersion.Forty, out ICodeInfoRepository baseRepository);
        Address eoaAddress = new("0x1234567890123456789012345678901234567890");
        Address sha256Precompile = new("0x0000000000000000000000000000000000000002");
        ArbitrumReleaseSpec spec = CreateSpec(ArbosVersion.Forty);
        spec.IsEip7702Enabled = true;
        CodeInfo precompileCode = new(new byte[] { 0x00 });

        ConfigureBaseRepository(baseRepository, eoaAddress, true, spec, precompileCode, sha256Precompile);

        CodeInfo result = repository.GetCachedCodeInfo(eoaAddress, true, spec, out Address? delegationAddress);

        result.Should().BeSameAs(precompileCode);
        delegationAddress.Should().Be(sha256Precompile);
    }

    [Test]
    public void GetCachedCodeInfo_WithEip7702DelegationToPrecompileAfterArbOS50AndFollowDelegation_ReturnsEmpty()
    {
        ArbitrumCodeInfoRepository repository = CreateRepository(ArbosVersion.Fifty, out ICodeInfoRepository baseRepository);
        Address eoaAddress = new("0x1234567890123456789012345678901234567890");
        Address sha256Precompile = new("0x0000000000000000000000000000000000000002");
        ArbitrumReleaseSpec spec = CreateSpec(ArbosVersion.Fifty);
        spec.IsEip7702Enabled = true;
        CodeInfo precompileCode = new(new byte[] { 0x00 });

        ConfigureBaseRepository(baseRepository, eoaAddress, true, spec, precompileCode, sha256Precompile);

        CodeInfo result = repository.GetCachedCodeInfo(eoaAddress, true, spec, out Address? delegationAddress);

        result.Should().BeSameAs(CodeInfo.Empty);
        delegationAddress.Should().Be(sha256Precompile);
    }

    [Test]
    public void GetCachedCodeInfo_WithEip7702DelegationToPrecompileAfterArbOS50WithoutFollowDelegation_ReturnsDelegationCode()
    {
        ArbitrumCodeInfoRepository repository = CreateRepository(ArbosVersion.Fifty, out ICodeInfoRepository baseRepository);
        Address eoaAddress = new("0x1234567890123456789012345678901234567890");
        Address sha256Precompile = new("0x0000000000000000000000000000000000000002");
        ArbitrumReleaseSpec spec = CreateSpec(ArbosVersion.Fifty);
        spec.IsEip7702Enabled = true;
        CodeInfo delegationCodeInfo = new(new byte[] { 0x00 });

        ConfigureBaseRepository(baseRepository, eoaAddress, false, spec, delegationCodeInfo, sha256Precompile);

        CodeInfo result = repository.GetCachedCodeInfo(eoaAddress, false, spec, out Address? delegationAddress);

        result.Should().BeSameAs(delegationCodeInfo);
        delegationAddress.Should().Be(sha256Precompile);
    }

    [Test]
    public void GetCachedCodeInfo_WithEip7702DelegationToContractAfterArbOS50_ReturnsContractCode()
    {
        ArbitrumCodeInfoRepository repository = CreateRepository(ArbosVersion.Fifty, out ICodeInfoRepository baseRepository);
        Address eoaAddress = new("0x1234567890123456789012345678901234567890");
        Address contractAddress = new("0xabcdefabcdefabcdefabcdefabcdefabcdefabcd");
        ArbitrumReleaseSpec spec = CreateSpec(ArbosVersion.Fifty);
        spec.IsEip7702Enabled = true;
        CodeInfo contractCode = new(new byte[] { 0x60, 0x00 });

        ConfigureBaseRepository(baseRepository, eoaAddress, true, spec, contractCode, contractAddress);

        CodeInfo result = repository.GetCachedCodeInfo(eoaAddress, true, spec, out Address? delegationAddress);

        result.Should().BeSameAs(contractCode);
        delegationAddress.Should().Be(contractAddress);
    }

    [Test]
    public void GetCachedCodeInfo_WithoutDelegationAfterArbOS50_ReturnsNormalCode()
    {
        ArbitrumCodeInfoRepository repository = CreateRepository(ArbosVersion.Fifty, out ICodeInfoRepository baseRepository);
        Address normalAddress = new("0x1234567890123456789012345678901234567890");
        ArbitrumReleaseSpec spec = CreateSpec(ArbosVersion.Fifty);
        CodeInfo normalCode = new(new byte[] { 0x60, 0x00 });

        ConfigureBaseRepository(baseRepository, normalAddress, true, spec, normalCode, delegationAddress: null);

        CodeInfo result = repository.GetCachedCodeInfo(normalAddress, true, spec, out Address? delegationAddress);

        result.Should().BeSameAs(normalCode);
        delegationAddress.Should().BeNull();
    }

    [TestCase("0x0000000000000000000000000000000000000001")] // ECRecover
    [TestCase("0x0000000000000000000000000000000000000002")] // SHA256
    [TestCase("0x0000000000000000000000000000000000000003")] // RIPEMD160
    [TestCase("0x000000000000000000000000000000000000000a")] // KZG
    public void GetCachedCodeInfo_WithEip7702DelegationToAnyPrecompileAfterArbOS50_ReturnsEmpty(string precompileHex)
    {
        ArbitrumCodeInfoRepository repository = CreateRepository(ArbosVersion.Fifty, out ICodeInfoRepository baseRepository);
        Address eoaAddress = new("0x1234567890123456789012345678901234567890");
        Address precompile = new(precompileHex);
        ArbitrumReleaseSpec spec = CreateSpec(ArbosVersion.Fifty);
        spec.IsEip7702Enabled = true;
        CodeInfo precompileCode = new(new byte[] { 0x60, 0x00 });

        ConfigureBaseRepository(baseRepository, eoaAddress, true, spec, precompileCode, precompile);

        CodeInfo result = repository.GetCachedCodeInfo(eoaAddress, true, spec, out _);

        result.Should().BeSameAs(CodeInfo.Empty);
    }

    private static ArbitrumCodeInfoRepository CreateRepository(ulong arbosVersion, out ICodeInfoRepository baseRepository)
    {
        baseRepository = Substitute.For<ICodeInfoRepository>();
        IArbosVersionProvider versionProvider = Substitute.For<IArbosVersionProvider>();
        versionProvider.Get().Returns(arbosVersion);
        return new ArbitrumCodeInfoRepository(baseRepository, versionProvider);
    }

    private static ArbitrumReleaseSpec CreateSpec(ulong arbosVersion) => new() { ArbOsVersion = arbosVersion };

    private static void ConfigureBaseRepository(ICodeInfoRepository baseRepository, Address address, bool followDelegation,
        IReleaseSpec spec, CodeInfo returnCode, Address? delegationAddress)
    {
        baseRepository.GetCachedCodeInfo(address, followDelegation, spec, out Arg.Any<Address?>())
            .Returns(x =>
            {
                x[3] = delegationAddress;
                return returnCode;
            });
    }
}
