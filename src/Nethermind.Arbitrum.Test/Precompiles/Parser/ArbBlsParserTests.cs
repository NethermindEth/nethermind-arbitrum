// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.CodeAnalysis;
using NSubstitute;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

[TestFixture]
public class ArbBlsTests
{
    [Test]
    public void Address_WhenQueried_ReturnsExpectedAddress()
    {
        Address expected = new("0x0000000000000000000000000000000000000067");

        ArbBls.Address.Should().Be(expected);
        ArbBls.Address.Should().Be(ArbosAddresses.ArbBLSAddress);
    }

    [Test]
    public void Abi_WhenQueried_ReturnsEmptyArray()
    {
        ArbBls.Abi.Should().Be("[]");
    }

    [Test]
    public void PrecompileFunctionDescription_WhenQueried_IsEmpty()
    {
        ArbBlsParser.PrecompileFunctionDescription.Should().BeEmpty();
    }

    [Test]
    public void PrecompileImplementation_WhenQueried_IsEmpty()
    {
        ArbBlsParser.PrecompileImplementation.Should().BeEmpty();
    }

    [Test]
    public void Instance_WhenAccessed_IsNotNull()
    {
        ArbBlsParser.Instance.Should().NotBeNull();
    }

    [Test]
    public void GetCachedCodeInfo_WhenQueriedFromCodeInfoRepository_ReturnsPrecompileInfo()
    {
        ICodeInfoRepository baseRepository = Substitute.For<ICodeInfoRepository>();
        IArbosVersionProvider arbosVersionProvider = Substitute.For<IArbosVersionProvider>();
        arbosVersionProvider.Get().Returns(ArbosVersion.FortyOne);

        ArbitrumCodeInfoRepository repository = new(baseRepository, arbosVersionProvider);
        IReleaseSpec spec = new ArbitrumReleaseSpec { ArbOsVersion = ArbosVersion.FortyOne };

        CodeInfo codeInfo = repository.GetCachedCodeInfo(
            ArbBls.Address,
            followDelegation: false,
            spec,
            out Address? delegationAddress);

        codeInfo.Should().NotBeNull();
        delegationAddress.Should().BeNull();
    }
}
