// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Logging;
using Nethermind.Core;
using Nethermind.Int256;
using Nethermind.Core.Extensions;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Evm;
using Autofac;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Specs.Forks;
using System.Diagnostics;
using Nethermind.Arbitrum.Data;
using System.Text.Json;
using System.Text;
using Nethermind.Abi;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using System.Numerics;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

public class ArbOwnerParserTests
{
    private const int WordSize = EvmPooledMemory.WordSize;

    private static readonly uint AddChainOwnerId = PrecompileTestAbiHelpers.GetMethodId("addChainOwner(address)");
    private static readonly uint RemoveChainOwnerId = PrecompileTestAbiHelpers.GetMethodId("removeChainOwner(address)");
    private static readonly uint IsChainOwnerId = PrecompileTestAbiHelpers.GetMethodId("isChainOwner(address)");
    private static readonly uint GetAllChainOwnersId = PrecompileTestAbiHelpers.GetMethodId("getAllChainOwners()");
    private static readonly uint SetNativeTokenManagementFromId = PrecompileTestAbiHelpers.GetMethodId("setNativeTokenManagementFrom(uint64)");
    private static readonly uint AddNativeTokenOwnerId = PrecompileTestAbiHelpers.GetMethodId("addNativeTokenOwner(address)");
    private static readonly uint RemoveNativeTokenOwnerId = PrecompileTestAbiHelpers.GetMethodId("removeNativeTokenOwner(address)");
    private static readonly uint IsNativeTokenOwnerId = PrecompileTestAbiHelpers.GetMethodId("isNativeTokenOwner(address)");
    private static readonly uint GetAllNativeTokenOwnersId = PrecompileTestAbiHelpers.GetMethodId("getAllNativeTokenOwners()");
    private static readonly uint SetL1BaseFeeEstimateInertiaId = PrecompileTestAbiHelpers.GetMethodId("setL1BaseFeeEstimateInertia(uint64)");
    private static readonly uint SetL2BaseFeeId = PrecompileTestAbiHelpers.GetMethodId("setL2BaseFee(uint256)");
    private static readonly uint SetMinimumL2BaseFeeId = PrecompileTestAbiHelpers.GetMethodId("setMinimumL2BaseFee(uint256)");
    private static readonly uint SetSpeedLimitId = PrecompileTestAbiHelpers.GetMethodId("setSpeedLimit(uint64)");
    private static readonly uint SetMaxTxGasLimitId = PrecompileTestAbiHelpers.GetMethodId("setMaxTxGasLimit(uint64)");
    private static readonly uint SetL2GasPricingInertiaId = PrecompileTestAbiHelpers.GetMethodId("setL2GasPricingInertia(uint64)");
    private static readonly uint SetL2GasBacklogToleranceId = PrecompileTestAbiHelpers.GetMethodId("setL2GasBacklogTolerance(uint64)");
    private static readonly uint GetNetworkFeeAccountId = PrecompileTestAbiHelpers.GetMethodId("getNetworkFeeAccount()");
    private static readonly uint GetInfraFeeAccountId = PrecompileTestAbiHelpers.GetMethodId("getInfraFeeAccount()");
    private static readonly uint SetNetworkFeeAccountId = PrecompileTestAbiHelpers.GetMethodId("setNetworkFeeAccount(address)");
    private static readonly uint SetInfraFeeAccountId = PrecompileTestAbiHelpers.GetMethodId("setInfraFeeAccount(address)");
    private static readonly uint ScheduleArbOSUpgradeId = PrecompileTestAbiHelpers.GetMethodId("scheduleArbOSUpgrade(uint64,uint64)");
    private static readonly uint SetL1PricingEquilibrationUnitsId = PrecompileTestAbiHelpers.GetMethodId("setL1PricingEquilibrationUnits(uint256)");
    private static readonly uint SetL1PricingInertiaId = PrecompileTestAbiHelpers.GetMethodId("setL1PricingInertia(uint64)");
    private static readonly uint SetL1PricingRewardRecipientId = PrecompileTestAbiHelpers.GetMethodId("setL1PricingRewardRecipient(address)");
    private static readonly uint SetL1PricingRewardRateId = PrecompileTestAbiHelpers.GetMethodId("setL1PricingRewardRate(uint64)");
    private static readonly uint SetL1PricePerUnitId = PrecompileTestAbiHelpers.GetMethodId("setL1PricePerUnit(uint256)");
    private static readonly uint SetPerBatchGasChargeId = PrecompileTestAbiHelpers.GetMethodId("setPerBatchGasCharge(int64)");
    private static readonly uint SetBrotliCompressionLevelId = PrecompileTestAbiHelpers.GetMethodId("setBrotliCompressionLevel(uint64)");
    private static readonly uint SetAmortizedCostCapBipsId = PrecompileTestAbiHelpers.GetMethodId("setAmortizedCostCapBips(uint64)");
    private static readonly uint ReleaseL1PricerSurplusFundsId = PrecompileTestAbiHelpers.GetMethodId("releaseL1PricerSurplusFunds(uint256)");
    private static readonly uint SetInkPriceId = PrecompileTestAbiHelpers.GetMethodId("setInkPrice(uint32)");
    private static readonly uint SetWasmMaxStackDepthId = PrecompileTestAbiHelpers.GetMethodId("setWasmMaxStackDepth(uint32)");
    private static readonly uint SetWasmFreePagesId = PrecompileTestAbiHelpers.GetMethodId("setWasmFreePages(uint16)");
    private static readonly uint SetWasmPageGasId = PrecompileTestAbiHelpers.GetMethodId("setWasmPageGas(uint16)");
    private static readonly uint SetWasmPageLimitId = PrecompileTestAbiHelpers.GetMethodId("setWasmPageLimit(uint16)");
    private static readonly uint SetWasmMaxSizeId = PrecompileTestAbiHelpers.GetMethodId("setWasmMaxSize(uint32)");
    private static readonly uint SetWasmMinInitGasId = PrecompileTestAbiHelpers.GetMethodId("setWasmMinInitGas(uint8,uint16)");
    private static readonly uint SetWasmInitCostScalarId = PrecompileTestAbiHelpers.GetMethodId("setWasmInitCostScalar(uint64)");
    private static readonly uint SetWasmExpiryDaysId = PrecompileTestAbiHelpers.GetMethodId("setWasmExpiryDays(uint16)");
    private static readonly uint SetWasmKeepaliveDaysId = PrecompileTestAbiHelpers.GetMethodId("setWasmKeepaliveDays(uint16)");
    private static readonly uint SetWasmBlockCacheSizeId = PrecompileTestAbiHelpers.GetMethodId("setWasmBlockCacheSize(uint16)");
    private static readonly uint AddWasmCacheManagerId = PrecompileTestAbiHelpers.GetMethodId("addWasmCacheManager(address)");
    private static readonly uint RemoveWasmCacheManagerId = PrecompileTestAbiHelpers.GetMethodId("removeWasmCacheManager(address)");
    private static readonly uint SetChainConfigId = PrecompileTestAbiHelpers.GetMethodId("setChainConfig(string)");
    private static readonly uint SetCalldataPriceIncreaseId = PrecompileTestAbiHelpers.GetMethodId("setCalldataPriceIncrease(bool)");
    private static readonly uint SetMaxBlockGasLimitId = PrecompileTestAbiHelpers.GetMethodId("setMaxBlockGasLimit(uint64)");
    private static readonly uint SetParentGasFloorPerTokenId = PrecompileTestAbiHelpers.GetMethodId("setParentGasFloorPerToken(uint64)");
    private static readonly uint SetMaxStylusContractFragmentsId = PrecompileTestAbiHelpers.GetMethodId("setMaxStylusContractFragments(uint8)");
    private static readonly uint SetCollectTipsId = PrecompileTestAbiHelpers.GetMethodId("setCollectTips(bool)");

