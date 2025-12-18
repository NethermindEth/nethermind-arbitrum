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
    private static readonly ILogManager Logger = LimboLogs.Instance;
    private const int WordSize = EvmPooledMemory.WordSize;

    private static readonly uint _addChainOwnerId = PrecompileHelper.GetMethodId("addChainOwner(address)");
    private static readonly uint _removeChainOwnerId = PrecompileHelper.GetMethodId("removeChainOwner(address)");
    private static readonly uint _isChainOwnerId = PrecompileHelper.GetMethodId("isChainOwner(address)");
    private static readonly uint _getAllChainOwnersId = PrecompileHelper.GetMethodId("getAllChainOwners()");
    private static readonly uint _setNativeTokenManagementFromId = PrecompileHelper.GetMethodId("setNativeTokenManagementFrom(uint64)");
    private static readonly uint _addNativeTokenOwnerId = PrecompileHelper.GetMethodId("addNativeTokenOwner(address)");
    private static readonly uint _removeNativeTokenOwnerId = PrecompileHelper.GetMethodId("removeNativeTokenOwner(address)");
    private static readonly uint _isNativeTokenOwnerId = PrecompileHelper.GetMethodId("isNativeTokenOwner(address)");
    private static readonly uint _getAllNativeTokenOwnersId = PrecompileHelper.GetMethodId("getAllNativeTokenOwners()");
    private static readonly uint _setL1BaseFeeEstimateInertiaId = PrecompileHelper.GetMethodId("setL1BaseFeeEstimateInertia(uint64)");
    private static readonly uint _setL2BaseFeeId = PrecompileHelper.GetMethodId("setL2BaseFee(uint256)");
    private static readonly uint _setMinimumL2BaseFeeId = PrecompileHelper.GetMethodId("setMinimumL2BaseFee(uint256)");
    private static readonly uint _setSpeedLimitId = PrecompileHelper.GetMethodId("setSpeedLimit(uint64)");
    private static readonly uint _setMaxTxGasLimitId = PrecompileHelper.GetMethodId("setMaxTxGasLimit(uint64)");
    private static readonly uint _setL2GasPricingInertiaId = PrecompileHelper.GetMethodId("setL2GasPricingInertia(uint64)");
    private static readonly uint _setL2GasBacklogToleranceId = PrecompileHelper.GetMethodId("setL2GasBacklogTolerance(uint64)");
    private static readonly uint _getNetworkFeeAccountId = PrecompileHelper.GetMethodId("getNetworkFeeAccount()");
    private static readonly uint _getInfraFeeAccountId = PrecompileHelper.GetMethodId("getInfraFeeAccount()");
    private static readonly uint _setNetworkFeeAccountId = PrecompileHelper.GetMethodId("setNetworkFeeAccount(address)");
    private static readonly uint _setInfraFeeAccountId = PrecompileHelper.GetMethodId("setInfraFeeAccount(address)");
    private static readonly uint _scheduleArbOSUpgradeId = PrecompileHelper.GetMethodId("scheduleArbOSUpgrade(uint64,uint64)");
    private static readonly uint _setL1PricingEquilibrationUnitsId = PrecompileHelper.GetMethodId("setL1PricingEquilibrationUnits(uint256)");
    private static readonly uint _setL1PricingInertiaId = PrecompileHelper.GetMethodId("setL1PricingInertia(uint64)");
    private static readonly uint _setL1PricingRewardRecipientId = PrecompileHelper.GetMethodId("setL1PricingRewardRecipient(address)");
    private static readonly uint _setL1PricingRewardRateId = PrecompileHelper.GetMethodId("setL1PricingRewardRate(uint64)");
    private static readonly uint _setL1PricePerUnitId = PrecompileHelper.GetMethodId("setL1PricePerUnit(uint256)");
    private static readonly uint _setPerBatchGasChargeId = PrecompileHelper.GetMethodId("setPerBatchGasCharge(int64)");
    private static readonly uint _setBrotliCompressionLevelId = PrecompileHelper.GetMethodId("setBrotliCompressionLevel(uint64)");
    private static readonly uint _setAmortizedCostCapBipsId = PrecompileHelper.GetMethodId("setAmortizedCostCapBips(uint64)");
    private static readonly uint _releaseL1PricerSurplusFundsId = PrecompileHelper.GetMethodId("releaseL1PricerSurplusFunds(uint256)");
    private static readonly uint _setInkPriceId = PrecompileHelper.GetMethodId("setInkPrice(uint32)");
    private static readonly uint _setWasmMaxStackDepthId = PrecompileHelper.GetMethodId("setWasmMaxStackDepth(uint32)");
    private static readonly uint _setWasmFreePagesId = PrecompileHelper.GetMethodId("setWasmFreePages(uint16)");
    private static readonly uint _setWasmPageGasId = PrecompileHelper.GetMethodId("setWasmPageGas(uint16)");
    private static readonly uint _setWasmPageLimitId = PrecompileHelper.GetMethodId("setWasmPageLimit(uint16)");
    private static readonly uint _setWasmMaxSizeId = PrecompileHelper.GetMethodId("setWasmMaxSize(uint32)");
    private static readonly uint _setWasmMinInitGasId = PrecompileHelper.GetMethodId("setWasmMinInitGas(uint8,uint16)");
    private static readonly uint _setWasmInitCostScalarId = PrecompileHelper.GetMethodId("setWasmInitCostScalar(uint64)");
    private static readonly uint _setWasmExpiryDaysId = PrecompileHelper.GetMethodId("setWasmExpiryDays(uint16)");
    private static readonly uint _setWasmKeepaliveDaysId = PrecompileHelper.GetMethodId("setWasmKeepaliveDays(uint16)");
    private static readonly uint _setWasmBlockCacheSizeId = PrecompileHelper.GetMethodId("setWasmBlockCacheSize(uint16)");
    private static readonly uint _addWasmCacheManagerId = PrecompileHelper.GetMethodId("addWasmCacheManager(address)");
    private static readonly uint _removeWasmCacheManagerId = PrecompileHelper.GetMethodId("removeWasmCacheManager(address)");
    private static readonly uint _setChainConfigId = PrecompileHelper.GetMethodId("setChainConfig(string)");
    private static readonly uint _setCalldataPriceIncreaseId = PrecompileHelper.GetMethodId("setCalldataPriceIncrease(bool)");
    private static readonly uint _setMaxBlockGasLimitId = PrecompileHelper.GetMethodId("setMaxBlockGasLimit(uint64)");
    private static readonly uint _setParentGasFloorPerTokenId = PrecompileHelper.GetMethodId("setParentGasFloorPerToken(uint64)");

    [Test]
    public void ParsesAddChainOwner_Always_AddsToState()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_addChainOwnerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_addChainOwnerId].AbiFunctionDescription;
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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_removeChainOwnerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_removeChainOwnerId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_removeChainOwnerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_removeChainOwnerId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_isChainOwnerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_isChainOwnerId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_isChainOwnerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_isChainOwnerId].AbiFunctionDescription;

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
        var preConfigurer = (ContainerBuilder cb) =>
        {
            cb.AddScoped(new ArbitrumTestBlockchainBase.Configuration()
            {
                SuggestGenesisOnStart = true, // for arbos state initialization
            });
        };
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(preConfigurer);

        using var dispose = chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head?.Header);

        PrecompileTestContextBuilder context = new(chain.WorldStateManager.GlobalWorldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        Address addr123 = new("0x0000000000000000000000000000000000000123");
        Address addr456 = new("0x0000000000000000000000000000000000000456");
        Address addr789 = new("0x0000000000000000000000000000000000000789");
        context.ArbosState.ChainOwners.Add(addr123);
        context.ArbosState.ChainOwners.Add(addr456);
        context.ArbosState.ChainOwners.Add(addr789);

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_getAllChainOwnersId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_getAllChainOwnersId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Enable the feature with some value to make sure the function indeed disables it
        context.ArbosState.NativeTokenEnabledTime.Set(100);

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setNativeTokenManagementFromId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setNativeTokenManagementFromId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesisBlock = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);

        ulong now = 100;
        genesisBlock.Header.Timestamp = now;
        context.WithArbosState().WithBlockExecutionContext(genesisBlock.Header);

        ulong sevenDaysFromNow = now + ArbOwner.NativeTokenEnableDelay;
        context.ArbosState.NativeTokenEnabledTime.Set(sevenDaysFromNow + 1); // greater than 7 days from now

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setNativeTokenManagementFromId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setNativeTokenManagementFromId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesisBlock = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);

        ulong now = 1;
        genesisBlock.Header.Timestamp = now;
        context.WithArbosState().WithBlockExecutionContext(genesisBlock.Header);

        context.ArbosState.NativeTokenEnabledTime.Set(3); // more than now but lower than 7 days from now

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setNativeTokenManagementFromId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setNativeTokenManagementFromId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesisBlock = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);

        ulong now = 0; // currently disabled
        genesisBlock.Header.Timestamp = now;
        context.WithArbosState().WithBlockExecutionContext(genesisBlock.Header);

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setNativeTokenManagementFromId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setNativeTokenManagementFromId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesisBlock = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);

        ulong now = 1;
        genesisBlock.Header.Timestamp = now;
        context.WithArbosState().WithBlockExecutionContext(genesisBlock.Header);

        context.ArbosState.NativeTokenEnabledTime.Set(now + 1); // scheduled to be enabled in the future

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_addNativeTokenOwnerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_addNativeTokenOwnerId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesisBlock = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);

        ulong now = 2;
        genesisBlock.Header.Timestamp = now;
        context.WithArbosState().WithBlockExecutionContext(genesisBlock.Header);

        context.ArbosState.NativeTokenEnabledTime.Set(now - 1); // already enabled

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_addNativeTokenOwnerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_addNativeTokenOwnerId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_removeNativeTokenOwnerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_removeNativeTokenOwnerId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_removeNativeTokenOwnerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_removeNativeTokenOwnerId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_isNativeTokenOwnerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_isNativeTokenOwnerId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_isNativeTokenOwnerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_isNativeTokenOwnerId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        Address addr111 = new("0x0000000000000000000000000000000000000111");
        Address addr222 = new("0x0000000000000000000000000000000000000222");
        Address addr333 = new("0x0000000000000000000000000000000000000333");
        context.ArbosState.NativeTokenOwners.Add(addr111);
        context.ArbosState.NativeTokenOwners.Add(addr222);
        context.ArbosState.NativeTokenOwners.Add(addr333);

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_getAllNativeTokenOwnersId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_getAllNativeTokenOwnersId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setL1BaseFeeEstimateInertiaId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setL1BaseFeeEstimateInertiaId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setL2BaseFeeId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setL2BaseFeeId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setMinimumL2BaseFeeId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setMinimumL2BaseFeeId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setSpeedLimitId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setSpeedLimitId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setSpeedLimitId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setSpeedLimitId].AbiFunctionDescription;

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
        var preConfigurer = (ContainerBuilder cb) =>
        {
            cb.AddScoped(new ArbitrumTestBlockchainBase.Configuration()
            {
                SuggestGenesisOnStart = true,
            });
        };
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(preConfigurer);

        using var dispose = chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head?.Header);
        PrecompileTestContextBuilder context = new(chain.WorldStateManager.GlobalWorldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        if (arbosVersion > ArbosVersion.One)
        {
            context.ArbosState.UpgradeArbosVersion(arbosVersion, false, chain.WorldStateManager.GlobalWorldState, chain.SpecProvider.GenesisSpec);
        }

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setMaxTxGasLimitId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setMaxTxGasLimitId].AbiFunctionDescription;

        UInt256 limit = 123;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            limit
        );

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();

        if (shouldSetBlockLimit)
        {
            context.ArbosState.L2PricingState.PerBlockGasLimitStorage.Get().Should().Be(limit.ToUInt64(null),
                $"Before ArbOS 50 (version {arbosVersion}), SetMaxTxGasLimit should set PerBlockGasLimit");
        }
        else
        {
            context.ArbosState.L2PricingState.PerTxGasLimitStorage.Get().Should().Be(limit.ToUInt64(null),
                $"At/After ArbOS 50 (version {arbosVersion}), SetMaxTxGasLimit should set PerTxGasLimit");
        }
    }

    [Test]
    public void ParsesSetMaxBlockGasLimit_Always_SetsPerBlockGasLimit()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setMaxTxGasLimitId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setMaxTxGasLimitId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setL2GasPricingInertiaId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setL2GasPricingInertiaId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setL2GasPricingInertiaId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setL2GasPricingInertiaId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setL2GasBacklogToleranceId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setL2GasBacklogToleranceId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_getNetworkFeeAccountId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_getNetworkFeeAccountId].AbiFunctionDescription;

        Address networkFeeAccount = new("0x0000000000000000000000000000000000000123");
        context.ArbosState.NetworkFeeAccount.Set(networkFeeAccount);

        byte[] result = implementation!(context, []);

        byte[] expectedResult = new byte[WordSize];
        networkFeeAccount.Bytes.CopyTo(expectedResult, WordSize - Address.Size);
        result.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public void ParsesGetInfraFeeAccount_Always_ReturnsInfraFeeAccount()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_getInfraFeeAccountId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_getInfraFeeAccountId].AbiFunctionDescription;

        Address infraFeeAccount = new("0x0000000000000000000000000000000000000123");
        context.ArbosState.InfraFeeAccount.Set(infraFeeAccount);

        byte[] result = implementation!(context, []);

        byte[] expectedResult = new byte[WordSize];
        infraFeeAccount.Bytes.CopyTo(expectedResult, WordSize - Address.Size);
        result.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public void ParsesSetNetworkFeeAccount_Always_SetsNetworkFeeAccount()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setNetworkFeeAccountId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setNetworkFeeAccountId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setInfraFeeAccountId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setInfraFeeAccountId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_scheduleArbOSUpgradeId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_scheduleArbOSUpgradeId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setL1PricingEquilibrationUnitsId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setL1PricingEquilibrationUnitsId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setL1PricingInertiaId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setL1PricingInertiaId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setL1PricingRewardRecipientId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setL1PricingRewardRecipientId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setL1PricingRewardRateId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setL1PricingRewardRateId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setL1PricePerUnitId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setL1PricePerUnitId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setPerBatchGasChargeId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setPerBatchGasChargeId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setAmortizedCostCapBipsId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setAmortizedCostCapBipsId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setBrotliCompressionLevelId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setBrotliCompressionLevelId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        UInt256 poolBalance = 123;
        context.WorldState.AddToBalanceAndCreateIfNotExists(ArbosAddresses.L1PricerFundsPoolAddress, poolBalance, London.Instance);
        UInt256 recognized = poolBalance + 1; // greater than pool balance
        context.ArbosState.L1PricingState.L1FeesAvailableStorage.Set(recognized);

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_releaseL1PricerSurplusFundsId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_releaseL1PricerSurplusFundsId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        UInt256 poolBalance = 100;
        context.WorldState.AddToBalanceAndCreateIfNotExists(ArbosAddresses.L1PricerFundsPoolAddress, poolBalance, London.Instance);
        UInt256 recognized = 40; // lower (or equal) than pool balance
        context.ArbosState.L1PricingState.L1FeesAvailableStorage.Set(recognized);

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_releaseL1PricerSurplusFundsId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_releaseL1PricerSurplusFundsId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setInkPriceId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setInkPriceId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setInkPriceId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setInkPriceId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setWasmMaxStackDepthId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setWasmMaxStackDepthId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setWasmFreePagesId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setWasmFreePagesId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setWasmPageGasId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setWasmPageGasId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setWasmPageLimitId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setWasmPageLimitId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setWasmMinInitGasId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setWasmMinInitGasId].AbiFunctionDescription;

        // ABI requires uint8 for gas argument
        byte gas = byte.MaxValue;
        // ABI requires uint16 for cached argument
        ushort cached = StylusParams.MinCachedGasUnits * 1 << 8; // greater than byte.MaxValue once divided by MinCachedGasUnits

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            [gas, cached]
        );

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

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setWasmMinInitGasId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setWasmMinInitGasId].AbiFunctionDescription;

        Action action = () => implementation!(context, calldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateRevertException("", true);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesSetWasmInitCostScalar_Always_SetsWasmInitCostScalar()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setWasmInitCostScalarId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setWasmInitCostScalarId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setWasmExpiryDaysId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setWasmExpiryDaysId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setWasmKeepaliveDaysId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setWasmKeepaliveDaysId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setWasmBlockCacheSizeId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setWasmBlockCacheSizeId].AbiFunctionDescription;

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
        var preConfigurer = (ContainerBuilder cb) =>
        {
            cb.AddScoped(new ArbitrumTestBlockchainBase.Configuration()
            {
                SuggestGenesisOnStart = true, // for arbos state initialization
            });
        };
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(preConfigurer);

        IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
        using var dispose = worldState.BeginScope(chain.BlockTree.Genesis);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Upgrade arbos version to 40 to include the wasm max size in storage (see StylusParams.Save())
        context.ArbosState.UpgradeArbosVersion(ArbosVersion.Forty, false, worldState, chain.SpecProvider.GenesisSpec);

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setWasmMaxSizeId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setWasmMaxSizeId].AbiFunctionDescription;

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
    public void ParsesAddWasmCacheManager_Always_AddsWasmCacheManager()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_addWasmCacheManagerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_addWasmCacheManagerId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_removeWasmCacheManagerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_removeWasmCacheManagerId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_removeWasmCacheManagerId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_removeWasmCacheManagerId].AbiFunctionDescription;

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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

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

        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setChainConfigId].AbiFunctionDescription;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            JsonSerializer.Serialize(newConfig)
        );

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setChainConfigId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(context, calldata);

        result.Should().BeEmpty();
        context.ArbosState.ChainConfigStorage.Get().Should().BeEquivalentTo(newSerializedConfig);
    }

    [Test]
    public void ParsesSetCalldataPriceIncrease_ToEnable_EnablesCalldataPriceIncrease()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

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

        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setCalldataPriceIncreaseId].AbiFunctionDescription;
        bool enabled = true;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            enabled
        );

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setCalldataPriceIncreaseId, out PrecompileHandler? implementation);
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
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

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

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setCalldataPriceIncreaseId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setCalldataPriceIncreaseId].AbiFunctionDescription;
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

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setMaxBlockGasLimitId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setMaxBlockGasLimitId].AbiFunctionDescription;

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

        bool exists = ArbOwnerParser.PrecompileImplementation.TryGetValue(_setParentGasFloorPerTokenId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();
        AbiFunctionDescription function = ArbOwnerParser.PrecompileFunctionDescription[_setParentGasFloorPerTokenId].AbiFunctionDescription;

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
}
