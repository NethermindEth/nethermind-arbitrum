// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Reflection;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Core.Test;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.State;
using Nethermind.Logging;
using PrecompileInfo = Nethermind.Arbitrum.Precompiles.PrecompileInfo;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public class ArbitrumCodeInfoRepositoryTests
{
    [Test]
    public void GetCachedCodeInfo_WithRegularAddress_DelegatesToBase()
    {
        using IDisposable scope = BeginWorldStateScope(out IWorldState state);
        ArbitrumReleaseSpec spec = CreateSpec(ArbosVersion.ThirtyTwo);
        ArbitrumCodeInfoRepository repository = CreateRepository(state, ArbosVersion.ThirtyTwo);

        Address regularAddress = new("0x1234567890123456789012345678901234567890");
        byte[] runtimeCode = [0x60, 0x00, 0x60, 0x00];
        InsertCode(state, regularAddress, runtimeCode, spec);

        CodeInfo result = repository.GetCachedCodeInfo(regularAddress, false, spec, out Address? delegationAddress);

        result.Should().NotBeOfType<PrecompileInfo>();
        result.Code.Span.ToArray().Should().Equal(runtimeCode);
        delegationAddress.Should().BeNull();
    }

    [Test]
    public void GetCachedCodeInfo_WithInactiveArbitrumPrecompile_DelegatesToBase()
    {
        const ulong preStylusVersion = ArbosVersion.Stylus - 1;
        using IDisposable scope = BeginWorldStateScope(out IWorldState state);
        ArbitrumReleaseSpec spec = CreateSpec(preStylusVersion);
        ArbitrumCodeInfoRepository repository = CreateRepository(state, preStylusVersion);

        byte[] runtimeCode = [0x60, 0x00, 0x60, 0x00];
        InsertCode(state, ArbosAddresses.ArbWasmAddress, runtimeCode, spec);

        CodeInfo result = repository.GetCachedCodeInfo(ArbosAddresses.ArbWasmAddress, false, spec, out Address? delegationAddress);

        spec.IsPrecompile(ArbosAddresses.ArbWasmAddress).Should().BeFalse();
        result.Should().NotBeOfType<PrecompileInfo>();
        result.Code.Span.ToArray().Should().Equal(runtimeCode);
        delegationAddress.Should().BeNull();
    }

    [Test]
    public void GetCachedCodeInfo_WithActiveArbitrumPrecompile_ReturnsArbitrumCodeInfo()
    {
        using IDisposable scope = BeginWorldStateScope(out IWorldState state);
        ArbitrumReleaseSpec spec = CreateSpec(ArbosVersion.Zero);
        ArbitrumCodeInfoRepository repository = CreateRepository(state, ArbosVersion.Zero);

        CodeInfo result = repository.GetCachedCodeInfo(ArbosAddresses.ArbSysAddress, false, spec, out Address? delegationAddress);

        spec.IsPrecompile(ArbosAddresses.ArbSysAddress).Should().BeTrue();
        result.Should().BeOfType<PrecompileInfo>();
        delegationAddress.Should().BeNull();
    }

    [Test]
    public void GetCachedCodeInfo_WithEthereumPrecompile_DelegatesToBase()
    {
        using IDisposable scope = BeginWorldStateScope(out IWorldState state);
        ArbitrumReleaseSpec spec = CreateSpec(ArbosVersion.ThirtyTwo);
        ArbitrumCodeInfoRepository repository = CreateRepository(state, ArbosVersion.ThirtyTwo);

        Address ecRecoverAddress = new("0x0000000000000000000000000000000000000001");

        CodeInfo result = repository.GetCachedCodeInfo(ecRecoverAddress, false, spec, out Address? delegationAddress);

        spec.IsPrecompile(ecRecoverAddress).Should().BeTrue();
        result.Should().NotBeOfType<PrecompileInfo>("ECRecover is an Ethereum precompile, not an Arbitrum one");
        result.IsPrecompile.Should().BeTrue("EthereumCodeInfoRepository must recognize ECRecover as a precompile");
        delegationAddress.Should().BeNull();
    }

    [Test]
    public void GetCachedCodeInfo_WithKzgAtVersion30_DelegatesToBase()
    {
        using IDisposable scope = BeginWorldStateScope(out IWorldState state);
        ArbitrumReleaseSpec spec = CreateSpec(ArbosVersion.Stylus);
        spec.IsEip4844Enabled = false;
        ArbitrumCodeInfoRepository repository = CreateRepository(state, ArbosVersion.Stylus);

        Address kzgAddress = new("0x000000000000000000000000000000000000000a");

        CodeInfo result = repository.GetCachedCodeInfo(kzgAddress, false, spec, out _);

        spec.IsPrecompile(kzgAddress).Should().BeTrue("Arbitrum spec adds KZG as precompile from Stylus onward");
        result.Should().NotBeOfType<PrecompileInfo>("KZG is not an Arbitrum precompile; delegation to the Ethereum base is required");
    }

    [Test]
    public void GetCachedCodeInfo_WithGapAddress_DelegatesToBase()
    {
        using IDisposable scope = BeginWorldStateScope(out IWorldState state);
        ArbitrumReleaseSpec spec = CreateSpec(ArbosVersion.ThirtyTwo);
        ArbitrumCodeInfoRepository repository = CreateRepository(state, ArbosVersion.ThirtyTwo);

        Address gapAddress = new("0x000000000000000000000000000000000000006a");
        byte[] runtimeCode = [0x60, 0x00, 0x60, 0x00];
        InsertCode(state, gapAddress, runtimeCode, spec);

        CodeInfo result = repository.GetCachedCodeInfo(gapAddress, false, spec, out _);

        spec.IsPrecompile(gapAddress).Should().BeFalse();
        result.Should().NotBeOfType<PrecompileInfo>();
        result.Code.Span.ToArray().Should().Equal(runtimeCode);
    }

    [Test]
    public void GetCachedCodeInfo_WithStylusPrecompileAtVersion30_ReturnsArbitrumCodeInfo()
    {
        using IDisposable scope = BeginWorldStateScope(out IWorldState state);
        ArbitrumReleaseSpec spec = CreateSpec(ArbosVersion.Stylus);
        ArbitrumCodeInfoRepository repository = CreateRepository(state, ArbosVersion.Stylus);

        CodeInfo result = repository.GetCachedCodeInfo(ArbosAddresses.ArbWasmAddress, false, spec, out Address? delegationAddress);

        spec.IsPrecompile(ArbosAddresses.ArbWasmAddress).Should().BeTrue();
        result.Should().BeOfType<PrecompileInfo>();
        delegationAddress.Should().BeNull();
    }

    [Test]
    public void GetCachedCodeInfo_WithEip7702DelegationToPrecompileBeforeArbOS50_ReturnsEmptyFromBase()
    {
        // The Arbitrum-specific EIP-7702 fix (ArbitrumCodeInfoRepository.cs) activates at ArbOS 50+.
        // Pre-50 we return whatever the base returns. Current Nethermind base's code loader goes
        // via the delegatee's world-state code hash — a precompile has no stored code, so the base
        // already returns CodeInfo.Empty independently of the Arbitrum fix.
        using IDisposable scope = BeginWorldStateScope(out IWorldState state);
        ArbitrumReleaseSpec spec = CreateSpec(ArbosVersion.Forty, enableEip7702: true);
        ArbitrumCodeInfoRepository repository = CreateRepository(state, ArbosVersion.Forty);

        Address eoa = new("0x1234567890123456789012345678901234567890");
        Address sha256Precompile = new("0x0000000000000000000000000000000000000002");
        InsertEip7702Delegation(state, eoa, sha256Precompile, spec);

        CodeInfo result = repository.GetCachedCodeInfo(eoa, true, spec, out Address? delegationAddress);

        delegationAddress.Should().Be(sha256Precompile);
        result.Should().BeSameAs(CodeInfo.Empty);
    }

    [Test]
    public void GetCachedCodeInfo_WithEip7702DelegationToPrecompileAfterArbOS50AndFollowDelegation_ReturnsEmpty()
    {
        using IDisposable scope = BeginWorldStateScope(out IWorldState state);
        ArbitrumReleaseSpec spec = CreateSpec(ArbosVersion.Fifty, enableEip7702: true);
        ArbitrumCodeInfoRepository repository = CreateRepository(state, ArbosVersion.Fifty);

        Address eoa = new("0x1234567890123456789012345678901234567890");
        Address sha256Precompile = new("0x0000000000000000000000000000000000000002");
        InsertEip7702Delegation(state, eoa, sha256Precompile, spec);

        CodeInfo result = repository.GetCachedCodeInfo(eoa, true, spec, out Address? delegationAddress);

        result.Should().BeSameAs(CodeInfo.Empty);
        delegationAddress.Should().Be(sha256Precompile);
    }

    [Test]
    public void GetCachedCodeInfo_WithEip7702DelegationToPrecompileAfterArbOS50WithoutFollowDelegation_ReturnsDelegationCode()
    {
        using IDisposable scope = BeginWorldStateScope(out IWorldState state);
        ArbitrumReleaseSpec spec = CreateSpec(ArbosVersion.Fifty, enableEip7702: true);
        ArbitrumCodeInfoRepository repository = CreateRepository(state, ArbosVersion.Fifty);

        Address eoa = new("0x1234567890123456789012345678901234567890");
        Address sha256Precompile = new("0x0000000000000000000000000000000000000002");
        InsertEip7702Delegation(state, eoa, sha256Precompile, spec);

        CodeInfo result = repository.GetCachedCodeInfo(eoa, false, spec, out Address? delegationAddress);

        result.Should().NotBeSameAs(CodeInfo.Empty, "followDelegation=false must return the raw delegation code, not the EIP-7702 fix's Empty");
        delegationAddress.Should().Be(sha256Precompile);
    }

    [Test]
    public void GetCachedCodeInfo_WithEip7702DelegationToContractAfterArbOS50_ReturnsContractCode()
    {
        using IDisposable scope = BeginWorldStateScope(out IWorldState state);
        ArbitrumReleaseSpec spec = CreateSpec(ArbosVersion.Fifty, enableEip7702: true);
        ArbitrumCodeInfoRepository repository = CreateRepository(state, ArbosVersion.Fifty);

        Address eoa = new("0x1234567890123456789012345678901234567890");
        Address contractAddress = new("0xabcdefabcdefabcdefabcdefabcdefabcdefabcd");
        byte[] contractCode = [0x60, 0x00];
        InsertCode(state, contractAddress, contractCode, spec);
        InsertEip7702Delegation(state, eoa, contractAddress, spec);

        CodeInfo result = repository.GetCachedCodeInfo(eoa, true, spec, out Address? delegationAddress);

        delegationAddress.Should().Be(contractAddress);
        result.Code.Span.ToArray().Should().Equal(contractCode, "following a non-precompile delegation returns the delegatee contract's code");
    }

    [Test]
    public void GetCachedCodeInfo_WithoutDelegationAfterArbOS50_ReturnsNormalCode()
    {
        using IDisposable scope = BeginWorldStateScope(out IWorldState state);
        ArbitrumReleaseSpec spec = CreateSpec(ArbosVersion.Fifty);
        ArbitrumCodeInfoRepository repository = CreateRepository(state, ArbosVersion.Fifty);

        Address normalAddress = new("0x1234567890123456789012345678901234567890");
        byte[] normalCode = [0x60, 0x00];
        InsertCode(state, normalAddress, normalCode, spec);

        CodeInfo result = repository.GetCachedCodeInfo(normalAddress, true, spec, out Address? delegationAddress);

        result.Code.Span.ToArray().Should().Equal(normalCode);
        delegationAddress.Should().BeNull();
    }

    [TestCase("0x0000000000000000000000000000000000000001")] // ECRecover
    [TestCase("0x0000000000000000000000000000000000000002")] // SHA256
    [TestCase("0x0000000000000000000000000000000000000003")] // RIPEMD160
    [TestCase("0x000000000000000000000000000000000000000a")] // KZG
    public void GetCachedCodeInfo_WithEip7702DelegationToAnyPrecompileAfterArbOS50_ReturnsEmpty(string precompileHex)
    {
        using IDisposable scope = BeginWorldStateScope(out IWorldState state);
        ArbitrumReleaseSpec spec = CreateSpec(ArbosVersion.Fifty, enableEip7702: true);
        ArbitrumCodeInfoRepository repository = CreateRepository(state, ArbosVersion.Fifty);

        Address eoa = new("0x1234567890123456789012345678901234567890");
        Address precompile = new(precompileHex);
        InsertEip7702Delegation(state, eoa, precompile, spec);

        CodeInfo result = repository.GetCachedCodeInfo(eoa, true, spec, out _);

        result.Should().BeSameAs(CodeInfo.Empty);
    }

    [TestCaseSource(nameof(GetAllArbitrumPrecompileParsers))]
    public void GetCachedCodeInfo_ForRegisteredParser_ReturnsRoutablePrecompile(Type parserType)
    {
        using IDisposable scope = BeginWorldStateScope(out IWorldState state);
        ArbitrumReleaseSpec spec = CreateSpec(ArbosVersion.FortyOne);
        ArbitrumCodeInfoRepository repository = CreateRepository(state, ArbosVersion.FortyOne);

        CodeInfo result = repository.GetCachedCodeInfo(GetParserAddress(parserType), false, spec, out _);

        result.Should().BeOfType<PrecompileInfo>(
            $"Parser {parserType.Name} must be registered in ArbitrumCodeInfoRepository.InitializePrecompiledContracts()");
        IArbitrumPrecompile precompile = ((PrecompileInfo)result).ArbitrumPrecompile;
        precompile.GetType().Should().Be(parserType, $"Address {GetParserAddress(parserType)} must dispatch to {parserType.Name}");

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(state, 1_000_000)
            .WithArbosVersion(ArbosVersion.FortyOne);
        Action dispatch = () =>
        {
            ReadOnlySpan<byte> calldata = new byte[4];
            PrecompileHelper.TryCheckMethodVisibility(precompile, context,
                LimboLogs.Instance.GetClassLogger<ArbitrumCodeInfoRepositoryTests>(),
                ref calldata, out _, out _);
        };
        dispatch.Should().NotThrow($"PrecompileHelper.TryCheckMethodVisibility must have a switch case for {parserType.Name}");
    }

    [Test]
    public void GetAllArbitrumPrecompileParsers_ViaReflection_DiscoversExpectedCount()
    {
        // Guards the parameterised exhaustiveness test against vacuous passes: if the
        // reflection scan ever returns empty, TestCaseSource produces zero test cases silently.
        GetAllArbitrumPrecompileParsers().Should()
            .HaveCountGreaterThanOrEqualTo(17,"Reflection must discover every IArbitrumPrecompile<T> implementation in the assembly");
    }

    private static IDisposable BeginWorldStateScope(out IWorldState state)
    {
        state = TestWorldStateFactory.CreateForTest();
        IDisposable scope = state.BeginScope(IWorldState.PreGenesis);
        try
        {
            ArbOSInitialization.Create(state);
            return scope;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    private static ArbitrumCodeInfoRepository CreateRepository(IWorldState state, ulong arbosVersion)
        => new(new EthereumCodeInfoRepository(state), new TestArbosVersionProvider(arbosVersion));

    private static ArbitrumReleaseSpec CreateSpec(ulong arbosVersion, bool enableEip7702 = false)
        => new() { ArbOsVersion = arbosVersion, IsEip7702Enabled = enableEip7702 };

    private static void InsertCode(IWorldState state, Address address, byte[] code, IReleaseSpec spec)
    {
        state.CreateAccountIfNotExists(address, balance: 0);
        state.InsertCode(address, Keccak.Compute(code), code, spec, false);
        state.Commit(spec);
    }

    private static void InsertEip7702Delegation(IWorldState state, Address from, Address to, IReleaseSpec spec)
        => InsertCode(state, from, [.. Eip7702Constants.DelegationHeader, .. to.Bytes], spec);

    private static IEnumerable<Type> GetAllArbitrumPrecompileParsers()
    {
        Type?[] types;
        try
        {
            types = typeof(IArbitrumPrecompile).Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types;
        }

        return types.Where(t => t is { IsAbstract: false, IsInterface: false }
            && t.GetInterfaces().Any(i => i.IsGenericType
                && i.GetGenericTypeDefinition() == typeof(IArbitrumPrecompile<>)))!;
    }

    private static Address GetParserAddress(Type parserType)
    {
        PropertyInfo addressProperty = parserType.GetProperty("Address", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Parser {parserType.Name} must expose a static Address property");
        return (Address)(addressProperty.GetValue(null)
            ?? throw new InvalidOperationException($"{parserType.Name}.Address must not be null"));
    }
}