    [Test]
    public void ParsesAddChainOwner_Always_AddsToState()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(AddChainOwnerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[AddChainOwnerId].AbiFunctionDescription;
        Address newOwner = new("0x0000000000000000000000000000000000000123");
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            newOwner
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.ChainOwners.IsMember(newOwner).Should().BeTrue();
    }

    [Test]
    public void ParsesRemoveChainOwner_IsNotOwner_ThrowsError()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(RemoveChainOwnerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[RemoveChainOwnerId].AbiFunctionDescription;

        Address owner = new("0x0000000000000000000000000000000000000123");
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            owner
        );

        Action action = () => implementation!(context, calldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException("Tried to remove non-owner");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesRemoveChainOwner_IsOwner_RemovesFromState()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(RemoveChainOwnerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[RemoveChainOwnerId].AbiFunctionDescription;

        Address owner = new("0x0000000000000000000000000000000000000123");
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            owner
        );

        context.ArbosState.ChainOwners.Add(owner);

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.ChainOwners.IsMember(owner).Should().BeFalse();
    }

    [Test]
    public void ParsesIsChainOwner_IsOwner_ReturnsTrue()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(IsChainOwnerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[IsChainOwnerId].AbiFunctionDescription;

        Address owner = new("0x0000000000000000000000000000000000000123");
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            owner
        );

        context.ArbosState.ChainOwners.Add(owner);

        byte[] result = implementation!(context, calldata);

        byte[] expectedResult = new byte[WordSize];
        expectedResult[WordSize - 1] = 1;
        result.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public void ParsesIsChainOwner_IsNotOwner_ReturnsFalse()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(IsChainOwnerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[IsChainOwnerId].AbiFunctionDescription;

        Address owner = new("0x0000000000000000000000000000000000000123");
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            owner
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEquivalentTo(new byte[WordSize]);
    }

    [Test]
    public void ParsesGetAllChainOwners_Always_ReturnsAllOwners()
    {
        Action<ContainerBuilder> preConfigurer = cb =>
        {
            cb.AddScoped(new ArbitrumTestBlockchainBase.Configuration()
            {
                SuggestGenesisOnStart = true, // for arbos state initialization
            });
        };
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(preConfigurer);

        using IDisposable dispose = chain.MainWorldState.BeginScope(chain.BlockTree.Head?.Header);

        PrecompileTestContextBuilder context = new(chain.MainWorldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        Address addr123 = new("0x0000000000000000000000000000000000000123");
        Address addr456 = new("0x0000000000000000000000000000000000000456");
        Address addr789 = new("0x0000000000000000000000000000000000000789");
        context.ArbosState.ChainOwners.Add(addr123);
        context.ArbosState.ChainOwners.Add(addr456);
        context.ArbosState.ChainOwners.Add(addr789);

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(GetAllChainOwnersId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[GetAllChainOwnersId].AbiFunctionDescription;

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature
        );

        byte[] result = implementation!(context, calldata);

        byte[] expectedResult = new byte[WordSize * 6];
        expectedResult[WordSize - 1] = WordSize; // offset to data section
        expectedResult[WordSize * 2 - 1] = 4; // length of actual data
        // Actual data
        chain.SpecHelper.InitialChainOwner.Bytes.PadLeft(WordSize).CopyTo(expectedResult, WordSize * 2);
        addr123.Bytes.PadLeft(WordSize).CopyTo(expectedResult, WordSize * 3);
        addr456.Bytes.PadLeft(WordSize).CopyTo(expectedResult, WordSize * 4);
        addr789.Bytes.PadLeft(WordSize).CopyTo(expectedResult, WordSize * 5);

        result.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public void ParsesSetNativeTokenManagementFrom_EnableTimeIsZero_DisablesFeature()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Enable the feature with some value to make sure the function indeed disables it
        context.ArbosState.NativeTokenEnabledTime.Set(100);

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetNativeTokenManagementFromId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetNativeTokenManagementFromId].AbiFunctionDescription;

        UInt256 newEnableTime = 0;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            newEnableTime
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.NativeTokenEnabledTime.Get().Should().Be(newEnableTime.ToUInt64(null));
    }

