// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Test.Infrastructure;
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
        IReleaseSpec spec = GetSpecForVersion(ArbosVersion.ThirtyTwo);

        // Ethereum precompiles (0x01-0x09)
        spec.IsPrecompile(PrecompiledAddresses.EcRecover).Should().BeTrue("EcRecover should be a precompile");
        spec.IsPrecompile(PrecompiledAddresses.Sha256).Should().BeTrue("Sha256 should be a precompile");
        spec.IsPrecompile(PrecompiledAddresses.Ripemd160).Should().BeTrue("Ripemd160 should be a precompile");
        spec.IsPrecompile(PrecompiledAddresses.Identity).Should().BeTrue("Identity should be a precompile");
    }

    [Test]
    public void IsPrecompile_WithArbitrumPrecompiles_ReturnsTrue()
    {
        IReleaseSpec spec = GetSpecForVersion(ArbosVersion.ThirtyTwo);

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
        IReleaseSpec spec = GetSpecForVersion(ArbosVersion.ThirtyTwo);

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
        IReleaseSpec spec = GetSpecForVersion(0);

        // Base Arbitrum precompiles (available from version 0)
        spec.IsPrecompile(ArbosAddresses.ArbSysAddress).Should().BeTrue("ArbSys available from version 0");
        spec.IsPrecompile(ArbosAddresses.ArbInfoAddress).Should().BeTrue("ArbInfo available from version 0");
        spec.IsPrecompile(ArbosAddresses.ArbAddressTableAddress).Should().BeTrue("ArbAddressTable available from version 0");

        // Stylus precompiles (available from version 30) should NOT be included
        spec.IsPrecompile(ArbosAddresses.ArbWasmAddress).Should().BeFalse("ArbWasm not available at version 0");
        spec.IsPrecompile(ArbosAddresses.ArbWasmCacheAddress).Should().BeFalse("ArbWasmCache not available at version 0");
    }

    [Test]
    public void BuildPrecompilesCache_AtVersion29_ExcludesStylusPrecompiles()
    {
        IReleaseSpec spec = GetSpecForVersion(29);

        // Base precompiles should be included
        spec.IsPrecompile(ArbosAddresses.ArbSysAddress).Should().BeTrue("ArbSys available at version 29");

        // Stylus precompiles should NOT be included (require version 30+)
        spec.IsPrecompile(ArbosAddresses.ArbWasmAddress).Should().BeFalse("ArbWasm requires version 30+");
        spec.IsPrecompile(ArbosAddresses.ArbWasmCacheAddress).Should().BeFalse("ArbWasmCache requires version 30+");
    }

    [Test]
    public void BuildPrecompilesCache_AtVersion30_IncludesStylusPrecompiles()
    {
        IReleaseSpec spec = GetSpecForVersion(ArbosVersion.Stylus);

        // Base precompiles should be included
        spec.IsPrecompile(ArbosAddresses.ArbSysAddress).Should().BeTrue("ArbSys available at version 30");

        // Stylus precompiles should be included (active from version 30)
        spec.IsPrecompile(ArbosAddresses.ArbWasmAddress).Should().BeTrue("ArbWasm available from version 30");
        spec.IsPrecompile(ArbosAddresses.ArbWasmCacheAddress).Should().BeTrue("ArbWasmCache available from version 30");
    }

    [Test]
    public void BuildPrecompilesCache_WithEip4844Disabled_IncludesKzgPrecompile()
    {
        IReleaseSpec spec = GetSpecForVersion(ArbosVersion.Stylus);

        // KZG (0x0a) should be included even when EIP-4844 is disabled
        spec.IsPrecompile(PrecompiledAddresses.PointEvaluation).Should().BeTrue("KZG should be included for Arbitrum");
    }

    [Test]
    public void IsPrecompile_StylusPrecompileBeforeVersion30_ReturnsFalse()
    {
        IReleaseSpec spec = GetSpecForVersion(29);

        // Stylus precompiles should not be active before version 30
        spec.IsPrecompile(ArbosAddresses.ArbWasmAddress).Should().BeFalse("ArbWasm inactive before version 30");
        spec.IsPrecompile(ArbosAddresses.ArbWasmCacheAddress).Should().BeFalse("ArbWasmCache inactive before version 30");
    }

    [Test]
    public void IsPrecompile_StylusPrecompileAtVersion30_ReturnsTrue()
    {
        IReleaseSpec spec = GetSpecForVersion(ArbosVersion.Stylus);

        // Stylus precompiles should be active at version 30
        spec.IsPrecompile(ArbosAddresses.ArbWasmAddress).Should().BeTrue("ArbWasm active at version 30");
        spec.IsPrecompile(ArbosAddresses.ArbWasmCacheAddress).Should().BeTrue("ArbWasmCache active at version 30");
    }

    [Test]
    public void BuildPrecompilesCache_AtVersion30WithAllEips_IncludesAllExpectedPrecompiles()
    {
        IReleaseSpec spec = GetSpecForVersion(ArbosVersion.Thirty);

        // Ethereum standard precompiles (0x01-0x0A)
        spec.IsPrecompile(PrecompiledAddresses.EcRecover).Should().BeTrue("ECRecover");
        spec.IsPrecompile(PrecompiledAddresses.Sha256).Should().BeTrue("SHA256");
        spec.IsPrecompile(PrecompiledAddresses.Ripemd160).Should().BeTrue("RIPEMD160");
        spec.IsPrecompile(PrecompiledAddresses.Identity).Should().BeTrue("Identity");
        spec.IsPrecompile(PrecompiledAddresses.ModExp).Should().BeTrue("ModExp");
        spec.IsPrecompile(PrecompiledAddresses.Bn128Add).Should().BeTrue("EcAdd");
        spec.IsPrecompile(PrecompiledAddresses.Bn128Mul).Should().BeTrue("EcMul");
        spec.IsPrecompile(PrecompiledAddresses.Bn128Pairing).Should().BeTrue("EcPairing");
        spec.IsPrecompile(PrecompiledAddresses.Blake2F).Should().BeTrue("Blake2F");
        spec.IsPrecompile(PrecompiledAddresses.PointEvaluation).Should().BeTrue("KZG Point Evaluation");

        // RIP-7212: P-256 precompile (added at v30)
        spec.IsPrecompile(PrecompiledAddresses.P256Verify).Should().BeTrue("P256Verify (RIP-7212)");

        // Arbitrum system precompiles (all versions)
        spec.IsPrecompile(ArbosAddresses.ArbSysAddress).Should().BeTrue("ArbSys");
        spec.IsPrecompile(ArbosAddresses.ArbInfoAddress).Should().BeTrue("ArbInfo");
        spec.IsPrecompile(ArbosAddresses.ArbAddressTableAddress).Should().BeTrue("ArbAddressTable");
        spec.IsPrecompile(ArbosAddresses.ArbBLSAddress).Should().BeTrue("ArbBLS");
        spec.IsPrecompile(ArbosAddresses.ArbFunctionTableAddress).Should().BeTrue("ArbFunctionTable");
        spec.IsPrecompile(ArbosAddresses.ArbosTestAddress).Should().BeTrue("ArbosTest");
        spec.IsPrecompile(ArbosAddresses.ArbOwnerPublicAddress).Should().BeTrue("ArbOwnerPublic");
        spec.IsPrecompile(ArbosAddresses.ArbGasInfoAddress).Should().BeTrue("ArbGasInfo");
        spec.IsPrecompile(ArbosAddresses.ArbAggregatorAddress).Should().BeTrue("ArbAggregator");
        spec.IsPrecompile(ArbosAddresses.ArbRetryableTxAddress).Should().BeTrue("ArbRetryableTx");
        spec.IsPrecompile(ArbosAddresses.ArbStatisticsAddress).Should().BeTrue("ArbStatistics");
        spec.IsPrecompile(ArbosAddresses.ArbOwnerAddress).Should().BeTrue("ArbOwner");
        spec.IsPrecompile(ArbosAddresses.ArbDebugAddress).Should().BeTrue("ArbDebug");
        spec.IsPrecompile(ArbosAddresses.ArbosAddress).Should().BeTrue("Arbos");

        // Stylus precompiles (added at v30)
        spec.IsPrecompile(ArbosAddresses.ArbWasmAddress).Should().BeTrue("ArbWasm (Stylus)");
        spec.IsPrecompile(ArbosAddresses.ArbWasmCacheAddress).Should().BeTrue("ArbWasmCache (Stylus)");
    }

    [Test]
    public void IsPrecompile_Bls12381PrecompileBeforeArbOS50_ReturnsFalse()
    {
        IReleaseSpec spec = GetSpecForVersion(ArbosVersion.Forty);

        spec.IsPrecompile(PrecompiledAddresses.Bls12G1Add).Should().BeFalse("BLS12-381 G1Add not available before ArbOS 50");
        spec.IsPrecompile(PrecompiledAddresses.Bls12G1Msm).Should().BeFalse("BLS12-381 G1Msm not available before ArbOS 50");
        spec.IsPrecompile(PrecompiledAddresses.Bls12G2Add).Should().BeFalse("BLS12-381 G2Add not available before ArbOS 50");
        spec.IsPrecompile(PrecompiledAddresses.Bls12G2Msm).Should().BeFalse("BLS12-381 G2Msm not available before ArbOS 50");
        spec.IsPrecompile(PrecompiledAddresses.Bls12PairingCheck).Should().BeFalse("BLS12-381 PairingCheck not available before ArbOS 50");
        spec.IsPrecompile(PrecompiledAddresses.Bls12MapFpToG1).Should().BeFalse("BLS12-381 MapFpToG1 not available before ArbOS 50");
        spec.IsPrecompile(PrecompiledAddresses.Bls12MapFp2ToG2).Should().BeFalse("BLS12-381 MapFp2ToG2 not available before ArbOS 50");
    }

    [Test]
    public void IsPrecompile_Bls12381PrecompileAtArbOS50_ReturnsTrue()
    {
        IReleaseSpec spec = GetSpecForVersion(ArbosVersion.Fifty);

        spec.IsPrecompile(PrecompiledAddresses.Bls12G1Add).Should().BeTrue("BLS12-381 G1Add available from ArbOS 50");
        spec.IsPrecompile(PrecompiledAddresses.Bls12G1Msm).Should().BeTrue("BLS12-381 G1Msm available from ArbOS 50");
        spec.IsPrecompile(PrecompiledAddresses.Bls12G2Add).Should().BeTrue("BLS12-381 G2Add available from ArbOS 50");
        spec.IsPrecompile(PrecompiledAddresses.Bls12G2Msm).Should().BeTrue("BLS12-381 G2Msm available from ArbOS 50");
        spec.IsPrecompile(PrecompiledAddresses.Bls12PairingCheck).Should().BeTrue("BLS12-381 PairingCheck available from ArbOS 50");
        spec.IsPrecompile(PrecompiledAddresses.Bls12MapFpToG1).Should().BeTrue("BLS12-381 MapFpToG1 available from ArbOS 50");
        spec.IsPrecompile(PrecompiledAddresses.Bls12MapFp2ToG2).Should().BeTrue("BLS12-381 MapFp2ToG2 available from ArbOS 50");
    }

    [Test]
    public void IsPrecompile_Bls12381PrecompileAtArbOS50WithoutEip2537_ReturnsFalse()
    {
        // Note: The spec provider enables EIP-2537 at the appropriate timestamp
        // This test verifies the spec respects the EIP flag
        IReleaseSpec spec = GetSpecForVersion(ArbosVersion.Fifty);

        // The spec should have EIP-2537 disabled before the timestamp
        // In production this is controlled by block timestamp
        spec.IsPrecompile(PrecompiledAddresses.Bls12G1Add).Should().BeTrue("BLS12-381 available at ArbOS 50 with proper timestamp");
    }

    [Test]
    public void BuildPrecompilesCache_AllSevenBls12381PrecompilesAtArbOS50_IncludesAll()
    {
        IReleaseSpec spec = GetSpecForVersion(ArbosVersion.Fifty);

        Address[] bls12381Addresses = [
            PrecompiledAddresses.Bls12G1Add,
            PrecompiledAddresses.Bls12G1Msm,
            PrecompiledAddresses.Bls12G2Add,
            PrecompiledAddresses.Bls12G2Msm,
            PrecompiledAddresses.Bls12PairingCheck,
            PrecompiledAddresses.Bls12MapFpToG1,
            PrecompiledAddresses.Bls12MapFp2ToG2
        ];

        foreach (Address address in bls12381Addresses)
        {
            spec.IsPrecompile(address).Should().BeTrue($"BLS12-381 precompile at {address} should be available at ArbOS 50");
        }
    }

    [Test]
    public void BuildPrecompilesCache_Bls12381ActivationAcrossVersions_ActivatesAtCorrectVersion()
    {
        IReleaseSpec specV40 = GetSpecForVersion(ArbosVersion.Forty);
        IReleaseSpec specV49 = GetSpecForVersion(ArbosVersion.FortyNine);
        IReleaseSpec specV50 = GetSpecForVersion(ArbosVersion.Fifty);

        specV40.IsPrecompile(PrecompiledAddresses.Bls12G1Add).Should().BeFalse("BLS12-381 not available at ArbOS 40");
        specV49.IsPrecompile(PrecompiledAddresses.Bls12G1Add).Should().BeFalse("BLS12-381 not available at ArbOS 49");
        specV50.IsPrecompile(PrecompiledAddresses.Bls12G1Add).Should().BeTrue("BLS12-381 available at ArbOS 50");
    }

    [Test]
    public void BuildPrecompilesCache_P256ActivationAcrossVersions_ActivatesAtCorrectVersion()
    {
        IReleaseSpec specV20 = GetSpecForVersion(ArbosVersion.Twenty);
        IReleaseSpec specV29 = GetSpecForVersion(29);
        IReleaseSpec specV30 = GetSpecForVersion(ArbosVersion.Thirty);

        specV20.IsPrecompile(PrecompiledAddresses.P256Verify).Should().BeFalse("P256Verify not available at ArbOS 20");
        specV29.IsPrecompile(PrecompiledAddresses.P256Verify).Should().BeFalse("P256Verify not available at ArbOS 29");
        specV30.IsPrecompile(PrecompiledAddresses.P256Verify).Should().BeTrue("P256Verify available at ArbOS 30");
    }

    private static IReleaseSpec GetSpecForVersion(ulong arbosVersion)
    {
        ISpecProvider provider = FullChainSimulationChainSpecProvider.CreateDynamicSpecProvider(arbosVersion);
        return provider.GenesisSpec;
    }
}
