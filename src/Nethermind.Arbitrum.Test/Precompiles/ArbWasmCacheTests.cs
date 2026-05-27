// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Buffers.Binary;
using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Solgen = Nethermind.Arbitrum.Precompiles.Solgen;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public class ArbWasmCacheTests
{
    private static readonly Address ManagerA = new("0x0000000000000000000000000000000000000aaa");
    private static readonly Address ManagerB = new("0x0000000000000000000000000000000000000bbb");
    private static readonly Address NonManager = new("0x0000000000000000000000000000000000000ccc");
    private static readonly ValueHash256 SomeCodeHash = Keccak.Compute("some-code").ValueHash256;

    [Test]
    public void Abi_WhenParsed_ContainsExpectedFunctionSignatures()
    {
        Dictionary<uint, ArbitrumFunctionDescription> allFunctions = PrecompileTestAbiHelpers.GetAllFunctionDescriptions(Solgen.ArbWasmCache.Abi);

        allFunctions.Keys.Should().BeEquivalentTo(new[]
        {
            PrecompileTestAbiHelpers.GetMethodId("isCacheManager(address)"),
            PrecompileTestAbiHelpers.GetMethodId("allCacheManagers()"),
            PrecompileTestAbiHelpers.GetMethodId("cacheCodehash(bytes32)"),
            PrecompileTestAbiHelpers.GetMethodId("cacheProgram(address)"),
            PrecompileTestAbiHelpers.GetMethodId("evictCodehash(bytes32)"),
            PrecompileTestAbiHelpers.GetMethodId("codehashIsCached(bytes32)"),
        });
    }

    [Test]
    public void Abi_WhenParsed_ContainsExpectedEvents()
    {
        Dictionary<string, AbiEventDescription> allEvents = PrecompileTestAbiHelpers.GetAllEventDescriptions(Solgen.ArbWasmCache.Abi);

        allEvents.Keys.Should().BeEquivalentTo("UpdateProgramCache");
    }

    [Test]
    public void Abi_WhenParsed_ContainsNoErrors()
    {
        PrecompileTestAbiHelpers.GetAllErrorDescriptions(Solgen.ArbWasmCache.Abi).Should().BeEmpty();
    }

    [Test]
    public void MethodIds_AllFunctions_MatchExpectedSelectors()
    {
        PrecompileTestAbiHelpers.GetMethodId("isCacheManager(address)").Should().Be(Solgen.ArbWasmCache.Methods.IsCacheManager);
        PrecompileTestAbiHelpers.GetMethodId("allCacheManagers()").Should().Be(Solgen.ArbWasmCache.Methods.AllCacheManagers);
        PrecompileTestAbiHelpers.GetMethodId("cacheCodehash(bytes32)").Should().Be(Solgen.ArbWasmCache.Methods.CacheCodehash);
        PrecompileTestAbiHelpers.GetMethodId("cacheProgram(address)").Should().Be(Solgen.ArbWasmCache.Methods.CacheProgram);
        PrecompileTestAbiHelpers.GetMethodId("evictCodehash(bytes32)").Should().Be(Solgen.ArbWasmCache.Methods.EvictCodehash);
        PrecompileTestAbiHelpers.GetMethodId("codehashIsCached(bytes32)").Should().Be(Solgen.ArbWasmCache.Methods.CodehashIsCached);
    }

    [Test]
    public void EventTopics_AllEvents_MatchExpectedHashes()
    {
        ArbWasmCache.UpdateProgramCache.GetHash().Should().Be(
            new Hash256(Solgen.ArbWasmCache.Events.UpdateProgramCache.Topic0Hex));
    }

    [Test]
    public void IsCacheManager_ForUnregisteredAddress_ReturnsFalse()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbWasmCache.IsCacheManager(context, ManagerA).Should().BeFalse();
    }

    [Test]
    public void IsCacheManager_AfterRegistration_ReturnsTrue()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        context.ArbosState.Programs.CacheManagersStorage.Add(ManagerA);

        ArbWasmCache.IsCacheManager(context, ManagerA).Should().BeTrue();
    }

    [Test]
    public void IsCacheManager_AfterRegistrationAndRemoval_ReturnsFalse()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        context.ArbosState.Programs.CacheManagersStorage.Add(ManagerA);
        context.ArbosState.Programs.CacheManagersStorage.Remove(ManagerA, ArbosVersion.Fifty);

        ArbWasmCache.IsCacheManager(context, ManagerA).Should().BeFalse();
    }

    [Test]
    public void AllCacheManagers_OnEmptyStorage_ReturnsEmptyArray()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbWasmCache.AllCacheManagers(context).Should().BeEmpty();
    }

    [Test]
    public void AllCacheManagers_AfterMultipleAdditions_ReturnsMembersInInsertionOrder()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        context.ArbosState.Programs.CacheManagersStorage.Add(ManagerA);
        context.ArbosState.Programs.CacheManagersStorage.Add(ManagerB);

        ArbWasmCache.AllCacheManagers(context).Should().Equal(ManagerA, ManagerB);
    }

    [Test]
    public void CodehashIsCached_ForUnknownCodeHash_ReturnsFalse()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbWasmCache.CodehashIsCached(context, SomeCodeHash).Should().BeFalse();
    }

    [Test]
    public void CodehashIsCached_ForCachedProgram_ReturnsTrue()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        SeedProgram(context, SomeCodeHash, cached: true);

        ArbWasmCache.CodehashIsCached(context, SomeCodeHash).Should().BeTrue();
    }

    [Test]
    public void CacheCodehash_WithoutAccess_ThrowsOutOfGas()
    {
        // HasAccess requires caller to be a cache manager or chain owner; neither holds for NonManager.
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        PrecompileTestContextBuilder callerContext = context.WithCaller(NonManager);

        Action act = () => ArbWasmCache.CacheCodehash(callerContext, SomeCodeHash);

        AssertOutOfGas(act, callerContext);
    }

    [Test]
    public void CacheProgram_WithoutAccess_ThrowsOutOfGas()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        PrecompileTestContextBuilder callerContext = context.WithCaller(NonManager);

        Action act = () => ArbWasmCache.CacheProgram(callerContext, ManagerA);

        AssertOutOfGas(act, callerContext);
    }

    [Test]
    public void EvictProgram_WithoutAccess_ThrowsOutOfGas()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        PrecompileTestContextBuilder callerContext = context.WithCaller(NonManager);

        Action act = () => ArbWasmCache.EvictProgram(callerContext, SomeCodeHash);

        AssertOutOfGas(act, callerContext);
    }

    [Test]
    public void CacheCodehash_ForInactiveProgramAsCacheManager_ThrowsProgramNeedsUpgrade()
    {
        // Caching an address whose program never activated hits the version mismatch check at
        // StylusPrograms.SetProgramCached — program.Version (0) != StylusVersion → ProgramNeedsUpgrade.
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        context.ArbosState.Programs.CacheManagersStorage.Add(ManagerA);
        PrecompileTestContextBuilder callerContext = context.WithCaller(ManagerA);
        ushort stylusVersion = (ushort)callerContext.ArbosState.Programs.GetParams().StylusVersion;

        Action act = () => ArbWasmCache.CacheCodehash(callerContext, SomeCodeHash);

        ArbitrumPrecompileException exception = act.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbWasm.ProgramNeedsUpgradeError(0, stylusVersion);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void EvictProgram_ForCachedProgram_TogglesCachedAndEmitsEvent()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        context.ArbosState.Programs.CacheManagersStorage.Add(ManagerA);
        SeedProgram(context, SomeCodeHash, cached: true);
        PrecompileTestContextBuilder callerContext = context.WithCaller(ManagerA);

        ArbWasmCache.EvictProgram(callerContext, SomeCodeHash);

        ReadSeededProgramCached(callerContext, SomeCodeHash).Should().BeFalse();
        callerContext.EventLogs.Should().ContainSingle()
            .Which.Topics[0].Should().Be(ArbWasmCache.UpdateProgramCache.GetHash());
    }

    [Test]
    public void EvictProgram_OnAlreadyEvictedProgramAsCacheManager_IsNoOp()
    {
        // An inactive program has Cached=false; evicting (Cached=false) is a no-op short-circuit in
        // StylusPrograms.SetProgramCached and must not emit UpdateProgramCache or mutate state.
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        context.ArbosState.Programs.CacheManagersStorage.Add(ManagerA);
        PrecompileTestContextBuilder callerContext = context.WithCaller(ManagerA);

        Action act = () => ArbWasmCache.EvictProgram(callerContext, SomeCodeHash);

        act.Should().NotThrow();
        callerContext.EventLogs.Should().BeEmpty();
        ArbWasmCache.CodehashIsCached(callerContext, SomeCodeHash).Should().BeFalse();
    }

    [Test]
    public void EvictProgram_AsChainOwnerCaller_GrantsAccessThroughChainOwnerPath()
    {
        // Chain owners bypass the CacheManagersStorage check via the second branch of HasAccess.
        // Paired with EvictProgram_WithoutAccess_ThrowsOutOfGas as a positive control: the only
        // material difference is ManagerA's membership in ChainOwners, so a success here proves the
        // chain-owner branch grants access.
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        context.ArbosState.ChainOwners.Add(ManagerA);
        PrecompileTestContextBuilder callerContext = context.WithCaller(ManagerA);

        Action act = () => ArbWasmCache.EvictProgram(callerContext, SomeCodeHash);

        act.Should().NotThrow();
    }

    private static void AssertOutOfGas(Action act, PrecompileTestContextBuilder context)
    {
        ArbitrumPrecompileException exception = act.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateOutOfGasException();
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
        context.GasLeft.Should().Be(0, "BurnOut must zero remaining gas before throwing");
    }

    // Seeds a program record into ProgramsStorage so SetProgramCached's state machine can exercise
    // the cache/evict transitions without requiring full Stylus activation. Layout mirrors the
    // private StylusPrograms.SetProgram serializer; activatedAtHours is pinned to the block
    // timestamp so the program is fresh (not expired) against the configured ExpiryDays.
    private static void SeedProgram(PrecompileTestContextBuilder context, ValueHash256 codeHash, bool cached)
    {
        StylusParams stylusParams = context.ArbosState.Programs.GetParams();
        uint activatedAtHours = ArbitrumTime.HoursSinceArbitrum(context.BlockExecutionContext.Header.Timestamp);
        Span<byte> data = stackalloc byte[32];
        BinaryPrimitives.WriteUInt16BigEndian(data, (ushort)stylusParams.StylusVersion);
        data[8] = (byte)(activatedAtHours >> 16);
        data[9] = (byte)(activatedAtHours >> 8);
        data[10] = (byte)activatedAtHours;
        data[14] = cached ? (byte)1 : (byte)0;
        context.ArbosState.Programs.ProgramsStorage.Set(codeHash, new ValueHash256(data));
    }

    private static bool ReadSeededProgramCached(PrecompileTestContextBuilder context, ValueHash256 codeHash)
    {
        ValueHash256 raw = context.ArbosState.Programs.ProgramsStorage.Get(codeHash);
        return raw.Bytes[14] != 0;
    }
}