    [Test]
    public void ParsesSetNativeTokenManagementFrom_CurrentEnableTimeIsGreaterThan7DaysFromNowButNewOneIsNot_Throws()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesisBlock = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);

        ulong now = 100;
        genesisBlock.Header.Timestamp = now;
        context.WithArbosState().WithBlockExecutionContext(genesisBlock.Header);

        ulong sevenDaysFromNow = now + ArbOwner.NativeTokenEnableDelay;
        context.ArbosState.NativeTokenEnabledTime.Set(sevenDaysFromNow + 1); // greater than 7 days from now

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetNativeTokenManagementFromId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetNativeTokenManagementFromId].AbiFunctionDescription;

        UInt256 newEnableTime = 1; // less than 7 days in the future
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            newEnableTime
        );

        Action action = () => implementation!(context, calldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException("native token feature must be enabled at least 7 days in the future");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesSetNativeTokenManagementFrom_CurrentEnableTimeIsLowerThan7DaysFromNowAndNewOneIsEvenSooner_Throws()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesisBlock = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);

        ulong now = 1;
        genesisBlock.Header.Timestamp = now;
        context.WithArbosState().WithBlockExecutionContext(genesisBlock.Header);

        context.ArbosState.NativeTokenEnabledTime.Set(3); // more than now but lower than 7 days from now

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetNativeTokenManagementFromId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetNativeTokenManagementFromId].AbiFunctionDescription;

        UInt256 newEnableTime = 2; // less than current enabled time
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            newEnableTime
        );

        Action action = () => implementation!(context, calldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException("native token feature cannot be updated to a time earlier than the current time at which it is scheduled to be enabled");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesSetNativeTokenManagementFrom_CorrectNewEnableTimeComparedToCurrentOne_SetsNewEnableTime()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesisBlock = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);

        ulong now = 0; // currently disabled
        genesisBlock.Header.Timestamp = now;
        context.WithArbosState().WithBlockExecutionContext(genesisBlock.Header);

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetNativeTokenManagementFromId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetNativeTokenManagementFromId].AbiFunctionDescription;

        UInt256 newEnableTime = now + ArbOwner.NativeTokenEnableDelay; // >= 7 days from now
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            newEnableTime
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.NativeTokenEnabledTime.Get().Should().Be(newEnableTime.ToUInt64(null));
    }

    [Test]
    public void ParsesAddNativeTokenOwner_NativeTokenManagementCurrentlyDisabled_Throws()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesisBlock = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);

        ulong now = 1;
        genesisBlock.Header.Timestamp = now;
        context.WithArbosState().WithBlockExecutionContext(genesisBlock.Header);

        context.ArbosState.NativeTokenEnabledTime.Set(now + 1); // scheduled to be enabled in the future

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(AddNativeTokenOwnerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[AddNativeTokenOwnerId].AbiFunctionDescription;

        Address tokenOwnerToAdd = new("0x0000000000000000000000000000000000000123");
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            tokenOwnerToAdd
        );

        Action action = () => implementation!(context, calldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException("native token feature is not enabled yet");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesAddNativeTokenOwner_NativeTokenManagementIsEnabled_AddsNativeTokenOwner()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesisBlock = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);

        ulong now = 2;
        genesisBlock.Header.Timestamp = now;
        context.WithArbosState().WithBlockExecutionContext(genesisBlock.Header);

        context.ArbosState.NativeTokenEnabledTime.Set(now - 1); // already enabled

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(AddNativeTokenOwnerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[AddNativeTokenOwnerId].AbiFunctionDescription;

        Address tokenOwnerToAdd = new("0x0000000000000000000000000000000000000123");
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            tokenOwnerToAdd
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.NativeTokenOwners.IsMember(tokenOwnerToAdd).Should().BeTrue();
    }

    [Test]
    public void ParsesRemoveNativeTokenOwner_NotAnOwner_Throws()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(RemoveNativeTokenOwnerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[RemoveNativeTokenOwnerId].AbiFunctionDescription;

        Address tokenOwnerToRemove = new("0x0000000000000000000000000000000000000123");
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            tokenOwnerToRemove
        );

        Action action = () => implementation!(context, calldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException("Tried to remove non native token owner");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesRemoveNativeTokenOwner_IsAnOwner_RemovesNativeTokenOwner()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(RemoveNativeTokenOwnerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[RemoveNativeTokenOwnerId].AbiFunctionDescription;

        Address tokenOwnerToRemove = new("0x0000000000000000000000000000000000000123");
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            tokenOwnerToRemove
        );

        context.ArbosState.NativeTokenOwners.Add(tokenOwnerToRemove);

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.NativeTokenOwners.IsMember(tokenOwnerToRemove).Should().BeFalse();
    }

    [Test]
    public void ParsesIsNativeTokenOwner_IsAnOwner_ReturnsTrue()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(IsNativeTokenOwnerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[IsNativeTokenOwnerId].AbiFunctionDescription;

        Address tokenOwner = new("0x0000000000000000000000000000000000000123");
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            tokenOwner
        );

        context.ArbosState.NativeTokenOwners.Add(tokenOwner);

        byte[] result = implementation!(context, calldata);

        byte[] expectedResult = new byte[WordSize];
        expectedResult[WordSize - 1] = 1;
        result.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public void ParsesIsNativeTokenOwner_NotAnOwner_ReturnsFalse()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(IsNativeTokenOwnerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[IsNativeTokenOwnerId].AbiFunctionDescription;

        Address tokenOwner = new("0x0000000000000000000000000000000000000123");
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            tokenOwner
        );

        byte[] result = implementation!(context, calldata);

        byte[] expectedResult = new byte[WordSize];
        result.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public void ParsesGetAllNativeTokenOwners_Always_ReturnsAllOwners()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        Address addr111 = new("0x0000000000000000000000000000000000000111");
        Address addr222 = new("0x0000000000000000000000000000000000000222");
        Address addr333 = new("0x0000000000000000000000000000000000000333");
        context.ArbosState.NativeTokenOwners.Add(addr111);
        context.ArbosState.NativeTokenOwners.Add(addr222);
        context.ArbosState.NativeTokenOwners.Add(addr333);

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(GetAllNativeTokenOwnersId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[GetAllNativeTokenOwnersId].AbiFunctionDescription;

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature
        );

        byte[] result = implementation!(context, calldata);

        byte[] expectedResult = new byte[WordSize * 5];
        expectedResult[WordSize - 1] = WordSize; // offset to data section
        expectedResult[WordSize * 2 - 1] = 3; // length of actual data
        // Actual data
        // chain.SpecHelper.InitialChainOwner.Bytes.PadLeft(WordSize).CopyTo(expectedResult, WordSize * 2);
        addr111.Bytes.PadLeft(WordSize).CopyTo(expectedResult, WordSize * 2);
        addr222.Bytes.PadLeft(WordSize).CopyTo(expectedResult, WordSize * 3);
        addr333.Bytes.PadLeft(WordSize).CopyTo(expectedResult, WordSize * 4);

        result.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public void ParsesSetL1BaseFeeEstimateInertia_Always_SetsInertia()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetL1BaseFeeEstimateInertiaId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetL1BaseFeeEstimateInertiaId].AbiFunctionDescription;

        UInt256 inertia = 123;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            inertia
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.L1PricingState.InertiaStorage.Get().Should().Be(inertia.ToUInt64(null));
    }

    [Test]
    public void ParsesSetL2BaseFee_Always_SetsL2BaseFee()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetL2BaseFeeId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetL2BaseFeeId].AbiFunctionDescription;

        UInt256 l2BaseFee = 123;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            l2BaseFee
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.L2PricingState.BaseFeeWeiStorage.Get().Should().Be(l2BaseFee);
    }

    [Test]
    public void ParsesSetMinimumL2BaseFee_CallIsMutating_SetsMinimumL2BaseFee()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetMinimumL2BaseFeeId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetMinimumL2BaseFeeId].AbiFunctionDescription;

        UInt256 minBaseFee = 123;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            minBaseFee
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.L2PricingState.MinBaseFeeWeiStorage.Get().Should().Be(minBaseFee);
    }

    [Test]
    public void ParsesSetSpeedLimit_IsZero_Throws()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetSpeedLimitId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetSpeedLimitId].AbiFunctionDescription;

        UInt256 limit = 0;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            limit
        );

        Action action = () => implementation!(context, calldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException("speed limit must be nonzero");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesSetSpeedLimit_IsNonZero_SetsSpeedLimit()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetSpeedLimitId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetSpeedLimitId].AbiFunctionDescription;

        UInt256 limit = 123;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            limit
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.L2PricingState.SpeedLimitPerSecondStorage.Get().Should().Be(limit.ToUInt64(null));
    }

    [Test]
    [TestCase(49ul, true)]  // Before ArbOS 50: sets block limit
    [TestCase(50ul, false)] // At ArbOS 50: sets per-tx limit
    public void ParsesSetMaxTxGasLimit_ArbOS50Transition_SetsCorrectLimit(ulong arbosVersion, bool shouldSetBlockLimit)
    {
        Action<ContainerBuilder> preConfigurer = cb =>
        {
            cb.AddScoped(new ArbitrumTestBlockchainBase.Configuration()
            {
                SuggestGenesisOnStart = true,
            });
        };
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(preConfigurer);

        using IDisposable dispose = chain.MainWorldState.BeginScope(chain.BlockTree.Head?.Header);
        PrecompileTestContextBuilder context = new(chain.MainWorldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        if (arbosVersion > ArbosVersion.One)
            context.ArbosState.UpgradeArbosVersion(arbosVersion, false, chain.MainWorldState, chain.SpecProvider.GenesisSpec);

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetMaxTxGasLimitId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetMaxTxGasLimitId].AbiFunctionDescription;

        UInt256 limit = 123;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            limit
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();

        if (shouldSetBlockLimit)
            context.ArbosState.L2PricingState.PerBlockGasLimitStorage.Get().Should().Be(limit.ToUInt64(null),
                $"Before ArbOS 50 (version {arbosVersion}), SetMaxTxGasLimit should set PerBlockGasLimit");
        else
            context.ArbosState.L2PricingState.PerTxGasLimitStorage.Get().Should().Be(limit.ToUInt64(null),
                $"At/After ArbOS 50 (version {arbosVersion}), SetMaxTxGasLimit should set PerTxGasLimit");
    }

    [Test]
    public void ParsesSetMaxBlockGasLimit_Always_SetsPerBlockGasLimit()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetMaxTxGasLimitId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetMaxTxGasLimitId].AbiFunctionDescription;

        UInt256 limit = 123;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            limit
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.L2PricingState.PerBlockGasLimitStorage.Get().Should().Be(limit.ToUInt64(null),
            "SetMaxBlockGasLimit should always set PerBlockGasLimit regardless of ArbOS version");
    }

    [Test]
    public void ParsesSetL2GasPricingInertia_IsZero_Throws()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetL2GasPricingInertiaId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetL2GasPricingInertiaId].AbiFunctionDescription;

        UInt256 inertia = 0;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            inertia
        );

        Action action = () => implementation!(context, calldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException("price inertia must be nonzero");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesSetL2GasPricingInertia_IsNonZero_SetsL2GasPricingInertia()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetL2GasPricingInertiaId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetL2GasPricingInertiaId].AbiFunctionDescription;

        UInt256 inertia = 123;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            inertia
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.L2PricingState.PricingInertiaStorage.Get().Should().Be(inertia.ToUInt64(null));
    }

    [Test]
    public void ParsesSetL2GasBacklogTolerance_Always_SetsL2GasBacklogTolerance()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetL2GasBacklogToleranceId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetL2GasBacklogToleranceId].AbiFunctionDescription;

        UInt256 backlogTolerance = 123;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            backlogTolerance
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.L2PricingState.BacklogToleranceStorage.Get().Should().Be(backlogTolerance.ToUInt64(null));
    }

    [Test]
    public void ParsesGetNetworkFeeAccount_Always_ReturnsNetworkFeeAccount()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(GetNetworkFeeAccountId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[GetNetworkFeeAccountId].AbiFunctionDescription;

        Address networkFeeAccount = new("0x0000000000000000000000000000000000000123");
        context.ArbosState.NetworkFeeAccount.Set(networkFeeAccount);

        byte[] result = implementation!(context, []);

        byte[] expectedResult = new byte[WordSize];
        networkFeeAccount.Bytes.CopyTo(expectedResult.AsSpan(WordSize - Address.Size));
        result.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public void ParsesGetInfraFeeAccount_Always_ReturnsInfraFeeAccount()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(GetInfraFeeAccountId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[GetInfraFeeAccountId].AbiFunctionDescription;

        Address infraFeeAccount = new("0x0000000000000000000000000000000000000123");
        context.ArbosState.InfraFeeAccount.Set(infraFeeAccount);

        byte[] result = implementation!(context, []);

        byte[] expectedResult = new byte[WordSize];
        infraFeeAccount.Bytes.CopyTo(expectedResult.AsSpan(WordSize - Address.Size));
        result.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public void ParsesSetNetworkFeeAccount_Always_SetsNetworkFeeAccount()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetNetworkFeeAccountId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetNetworkFeeAccountId].AbiFunctionDescription;

        Address networkFeeAccount = new("0x0000000000000000000000000000000000000456");
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            networkFeeAccount
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.NetworkFeeAccount.Get().Should().Be(networkFeeAccount);
    }

    [Test]
    public void ParsesSetInfraFeeAccount_Always_SetsInfraFeeAccount()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetInfraFeeAccountId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetInfraFeeAccountId].AbiFunctionDescription;

        Address infraFeeAccount = new("0x0000000000000000000000000000000000000456");
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            infraFeeAccount
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.InfraFeeAccount.Get().Should().Be(infraFeeAccount);
    }

    [Test]
    public void ParsesScheduleArbOSUpgrade_Always_SetsArbosUpgrade()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(ScheduleArbOSUpgradeId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[ScheduleArbOSUpgradeId].AbiFunctionDescription;

        UInt256 version = 123;
        UInt256 timestamp = 456;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            version,
            timestamp
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.UpgradeVersion.Get().Should().Be(version.ToUInt64(null));
        context.ArbosState.UpgradeTimestamp.Get().Should().Be(timestamp.ToUInt64(null));
    }

    [Test]
    public void ParsesSetL1PricingEquilibrationUnits_Always_SetsL1PricingEquilibrationUnits()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetL1PricingEquilibrationUnitsId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetL1PricingEquilibrationUnitsId].AbiFunctionDescription;

        UInt256 units = 123;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            units
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.L1PricingState.EquilibrationUnitsStorage.Get().Should().Be(units.ToUInt64(null));
    }

    [Test]
    public void ParsesSetL1PricingInertia_Always_SetsL1PricingInertia()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetL1PricingInertiaId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetL1PricingInertiaId].AbiFunctionDescription;

        UInt256 inertia = 123;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            inertia
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.L1PricingState.InertiaStorage.Get().Should().Be(inertia.ToUInt64(null));
    }

    [Test]
    public void ParsesSetL1PricingRewardRecipient_Always_SetsL1PricingRewardRecipient()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetL1PricingRewardRecipientId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetL1PricingRewardRecipientId].AbiFunctionDescription;

        Address recipient = new("0x0000000000000000000000000000000000000123");
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            recipient
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.L1PricingState.PayRewardsToStorage.Get().Should().Be(recipient);
    }

    [Test]
    public void ParsesSetL1PricingRewardRate_Always_SetsL1PricingRewardRate()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetL1PricingRewardRateId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetL1PricingRewardRateId].AbiFunctionDescription;

        UInt256 weiPerUnit = 123;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            weiPerUnit
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.L1PricingState.PerUnitRewardStorage.Get().Should().Be(weiPerUnit.ToUInt64(null));
    }

    [Test]
    public void ParsesSetL1PricePerUnit_Always_SetsL1PricePerUnit()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetL1PricePerUnitId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetL1PricePerUnitId].AbiFunctionDescription;

        UInt256 pricePerUnit = 123;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            pricePerUnit
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.L1PricingState.PricePerUnitStorage.Get().Should().Be(pricePerUnit.ToUInt64(null));
    }

    [Test]
    public void ParsesSetPerBatchGasCharge_Always_SetsPerBatchGasCharge()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetPerBatchGasChargeId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetPerBatchGasChargeId].AbiFunctionDescription;

        BigInteger baseCharge = 123;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            baseCharge
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.L1PricingState.PerBatchGasCostStorage.Get().Should().Be((ulong)baseCharge);
    }

    [Test]
    public void ParsesSetAmortizedCostCapBips_Always_SetsAmortizedCostCapBips()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetAmortizedCostCapBipsId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetAmortizedCostCapBipsId].AbiFunctionDescription;

        UInt256 cap = 123;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            cap
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.L1PricingState.AmortizedCostCapBipsStorage.Get().Should().Be(cap.ToUInt64(null));
    }

    [Test]
    public void ParsesSetBrotliCompressionLevel_Always_SetsBrotliCompressionLevel()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetBrotliCompressionLevelId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetBrotliCompressionLevelId].AbiFunctionDescription;

        UInt256 level = 10;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            level
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.BrotliCompressionLevel.Get().Should().Be(level.ToUInt64(null));
    }

    [Test]
    public void ParsesReleaseL1PricerSurplusFunds_RecognizedFundsGreaterThanPoolBalance_ReturnsZero()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        UInt256 poolBalance = 123;
        context.WorldState.AddToBalanceAndCreateIfNotExists(ArbosAddresses.L1PricerFundsPoolAddress, poolBalance, London.Instance);
        UInt256 recognized = poolBalance + 1; // greater than pool balance
        context.ArbosState.L1PricingState.L1FeesAvailableStorage.Set(recognized);

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(ReleaseL1PricerSurplusFundsId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[ReleaseL1PricerSurplusFundsId].AbiFunctionDescription;

        UInt256 maxWeiToRelease = 111; // does not matter for that test case
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            maxWeiToRelease
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEquivalentTo(new byte[WordSize]);
        context.ArbosState.L1PricingState.L1FeesAvailableStorage.Get().Should().Be(recognized.ToUInt64(null));
    }

    [Test]
    public void ParsesReleaseL1PricerSurplusFunds_RecognizedFundsLowerThanPoolBalance_ReturnsWeiToTransfer()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        UInt256 poolBalance = 100;
        context.WorldState.AddToBalanceAndCreateIfNotExists(ArbosAddresses.L1PricerFundsPoolAddress, poolBalance, London.Instance);
        UInt256 recognized = 40; // lower (or equal) than pool balance
        context.ArbosState.L1PricingState.L1FeesAvailableStorage.Set(recognized);

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(ReleaseL1PricerSurplusFundsId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[ReleaseL1PricerSurplusFundsId].AbiFunctionDescription;

        UInt256 maxWeiToRelease = 50; // lower than poolBalance - recognized
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            maxWeiToRelease
        );

        byte[] result = implementation!(context, calldata);

        byte[] expectedResult = new byte[WordSize];
        expectedResult[WordSize - 1] = (byte)maxWeiToRelease;
        result.Should().BeEquivalentTo(expectedResult);
        context.ArbosState.L1PricingState.L1FeesAvailableStorage.Get().Should().Be((recognized + maxWeiToRelease).ToUInt64(null));
    }

    [Test]
    public void ParsesSetInkPrice_PriceGreaterThanUint24_Throws()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetInkPriceId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetInkPriceId].AbiFunctionDescription;

        UInt256 inkPrice = 1 << 24; // bigger than 24 bits (uint24)
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            inkPrice
        );

        Action action = () => implementation!(context, calldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException("ink price must be a positive uint24");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesSetInkPrice_PriceFitsWithinUint24_SetsInkPrice()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetInkPriceId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetInkPriceId].AbiFunctionDescription;

        UInt256 inkPrice = 123;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            inkPrice
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.Programs.GetParams().InkPrice.Should().Be(inkPrice.ToUInt32(null));
    }

    [Test]
    public void ParsesSetWasmMaxStackDepth_Always_SetsWasmMaxStackDepth()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetWasmMaxStackDepthId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetWasmMaxStackDepthId].AbiFunctionDescription;

        UInt256 maxStackDepth = 123;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            maxStackDepth
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.Programs.GetParams().MaxStackDepth.Should().Be(maxStackDepth.ToUInt32(null));
    }

    [Test]
    public void ParsesSetWasmFreePages_Always_SetsWasmFreePages()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetWasmFreePagesId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetWasmFreePagesId].AbiFunctionDescription;

        UInt256 freePages = 123;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            freePages
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.Programs.GetParams().FreePages.Should().Be(freePages.ToUInt16(null));
    }

    [Test]
    public void ParsesSetWasmPageGas_Always_SetsWasmPageGas()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetWasmPageGasId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetWasmPageGasId].AbiFunctionDescription;

        UInt256 pageGas = 123;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            pageGas
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.Programs.GetParams().PageGas.Should().Be(pageGas.ToUInt16(null));
    }

    [Test]
    public void ParsesSetWasmPageLimit_Always_SetsWasmPageLimit()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetWasmPageLimitId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetWasmPageLimitId].AbiFunctionDescription;

        UInt256 pageLimit = 123;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            pageLimit
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.Programs.GetParams().PageLimit.Should().Be(pageLimit.ToUInt16(null));
    }

    [Test]
    public void ParsesSetWasmMinInitGas_ArgumentsAreWithinRange_SetsWasmMinInitGas()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetWasmMinInitGasId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetWasmMinInitGasId].AbiFunctionDescription;

        // ABI requires uint8 for gas argument
        byte gas = byte.MaxValue;
        // ABI requires uint16 for cached argument
        ushort cached = StylusParams.MinCachedGasUnits * 1 << 8; // greater than byte.MaxValue once divided by MinCachedGasUnits

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature, gas, cached);

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.Programs.GetParams().MinInitGas.Should().Be(2); // ceiling div
        context.ArbosState.Programs.GetParams().MinCachedInitGas.Should().Be(byte.MaxValue); // got saturated
    }

    [Test]
    public void ParsesSetWasmMinInitGas_ArgumentsOverflow_ThrowsRevertException()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // ABI expects a uint8 for gas argument ! Will create overflow exception
        UInt256 gas = byte.MaxValue + 1;
        UInt256 cached = 0; // whatever here, will fail before anyway

        byte[] calldata = Bytes.FromHexString(
            $"{gas.ToBigEndian().ToHexString(withZeroX: false)}{cached.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetWasmMinInitGasId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetWasmMinInitGasId].AbiFunctionDescription;

        Action action = () => implementation!(context, calldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateRevertException("", true);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesSetWasmInitCostScalar_Always_SetsWasmInitCostScalar()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetWasmInitCostScalarId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetWasmInitCostScalarId].AbiFunctionDescription;

        UInt256 percent = StylusParams.CostScalarPercent; // ceiling div gives 1
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            percent
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.Programs.GetParams().InitCostScalar.Should().Be(1);
    }

    [Test]
    public void ParsesSetWasmExpiryDays_Always_SetsWasmExpiryDays()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetWasmExpiryDaysId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetWasmExpiryDaysId].AbiFunctionDescription;

        UInt256 expiryDays = 123;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            expiryDays
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.Programs.GetParams().ExpiryDays.Should().Be(expiryDays.ToUInt16(null));
    }

    [Test]
    public void ParsesSetWasmKeepaliveDays_Always_SetsWasmKeepaliveDays()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetWasmKeepaliveDaysId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetWasmKeepaliveDaysId].AbiFunctionDescription;

        UInt256 keepaliveDays = 123;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            keepaliveDays
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.Programs.GetParams().KeepaliveDays.Should().Be(keepaliveDays.ToUInt16(null));
    }

    [Test]
    public void ParsesSetWasmBlockCacheSize_Always_SetsWasmBlockCacheSize()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetWasmBlockCacheSizeId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetWasmBlockCacheSizeId].AbiFunctionDescription;

        UInt256 blockCacheSize = 123;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            blockCacheSize
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.Programs.GetParams().BlockCacheSize.Should().Be(blockCacheSize.ToUInt16(null));
    }

    [Test]
    public void ParsesSetWasmMaxSize_Always_SetsWasmMaxSize()
    {
        Action<ContainerBuilder> preConfigurer = cb =>
        {
            cb.AddScoped(new ArbitrumTestBlockchainBase.Configuration()
            {
                SuggestGenesisOnStart = true, // for arbos state initialization
            });
        };
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(preConfigurer);

        IWorldState worldState = chain.MainWorldState;
        using IDisposable dispose = worldState.BeginScope(chain.BlockTree.Genesis);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Upgrade arbos version to 40 to include the wasm max size in storage (see StylusParams.Save())
        context.ArbosState.UpgradeArbosVersion(ArbosVersion.Forty, false, worldState, chain.SpecProvider.GenesisSpec);

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetWasmMaxSizeId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetWasmMaxSizeId].AbiFunctionDescription;

        UInt256 maxWasmSize = 123;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            maxWasmSize
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.Programs.GetParams().MaxWasmSize.Should().Be(maxWasmSize.ToUInt32(null));
    }

    [Test]
    public void ParsesSetMaxStylusContractFragments_AtSixty_SetsMaxFragmentCount()
    {
        Action<ContainerBuilder> preConfigurer = cb =>
        {
            cb.AddScoped(new ArbitrumTestBlockchainBase.Configuration()
            {
                SuggestGenesisOnStart = true,
            });
        };
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(preConfigurer);

        IWorldState worldState = chain.MainWorldState;
        using IDisposable dispose = worldState.BeginScope(chain.BlockTree.Genesis);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Upgrade arbos version to 60 so MaxFragmentCount byte is part of slot 0 (see StylusParams.Save())
        context.ArbosState.UpgradeArbosVersion(ArbosVersion.Sixty, false, worldState, chain.SpecProvider.GenesisSpec);

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetMaxStylusContractFragmentsId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetMaxStylusContractFragmentsId].AbiFunctionDescription;

        byte maxFragments = 11;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            maxFragments
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.Programs.GetParams().MaxFragmentCount.Should().Be(maxFragments);
    }

    [Test]
    public void ParsesAddWasmCacheManager_Always_AddsWasmCacheManager()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(AddWasmCacheManagerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[AddWasmCacheManagerId].AbiFunctionDescription;

        Address manager = new("0x0000000000000000000000000000000000000123");
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            manager
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.Programs.CacheManagersStorage.IsMember(manager).Should().BeTrue();
    }

    [Test]
    public void ParsesRemoveWasmCacheManager_IsNotManager_Throws()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(RemoveWasmCacheManagerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[RemoveWasmCacheManagerId].AbiFunctionDescription;

        Address manager = new("0x0000000000000000000000000000000000000123");
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            manager
        );

        Action action = () => implementation!(context, calldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException("Tried to remove non-manager");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesRemoveWasmCacheManager_IsManager_RemovesWasmCacheManager()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(RemoveWasmCacheManagerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[RemoveWasmCacheManagerId].AbiFunctionDescription;

        Address manager = new("0x0000000000000000000000000000000000000123");
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            manager
        );

        context.ArbosState.Programs.CacheManagersStorage.Add(manager);
        Debug.Assert(context.ArbosState.Programs.CacheManagersStorage.IsMember(manager));

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.Programs.CacheManagersStorage.IsMember(manager).Should().BeFalse();
    }

    [Test]
    public void ParsesSetChainConfig_CallIsNonMutating_ReplacesChainConfig()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        ChainConfig currentConfig = JsonSerializer.Deserialize<ChainConfig>(
            context.ArbosState.ChainConfigStorage.Get()
        ) ?? throw new InvalidOperationException("Failed to deserialize current chain config");

        ChainConfig newConfig = currentConfig;
        long oldEip158Block = (long)newConfig.Eip158Block!;
        newConfig.Eip158Block = oldEip158Block + 3;

        byte[] newSerializedConfig = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(newConfig));

        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetChainConfigId].AbiFunctionDescription;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            JsonSerializer.Serialize(newConfig)
        );

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetChainConfigId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.ChainConfigStorage.Get().Should().BeEquivalentTo(newSerializedConfig);
    }

    [Test]
    public void ParsesSetCalldataPriceIncrease_ToEnable_EnablesCalldataPriceIncrease()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Sets some initial random features
        Span<byte> bytes = stackalloc byte[32];
        Random rng = new();
        rng.NextBytes(bytes);
        bytes[31] &= 0xFE; // Ensure even number (IncreasedCalldataFeature corresponds to bit 0)
        UInt256 features = new(bytes, isBigEndian: true);

        context.ArbosState.Features.FeaturesStorage.Set(features);
        Debug.Assert(!context.ArbosState.Features.IsCalldataPriceIncreaseEnabled());

        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetCalldataPriceIncreaseId].AbiFunctionDescription;
        bool enabled = true;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            enabled
        );

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetCalldataPriceIncreaseId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.Features.IsCalldataPriceIncreaseEnabled().Should().Be(true);
        context.ArbosState.Features.FeaturesStorage.Get().Should().Be(features + 1);
    }

    [Test]
    public void ParsesSetCalldataPriceIncrease_ToDisable_DisablesCalldataPriceIncrease()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Sets some initial random features
        Span<byte> bytes = stackalloc byte[32];
        Random rng = new();
        rng.NextBytes(bytes);
        bytes[31] |= 0x1; // Ensures odd number (IncreasedCalldataFeature corresponds to bit 0)
        UInt256 features = new(bytes, isBigEndian: true);

        context.ArbosState.Features.FeaturesStorage.Set(features);
        Debug.Assert(context.ArbosState.Features.IsCalldataPriceIncreaseEnabled());

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetCalldataPriceIncreaseId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetCalldataPriceIncreaseId].AbiFunctionDescription;
        bool enabled = false;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            enabled
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.Features.IsCalldataPriceIncreaseEnabled().Should().Be(false);
        context.ArbosState.Features.FeaturesStorage.Get().Should().Be(features - 1);
    }

    [Test]
    public void ParsesSetMaxBlockGasLimit_Always_SetsMaxBlockGasLimit()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetMaxBlockGasLimitId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetMaxBlockGasLimitId].AbiFunctionDescription;

        UInt256 limit = 456;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            limit
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.L2PricingState.PerBlockGasLimitStorage.Get().Should().Be(limit.ToUInt64(null));
    }

    [Test]
    public void ParsesSetParentGasFloorPerToken_Always_SetsParentGasFloorPerToken()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetParentGasFloorPerTokenId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetParentGasFloorPerTokenId].AbiFunctionDescription;

        UInt256 floorPerToken = 789;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            floorPerToken
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.L1PricingState.GasFloorPerTokenStorage.Get().Should().Be(floorPerToken.ToUInt64(null));
    }

    [Test]
    public void SetGasPricingConstraints_MethodId_MatchesExpectedSelector()
    {
        uint actualSelector = PrecompileTestAbiHelpers.GetMethodId("setGasPricingConstraints(uint64[3][])");

        actualSelector.Should().Be(0xcc0d556a, "Method ID for setGasPricingConstraints(uint64[3][]) must match the selector");
    }

    [Test]
    public void SetGasPricingConstraints_SingleConstraint_StoresCorrectly()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        uint methodId = PrecompileTestAbiHelpers.GetMethodId("setGasPricingConstraints(uint64[3][])");
        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(methodId, out PrecompileHandler? implementation);
        exists.Should().BeTrue("setGasPricingConstraints should be registered");

        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[methodId].AbiFunctionDescription;

        ulong[][] constraints = [[1UL, 2UL, 3UL]];
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            (object)constraints
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.L2PricingState.ConstraintsLength().Should().Be(1);

        GasConstraint constraint = context.ArbosState.L2PricingState.OpenConstraintAt(0);
        constraint.Target.Should().Be(1UL);
        constraint.AdjustmentWindow.Should().Be(2UL);
        constraint.Backlog.Should().Be(3UL);
    }

    [Test]
    public void SetGasPricingConstraints_MultipleConstraints_StoresAllCorrectly()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        uint methodId = PrecompileTestAbiHelpers.GetMethodId("setGasPricingConstraints(uint64[3][])");
        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(methodId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[methodId].AbiFunctionDescription;

        const ulong n = 10;
        ulong[][] constraints = new ulong[n][];
        for (ulong i = 0; i < n; i++)
            constraints[i] = [100 * i + 1, 100 * i + 2, 100 * i + 3];

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            (object)constraints
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.L2PricingState.ConstraintsLength().Should().Be(n);

        for (ulong i = 0; i < n; i++)
        {
            GasConstraint constraint = context.ArbosState.L2PricingState.OpenConstraintAt(i);
            constraint.Target.Should().Be(100 * i + 1);
            constraint.AdjustmentWindow.Should().Be(100 * i + 2);
            constraint.Backlog.Should().Be(100 * i + 3);
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ParsesSetCollectTips_AtSixty_StoresFlag(bool collectTips)
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosVersion(ArbosVersion.Sixty);

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(SetCollectTipsId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[SetCollectTipsId].AbiFunctionDescription;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            collectTips
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.CollectTips().Should().Be(collectTips);
    }
}
