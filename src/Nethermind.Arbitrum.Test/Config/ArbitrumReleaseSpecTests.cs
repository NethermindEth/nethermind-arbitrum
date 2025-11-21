// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
using Nethermind.Core;
using Nethermind.Core.Precompiles;
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
        spec.IsPrecompile(PrecompiledAddresses.EcRecover).Should().BeTrue("EcRecover should be a precompile");
        spec.IsPrecompile(PrecompiledAddresses.Sha256).Should().BeTrue("Sha256 should be a precompile");
        spec.IsPrecompile(PrecompiledAddresses.Ripemd160).Should().BeTrue("Ripemd160 should be a precompile");
        spec.IsPrecompile(PrecompiledAddresses.Identity).Should().BeTrue("Identity should be a precompile");
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

    [Test]
    public void ArbOsVersion_WhenSetToSameValue_DoesNotClearCache()
    {
        ArbitrumReleaseSpec spec = new();
        IReleaseSpec specInterface = spec;
        spec.ArbOsVersion = ArbosVersion.Stylus;

        // Force cache population
        bool firstCheck = specInterface.IsPrecompile(ArbosAddresses.ArbWasmAddress);
        firstCheck.Should().BeTrue();

        // Manually set EVM cache to verify it's not cleared
        Array dummyCache = Array.Empty<object>();
        specInterface.EvmInstructionsNoTrace = dummyCache;

        // Setting to the same value should NOT clear caches
        spec.ArbOsVersion = ArbosVersion.Stylus;

        // Verify EVM cache was NOT cleared
        specInterface.EvmInstructionsNoTrace.Should().BeSameAs(dummyCache, "EVM cache should not be cleared when version unchanged");
    }

    [Test]
    public void ArbOsVersion_WhenSetToDifferentValue_ClearsCaches()
    {
        ArbitrumReleaseSpec spec = new();
        IReleaseSpec specInterface = spec;
        spec.ArbOsVersion = ArbosVersion.Stylus;

        // Force cache population and set EVM caches
        specInterface.IsPrecompile(ArbosAddresses.ArbWasmAddress);
        Array dummyNoTraceCache = Array.Empty<object>();
        Array dummyTracedCache = Array.Empty<object>();
        specInterface.EvmInstructionsNoTrace = dummyNoTraceCache;
        specInterface.EvmInstructionsTraced = dummyTracedCache;

        // Change version - should clear all caches
        spec.ArbOsVersion = ArbosVersion.ThirtyTwo;

        // Verify all caches were cleared
        specInterface.EvmInstructionsNoTrace.Should().BeNull("EvmInstructionsNoTrace should be cleared");
        specInterface.EvmInstructionsTraced.Should().BeNull("EvmInstructionsTraced should be cleared");

        // Verify precompile cache was rebuilt with a new version
        specInterface.IsPrecompile(ArbosAddresses.ArbWasmAddress).Should().BeTrue("Precompile cache should be rebuilt");
    }

    [Test]
    public void BuildPrecompilesCache_AtVersion0_IncludesOnlyBasePrecompiles()
    {
        ArbitrumReleaseSpec spec = new();
        IReleaseSpec specInterface = spec;
        spec.ArbOsVersion = 0;

        // Base Arbitrum precompiles (available from version 0)
        specInterface.IsPrecompile(ArbosAddresses.ArbSysAddress).Should().BeTrue("ArbSys available from version 0");
        specInterface.IsPrecompile(ArbosAddresses.ArbInfoAddress).Should().BeTrue("ArbInfo available from version 0");
        specInterface.IsPrecompile(ArbosAddresses.ArbAddressTableAddress).Should().BeTrue("ArbAddressTable available from version 0");

        // Stylus precompiles (available from version 30) should NOT be included
        specInterface.IsPrecompile(ArbosAddresses.ArbWasmAddress).Should().BeFalse("ArbWasm not available at version 0");
        specInterface.IsPrecompile(ArbosAddresses.ArbWasmCacheAddress).Should().BeFalse("ArbWasmCache not available at version 0");
    }

    [Test]
    public void BuildPrecompilesCache_AtVersion29_ExcludesStylusPrecompiles()
    {
        ArbitrumReleaseSpec spec = new();
        IReleaseSpec specInterface = spec;
        spec.ArbOsVersion = 29;

        // Base precompiles should be included
        specInterface.IsPrecompile(ArbosAddresses.ArbSysAddress).Should().BeTrue("ArbSys available at version 29");

        // Stylus precompiles should NOT be included (require version 30+)
        specInterface.IsPrecompile(ArbosAddresses.ArbWasmAddress).Should().BeFalse("ArbWasm requires version 30+");
        specInterface.IsPrecompile(ArbosAddresses.ArbWasmCacheAddress).Should().BeFalse("ArbWasmCache requires version 30+");
    }

    [Test]
    public void BuildPrecompilesCache_AtVersion30_IncludesStylusPrecompiles()
    {
        ArbitrumReleaseSpec spec = new();
        IReleaseSpec specInterface = spec;
        spec.ArbOsVersion = ArbosVersion.Stylus; // Version 30

        // Base precompiles should be included
        specInterface.IsPrecompile(ArbosAddresses.ArbSysAddress).Should().BeTrue("ArbSys available at version 30");

        // Stylus precompiles should be included (active from version 30)
        specInterface.IsPrecompile(ArbosAddresses.ArbWasmAddress).Should().BeTrue("ArbWasm available from version 30");
        specInterface.IsPrecompile(ArbosAddresses.ArbWasmCacheAddress).Should().BeTrue("ArbWasmCache available from version 30");
    }

    [Test]
    public void BuildPrecompilesCache_KzgIncluded_WhenEip4844Disabled()
    {
        ArbitrumReleaseSpec spec = new();
        IReleaseSpec specInterface = spec;
        spec.ArbOsVersion = ArbosVersion.Stylus;
        spec.IsEip4844Enabled = false;

        // KZG (0x0a) should be included even when EIP-4844 is disabled
        specInterface.IsPrecompile(PrecompiledAddresses.PointEvaluation).Should().BeTrue("KZG should be included for Arbitrum");
    }

    [Test]
    public void IsPrecompile_StylusPrecompile_BeforeVersion30_ReturnsFalse()
    {
        ArbitrumReleaseSpec spec = new();
        IReleaseSpec specInterface = spec;
        spec.ArbOsVersion = 29;

        // Stylus precompiles should not be active before version 30
        specInterface.IsPrecompile(ArbosAddresses.ArbWasmAddress).Should().BeFalse("ArbWasm inactive before version 30");
        specInterface.IsPrecompile(ArbosAddresses.ArbWasmCacheAddress).Should().BeFalse("ArbWasmCache inactive before version 30");
    }

    [Test]
    public void IsPrecompile_StylusPrecompile_AtVersion30_ReturnsTrue()
    {
        ArbitrumReleaseSpec spec = new();
        IReleaseSpec specInterface = spec;
        spec.ArbOsVersion = ArbosVersion.Stylus;

        // Stylus precompiles should be active at version 30
        specInterface.IsPrecompile(ArbosAddresses.ArbWasmAddress).Should().BeTrue("ArbWasm active at version 30");
        specInterface.IsPrecompile(ArbosAddresses.ArbWasmCacheAddress).Should().BeTrue("ArbWasmCache active at version 30");
    }

    [Test]
    public void BuildPrecompilesCache_AtVersion30_IncludesAllExpectedPrecompiles()
    {
        ArbitrumReleaseSpec spec = new();
        IReleaseSpec specInterface = spec;
        spec.ArbOsVersion = ArbosVersion.Thirty;

        // Enable base Ethereum EIPs (normally set by chainspec at block 0)
        spec.IsEip152Enabled = true;  // Blake2F (0x09)
        spec.IsEip196Enabled = true;  // EcAdd (0x06)
        spec.IsEip197Enabled = true;  // EcMul (0x07), EcPairing (0x08)
        spec.IsEip198Enabled = true;  // ModExp (0x05)

        // Enable RIP-7212 (P-256, added at v30)
        spec.IsRip7212Enabled = true;

        // Ethereum standard precompiles (0x01-0x0A)
        specInterface.IsPrecompile(PrecompiledAddresses.EcRecover).Should().BeTrue("ECRecover");
        specInterface.IsPrecompile(PrecompiledAddresses.Sha256).Should().BeTrue("SHA256");
        specInterface.IsPrecompile(PrecompiledAddresses.Ripemd160).Should().BeTrue("RIPEMD160");
        specInterface.IsPrecompile(PrecompiledAddresses.Identity).Should().BeTrue("Identity");
        specInterface.IsPrecompile(PrecompiledAddresses.ModExp).Should().BeTrue("ModExp");
        specInterface.IsPrecompile(PrecompiledAddresses.Bn128Add).Should().BeTrue("EcAdd");
        specInterface.IsPrecompile(PrecompiledAddresses.Bn128Mul).Should().BeTrue("EcMul");
        specInterface.IsPrecompile(PrecompiledAddresses.Bn128Pairing).Should().BeTrue("EcPairing");
        specInterface.IsPrecompile(PrecompiledAddresses.Blake2F).Should().BeTrue("Blake2F");
        specInterface.IsPrecompile(PrecompiledAddresses.PointEvaluation).Should().BeTrue("KZG Point Evaluation");

        // RIP-7212: P-256 precompile (added at v30)
        specInterface.IsPrecompile(PrecompiledAddresses.P256Verify).Should().BeTrue("P256Verify (RIP-7212)");

        // Arbitrum system precompiles (all versions)
        specInterface.IsPrecompile(ArbosAddresses.ArbSysAddress).Should().BeTrue("ArbSys");
        specInterface.IsPrecompile(ArbosAddresses.ArbInfoAddress).Should().BeTrue("ArbInfo");
        specInterface.IsPrecompile(ArbosAddresses.ArbAddressTableAddress).Should().BeTrue("ArbAddressTable");
        specInterface.IsPrecompile(ArbosAddresses.ArbBLSAddress).Should().BeTrue("ArbBLS");
        specInterface.IsPrecompile(ArbosAddresses.ArbFunctionTableAddress).Should().BeTrue("ArbFunctionTable");
        specInterface.IsPrecompile(ArbosAddresses.ArbosTestAddress).Should().BeTrue("ArbosTest");
        specInterface.IsPrecompile(ArbosAddresses.ArbOwnerPublicAddress).Should().BeTrue("ArbOwnerPublic");
        specInterface.IsPrecompile(ArbosAddresses.ArbGasInfoAddress).Should().BeTrue("ArbGasInfo");
        specInterface.IsPrecompile(ArbosAddresses.ArbAggregatorAddress).Should().BeTrue("ArbAggregator");
        specInterface.IsPrecompile(ArbosAddresses.ArbRetryableTxAddress).Should().BeTrue("ArbRetryableTx");
        specInterface.IsPrecompile(ArbosAddresses.ArbStatisticsAddress).Should().BeTrue("ArbStatistics");
        specInterface.IsPrecompile(ArbosAddresses.ArbOwnerAddress).Should().BeTrue("ArbOwner");
        specInterface.IsPrecompile(ArbosAddresses.ArbDebugAddress).Should().BeTrue("ArbDebug");
        specInterface.IsPrecompile(ArbosAddresses.ArbosAddress).Should().BeTrue("Arbos");

        // Stylus precompiles (added at v30)
        specInterface.IsPrecompile(ArbosAddresses.ArbWasmAddress).Should().BeTrue("ArbWasm (Stylus)");
        specInterface.IsPrecompile(ArbosAddresses.ArbWasmCacheAddress).Should().BeTrue("ArbWasmCache (Stylus)");
    }
}
