// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
using Nethermind.Core;
using Nethermind.Core.Specs;

namespace Nethermind.Arbitrum.Test.Config;

[TestFixture]
public class ArbitrumReleaseSpecTests
{
    [Test]
    public void IsPrecompile_WithEthereumPrecompiles_ReturnsTrue()
    {
        IReleaseSpec spec = new ArbitrumReleaseSpec();

        // Ethereum precompiles (0x01-0x09)
        spec.IsPrecompile(new Address("0x0000000000000000000000000000000000000001")).Should().BeTrue("EcRecover should be a precompile");
        spec.IsPrecompile(new Address("0x0000000000000000000000000000000000000002")).Should().BeTrue("Sha256 should be a precompile");
        spec.IsPrecompile(new Address("0x0000000000000000000000000000000000000003")).Should().BeTrue("Ripemd160 should be a precompile");
        spec.IsPrecompile(new Address("0x0000000000000000000000000000000000000004")).Should().BeTrue("Identity should be a precompile");
    }

    [Test]
    public void IsPrecompile_WithArbitrumPrecompiles_ReturnsTrue()
    {
        IReleaseSpec spec = new ArbitrumReleaseSpec();

        // All 14 Arbitrum precompiles from block 11957941
        spec.IsPrecompile(ArbosAddresses.ArbSysAddress).Should().BeTrue("ArbSys (0x64) should be a precompile");
        spec.IsPrecompile(ArbosAddresses.ArbInfoAddress).Should().BeTrue("ArbInfo (0x65) should be a precompile");
        spec.IsPrecompile(ArbosAddresses.ArbAddressTableAddress).Should().BeTrue("ArbAddressTable (0x66) should be a precompile");
        spec.IsPrecompile(ArbosAddresses.ArbBLSAddress).Should().BeTrue("ArbBLS (0x67) should be a precompile");
        spec.IsPrecompile(ArbosAddresses.ArbFunctionTableAddress).Should().BeTrue("ArbFunctionTable (0x68) should be a precompile");
        spec.IsPrecompile(ArbosAddresses.ArbosTestAddress).Should().BeTrue("ArbosTest (0x69) should be a precompile");
        spec.IsPrecompile(ArbosAddresses.ArbOwnerPublicAddress).Should().BeTrue("ArbOwnerPublic (0x6b) should be a precompile");
        spec.IsPrecompile(ArbosAddresses.ArbGasInfoAddress).Should().BeTrue("ArbGasInfo (0x6c) should be a precompile");
        spec.IsPrecompile(ArbosAddresses.ArbAggregatorAddress).Should().BeTrue("ArbAggregator (0x6d) should be a precompile");
        spec.IsPrecompile(ArbosAddresses.ArbRetryableTxAddress).Should().BeTrue("ArbRetryableTx (0x6e) should be a precompile");
        spec.IsPrecompile(ArbosAddresses.ArbStatisticsAddress).Should().BeTrue("ArbStatistics (0x6f) should be a precompile");
        spec.IsPrecompile(ArbosAddresses.ArbOwnerAddress).Should().BeTrue("ArbOwner (0x70) should be a precompile");
        spec.IsPrecompile(ArbosAddresses.ArbDebugAddress).Should().BeTrue("ArbDebug (0xff) should be a precompile");
        spec.IsPrecompile(ArbosAddresses.ArbosAddress).Should().BeTrue("Arbos (0xa4b05) should be a precompile");
    }

    [Test]
    public void IsPrecompile_WithRegularAddresses_ReturnsFalse()
    {
        IReleaseSpec spec = new ArbitrumReleaseSpec();

        // Regular addresses that are NOT precompiles
        spec.IsPrecompile(new Address("0x1234567890123456789012345678901234567890")).Should().BeFalse("Random address should not be a precompile");
        spec.IsPrecompile(new Address("0x0000000000000000000000000000000000000063")).Should().BeFalse("0x63 is not a precompile");
        spec.IsPrecompile(new Address("0x000000000000000000000000000000000000006a")).Should().BeFalse("0x6a is not a precompile");
        spec.IsPrecompile(Address.Zero).Should().BeFalse("Zero address should not be a precompile");
    }
}
