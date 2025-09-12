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
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.State;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

public class ArbOwnerParserTests
{
    private static readonly ILogManager Logger = LimboLogs.Instance;
    private const int WordSize = EvmPooledMemory.WordSize;

    [Test]
    public void ParsesAddChainOwner_Always_AddsToState()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string addChainOwnerMethodId = "0x481f8dbf";
        Address newOwner = new("0x0000000000000000000000000000000000000123");
        byte[] inputData = Bytes.FromHexString(
            $"{addChainOwnerMethodId}{newOwner.ToString(withZeroX: false, false).PadLeft(64, '0')}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.ChainOwners.IsMember(newOwner).Should().BeTrue();
    }

    [Test]
    public void ParsesRemoveChainOwner_IsNotOwner_ThrowsError()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string removeChainOwnerMethodId = "0x8792701a";
        Address owner = new("0x0000000000000000000000000000000000000123");
        byte[] inputData = Bytes.FromHexString(
            $"{removeChainOwnerMethodId}{owner.ToString(withZeroX: false, false).PadLeft(64, '0')}"
        );

        ArbOwnerParser arbOwnerParser = new();
        Action action = () => arbOwnerParser.RunAdvanced(context, inputData);

        action.Should().Throw<InvalidOperationException>().WithMessage("Tried to remove non-owner");
    }

    [Test]
    public void ParsesRemoveChainOwner_IsOwner_RemovesFromState()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();


        // Setup input data
        string removeChainOwnerMethodId = "0x8792701a";
        Address owner = new("0x0000000000000000000000000000000000000123");
        byte[] inputData = Bytes.FromHexString(
            $"{removeChainOwnerMethodId}{owner.ToString(withZeroX: false, false).PadLeft(64, '0')}"
        );

        context.ArbosState.ChainOwners.Add(owner);

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.ChainOwners.IsMember(owner).Should().BeFalse();
    }

    [Test]
    public void ParsesIsChainOwner_IsOwner_ReturnsTrue()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();


        // Setup input data
        string isChainOwnerMethodId = "0x26ef7f68";
        Address owner = new("0x0000000000000000000000000000000000000123");
        byte[] inputData = Bytes.FromHexString(
            $"{isChainOwnerMethodId}{owner.ToString(withZeroX: false, false).PadLeft(64, '0')}"
        );

        context.ArbosState.ChainOwners.Add(owner);

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        byte[] expectedResult = new byte[WordSize];
        expectedResult[WordSize - 1] = 1;
        result.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public void ParsesIsChainOwner_IsNotOwner_ReturnsFalse()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();


        // Setup input data
        string isChainOwnerMethodId = "0x26ef7f68";
        Address owner = new("0x0000000000000000000000000000000000000123");
        byte[] inputData = Bytes.FromHexString(
            $"{isChainOwnerMethodId}{owner.ToString(withZeroX: false, false).PadLeft(64, '0')}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

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

        // Setup input data
        string getAllChainOwnersMethodId = "0x516b4e0f";
        byte[] inputData = Bytes.FromHexString($"{getAllChainOwnersMethodId}");

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

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
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Enable the feature with some value to make sure the function indeed disables it
        context.ArbosState.NativeTokenEnabledTime.Set(100);

        // Setup input data
        string setNativeTokenManagementFromMethodId = "0xbdb8f707";
        UInt256 newEnableTime = 0;
        byte[] inputData = Bytes.FromHexString(
            $"{setNativeTokenManagementFromMethodId}{newEnableTime.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.NativeTokenEnabledTime.Get().Should().Be(newEnableTime.ToUInt64(null));
    }

    [Test]
    public void ParsesSetNativeTokenManagementFrom_CurrentEnableTimeIsGreaterThan7DaysFromNowButNewOneIsNot_Throws()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesisBlock = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);

        ulong now = 100;
        genesisBlock.Header.Timestamp = now;
        context.WithArbosState().WithBlockExecutionContext(genesisBlock.Header);

        ulong sevenDaysFromNow = now + ArbOwner.NativeTokenEnableDelay;
        context.ArbosState.NativeTokenEnabledTime.Set(sevenDaysFromNow + 1); // greater than 7 days from now

        // Setup input data
        string setNativeTokenManagementFromMethodId = "0xbdb8f707";
        UInt256 newEnableTime = 1; // less than 7 days in the future
        byte[] inputData = Bytes.FromHexString(
            $"{setNativeTokenManagementFromMethodId}{newEnableTime.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        Action action = () => arbOwnerParser.RunAdvanced(context, inputData);

        action
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("native token feature must be enabled at least 7 days in the future");
    }

    [Test]
    public void ParsesSetNativeTokenManagementFrom_CurrentEnableTimeIsLowerThan7DaysFromNowAndNewOneIsEvenSooner_Throws()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesisBlock = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);

        ulong now = 1;
        genesisBlock.Header.Timestamp = now;
        context.WithArbosState().WithBlockExecutionContext(genesisBlock.Header);

        context.ArbosState.NativeTokenEnabledTime.Set(3); // more than now but lower than 7 days from now

        // Setup input data
        string setNativeTokenManagementFromMethodId = "0xbdb8f707";
        UInt256 newEnableTime = 2; // less than current enabled time
        byte[] inputData = Bytes.FromHexString(
            $"{setNativeTokenManagementFromMethodId}{newEnableTime.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        Action action = () => arbOwnerParser.RunAdvanced(context, inputData);

        action
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("native token feature cannot be updated to a time earlier than the current time at which it is scheduled to be enabled");
    }

    [Test]
    public void ParsesSetNativeTokenManagementFrom_CorrectNewEnableTimeComparedToCurrentOne_SetsNewEnableTime()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesisBlock = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);

        ulong now = 0; // currently disabled
        genesisBlock.Header.Timestamp = now;
        context.WithArbosState().WithBlockExecutionContext(genesisBlock.Header);

        // Setup input data
        string setNativeTokenManagementFromMethodId = "0xbdb8f707";
        UInt256 newEnableTime = now + ArbOwner.NativeTokenEnableDelay; // >= 7 days from now
        byte[] inputData = Bytes.FromHexString(
            $"{setNativeTokenManagementFromMethodId}{newEnableTime.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.NativeTokenEnabledTime.Get().Should().Be(newEnableTime.ToUInt64(null));
    }

    [Test]
    public void ParsesAddNativeTokenOwner_NativeTokenManagementCurrentlyDisabled_Throws()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesisBlock = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);

        ulong now = 1;
        genesisBlock.Header.Timestamp = now;
        context.WithArbosState().WithBlockExecutionContext(genesisBlock.Header);

        context.ArbosState.NativeTokenEnabledTime.Set(now + 1); // scheduled to be enabled in the future

        // Setup input data
        string addNativeTokenOwnerMethodId = "0xaeb3a464";
        Address tokenOwnerToAdd = new("0x0000000000000000000000000000000000000123");
        byte[] inputData = Bytes.FromHexString(
            $"{addNativeTokenOwnerMethodId}{tokenOwnerToAdd.ToString(withZeroX: false, false).PadLeft(64, '0')}"
        );

        ArbOwnerParser arbOwnerParser = new();
        Action action = () => arbOwnerParser.RunAdvanced(context, inputData);

        action
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("native token feature is not enabled yet");
    }

    [Test]
    public void ParsesAddNativeTokenOwner_NativeTokenManagementIsEnabled_AddsNativeTokenOwner()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesisBlock = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);

        ulong now = 2;
        genesisBlock.Header.Timestamp = now;
        context.WithArbosState().WithBlockExecutionContext(genesisBlock.Header);

        context.ArbosState.NativeTokenEnabledTime.Set(now - 1); // already enabled

        // Setup input data
        string addNativeTokenOwnerMethodId = "0xaeb3a464";
        Address tokenOwnerToAdd = new("0x0000000000000000000000000000000000000123");
        byte[] inputData = Bytes.FromHexString(
            $"{addNativeTokenOwnerMethodId}{tokenOwnerToAdd.ToString(withZeroX: false, false).PadLeft(64, '0')}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.NativeTokenOwners.IsMember(tokenOwnerToAdd).Should().BeTrue();
    }

    [Test]
    public void ParsesRemoveNativeTokenOwner_NotAnOwner_Throws()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string removeNativeTokenOwnerMethodId = "0x96a3751d";
        Address tokenOwnerToRemove = new("0x0000000000000000000000000000000000000123");
        byte[] inputData = Bytes.FromHexString(
            $"{removeNativeTokenOwnerMethodId}{tokenOwnerToRemove.ToString(withZeroX: false, false).PadLeft(64, '0')}"
        );

        ArbOwnerParser arbOwnerParser = new();
        Action action = () => arbOwnerParser.RunAdvanced(context, inputData);

        action
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Tried to remove non native token owner");
    }

    [Test]
    public void ParsesRemoveNativeTokenOwner_IsAnOwner_RemovesNativeTokenOwner()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string removeNativeTokenOwnerMethodId = "0x96a3751d";
        Address tokenOwnerToRemove = new("0x0000000000000000000000000000000000000123");
        byte[] inputData = Bytes.FromHexString(
            $"{removeNativeTokenOwnerMethodId}{tokenOwnerToRemove.ToString(withZeroX: false, false).PadLeft(64, '0')}"
        );

        context.ArbosState.NativeTokenOwners.Add(tokenOwnerToRemove);

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.NativeTokenOwners.IsMember(tokenOwnerToRemove).Should().BeFalse();
    }

    [Test]
    public void ParsesIsNativeTokenOwner_IsAnOwner_ReturnsTrue()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string isNativeTokenOwnerMethodId = "0xc686f4db";
        Address tokenOwner = new("0x0000000000000000000000000000000000000123");
        byte[] inputData = Bytes.FromHexString(
            $"{isNativeTokenOwnerMethodId}{tokenOwner.ToString(withZeroX: false, false).PadLeft(64, '0')}"
        );

        context.ArbosState.NativeTokenOwners.Add(tokenOwner);

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        byte[] expectedResult = new byte[WordSize];
        expectedResult[WordSize - 1] = 1;
        result.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public void ParsesIsNativeTokenOwner_NotAnOwner_ReturnsFalse()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string isNativeTokenOwnerMethodId = "0xc686f4db";
        Address tokenOwner = new("0x0000000000000000000000000000000000000123");
        byte[] inputData = Bytes.FromHexString(
            $"{isNativeTokenOwnerMethodId}{tokenOwner.ToString(withZeroX: false, false).PadLeft(64, '0')}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        byte[] expectedResult = new byte[WordSize];
        result.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public void ParsesGetAllNativeTokenOwners_Always_ReturnsAllOwners()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
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

        // Setup input data
        string getAllNativeTokenOwnersMethodId = "0x3f8601e4";
        byte[] inputData = Bytes.FromHexString($"{getAllNativeTokenOwnersMethodId}");

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

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
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setL1BaseFeeEstimateInertiaMethodId = "0x718f7805";
        UInt256 inertia = 123;
        byte[] inputData = Bytes.FromHexString(
            $"{setL1BaseFeeEstimateInertiaMethodId}{inertia.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.L1PricingState.InertiaStorage.Get().Should().Be(inertia.ToUInt64(null));
    }

    [Test]
    public void ParsesSetL2BaseFee_Always_SetsL2BaseFee()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setL2BaseFeeMethodId = "0xd99bc80e";
        UInt256 l2BaseFee = 123;
        byte[] inputData = Bytes.FromHexString(
            $"{setL2BaseFeeMethodId}{l2BaseFee.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.L2PricingState.BaseFeeWeiStorage.Get().Should().Be(l2BaseFee);
    }

    [Test]
    public void ParsesSetMinimumL2BaseFee_CallIsMutating_SetsMinimumL2BaseFee()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setL2BaseFeeMethodId = "0xa0188cdb";
        UInt256 l2BaseFee = 123;
        byte[] inputData = Bytes.FromHexString(
            $"{setL2BaseFeeMethodId}{l2BaseFee.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.L2PricingState.MinBaseFeeWeiStorage.Get().Should().Be(l2BaseFee);
    }

    [Test]
    public void ParsesSetSpeedLimit_IsZero_Throws()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setSpeedLimitMethodId = "0x4d7a060d";
        UInt256 limit = 0;
        byte[] inputData = Bytes.FromHexString(
            $"{setSpeedLimitMethodId}{limit.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        Action action = () => arbOwnerParser.RunAdvanced(context, inputData);

        action.Should().Throw<InvalidOperationException>().WithMessage("speed limit must be nonzero");
    }

    [Test]
    public void ParsesSetSpeedLimit_IsNonZero_SetsSpeedLimit()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setSpeedLimitMethodId = "0x4d7a060d";
        UInt256 limit = 123;
        byte[] inputData = Bytes.FromHexString(
            $"{setSpeedLimitMethodId}{limit.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.L2PricingState.SpeedLimitPerSecondStorage.Get().Should().Be(limit.ToUInt64(null));
    }

    [Test]
    public void ParsesSetMaxTxGasLimit_Always_SetsMaxTxGasLimit()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setMaxTxGasLimitMethodId = "0x39673611";
        UInt256 limit = 123;
        byte[] inputData = Bytes.FromHexString(
            $"{setMaxTxGasLimitMethodId}{limit.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.L2PricingState.PerBlockGasLimitStorage.Get().Should().Be(limit.ToUInt64(null));
    }

    [Test]
    public void ParsesSetL2GasPricingInertia_IsZero_Throws()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setL2GasPricingInertiaMethodId = "0x3fd62a29";
        UInt256 inertia = 0;
        byte[] inputData = Bytes.FromHexString(
            $"{setL2GasPricingInertiaMethodId}{inertia.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        Action action = () => arbOwnerParser.RunAdvanced(context, inputData);

        action.Should().Throw<InvalidOperationException>().WithMessage("price inertia must be nonzero");
    }

    [Test]
    public void ParsesSetL2GasPricingInertia_IsNonZero_SetsL2GasPricingInertia()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setL2GasPricingInertiaMethodId = "0x3fd62a29";
        UInt256 inertia = 123;
        byte[] inputData = Bytes.FromHexString(
            $"{setL2GasPricingInertiaMethodId}{inertia.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.L2PricingState.PricingInertiaStorage.Get().Should().Be(inertia.ToUInt64(null));
    }

    [Test]
    public void ParsesSetL2GasBacklogTolerance_Always_SetsL2GasBacklogTolerance()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setL2GasBacklogToleranceMethodId = "0x198e7157";
        UInt256 backlogTolerance = 123;
        byte[] inputData = Bytes.FromHexString(
            $"{setL2GasBacklogToleranceMethodId}{backlogTolerance.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.L2PricingState.BacklogToleranceStorage.Get().Should().Be(backlogTolerance.ToUInt64(null));
    }

    [Test]
    public void ParsesGetNetworkFeeAccount_Always_ReturnsNetworkFeeAccount()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string getNetworkFeeAccountMethodId = "0x2d9125e9";
        byte[] inputData = Bytes.FromHexString($"{getNetworkFeeAccountMethodId}");

        Address networkFeeAccount = new("0x0000000000000000000000000000000000000123");
        context.ArbosState.NetworkFeeAccount.Set(networkFeeAccount);

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        byte[] expectedResult = new byte[WordSize];
        networkFeeAccount.Bytes.CopyTo(expectedResult, WordSize - Address.Size);
        result.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public void ParsesGetInfraFeeAccount_Always_ReturnsInfraFeeAccount()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string getInfraFeeAccountMethodId = "0xee95a824";
        byte[] inputData = Bytes.FromHexString($"{getInfraFeeAccountMethodId}");

        Address infraFeeAccount = new("0x0000000000000000000000000000000000000123");
        context.ArbosState.InfraFeeAccount.Set(infraFeeAccount);

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        byte[] expectedResult = new byte[WordSize];
        infraFeeAccount.Bytes.CopyTo(expectedResult, WordSize - Address.Size);
        result.Should().BeEquivalentTo(expectedResult);
    }

    [Test]
    public void ParsesSetNetworkFeeAccount_Always_SetsNetworkFeeAccount()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setNetworkFeeAccountMethodId = "0xfcdde2b4";
        Address networkFeeAccount = new("0x0000000000000000000000000000000000000456");
        byte[] inputData = Bytes.FromHexString(
            $"{setNetworkFeeAccountMethodId}{networkFeeAccount.ToString(withZeroX: false, false).PadLeft(64, '0')}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.NetworkFeeAccount.Get().Should().Be(networkFeeAccount);
    }

    [Test]
    public void ParsesSetInfraFeeAccount_Always_SetsInfraFeeAccount()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setInfraFeeAccountMethodId = "0x57f585db";
        Address infraFeeAccount = new("0x0000000000000000000000000000000000000456");
        byte[] inputData = Bytes.FromHexString(
            $"{setInfraFeeAccountMethodId}{infraFeeAccount.ToString(withZeroX: false, false).PadLeft(64, '0')}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.InfraFeeAccount.Get().Should().Be(infraFeeAccount);
    }

    [Test]
    public void ParsesScheduleArbOSUpgrade_Always_SetsArbosUpgrade()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string scheduleArbOSUpgradeMethodId = "0xe388b381";
        UInt256 version = 123;
        UInt256 timestamp = 456;
        byte[] inputData = Bytes.FromHexString(
            $"{scheduleArbOSUpgradeMethodId}{version.ToBigEndian().ToHexString(withZeroX: false)}{timestamp.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.UpgradeVersion.Get().Should().Be(version.ToUInt64(null));
        context.ArbosState.UpgradeTimestamp.Get().Should().Be(timestamp.ToUInt64(null));
    }

    [Test]
    public void ParsesSetL1PricingEquilibrationUnits_Always_SetsL1PricingEquilibrationUnits()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setL1PricingEquilibrationUnitsMethodId = "0x152db696";
        UInt256 units = 123;
        byte[] inputData = Bytes.FromHexString(
            $"{setL1PricingEquilibrationUnitsMethodId}{units.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.L1PricingState.EquilibrationUnitsStorage.Get().Should().Be(units.ToUInt64(null));
    }

    [Test]
    public void ParsesSetL1PricingInertia_Always_SetsL1PricingInertia()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setL1PricingInertiaMethodId = "0x775a82e9";
        UInt256 inertia = 123;
        byte[] inputData = Bytes.FromHexString(
            $"{setL1PricingInertiaMethodId}{inertia.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.L1PricingState.InertiaStorage.Get().Should().Be(inertia.ToUInt64(null));
    }

    [Test]
    public void ParsesSetL1PricingRewardRecipient_Always_SetsL1PricingRewardRecipient()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setL1PricingRewardRecipientMethodId = "0x934be07d";
        Address recipient = new("0x0000000000000000000000000000000000000123");
        byte[] inputData = Bytes.FromHexString(
            $"{setL1PricingRewardRecipientMethodId}{recipient.ToString(withZeroX: false, false).PadLeft(64, '0')}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.L1PricingState.PayRewardsToStorage.Get().Should().Be(recipient);
    }

    [Test]
    public void ParsesSetL1PricingRewardRate_Always_SetsL1PricingRewardRate()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setL1PricingRewardRateMethodId = "0xf6739500";
        UInt256 weiPerUnit = 123;
        byte[] inputData = Bytes.FromHexString(
            $"{setL1PricingRewardRateMethodId}{weiPerUnit.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.L1PricingState.PerUnitRewardStorage.Get().Should().Be(weiPerUnit.ToUInt64(null));
    }

    [Test]
    public void ParsesSetL1PricePerUnit_Always_SetsL1PricePerUnit()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setL1PricePerUnitMethodId = "0x2b352fae";
        UInt256 pricePerUnit = 123;
        byte[] inputData = Bytes.FromHexString(
            $"{setL1PricePerUnitMethodId}{pricePerUnit.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.L1PricingState.PricePerUnitStorage.Get().Should().Be(pricePerUnit.ToUInt64(null));
    }

    [Test]
    public void ParsesSetPerBatchGasCharge_Always_SetsPerBatchGasCharge()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setPerBatchGasChargeMethodId = "0xfad7f20b";
        UInt256 baseCharge = 123;
        byte[] inputData = Bytes.FromHexString(
            $"{setPerBatchGasChargeMethodId}{baseCharge.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.L1PricingState.PerBatchGasCostStorage.Get().Should().Be(baseCharge.ToUInt64(null));
    }

    [Test]
    public void ParsesSetAmortizedCostCapBips_Always_SetsAmortizedCostCapBips()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setAmortizedCostCapBipsMethodId = "0x56191cc3";
        UInt256 cap = 123;
        byte[] inputData = Bytes.FromHexString(
            $"{setAmortizedCostCapBipsMethodId}{cap.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.L1PricingState.AmortizedCostCapBipsStorage.Get().Should().Be(cap.ToUInt64(null));
    }

    [Test]
    public void ParsesSetBrotliCompressionLevel_Always_SetsBrotliCompressionLevel()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setBrotliCompressionLevelMethodId = "0x5399126f";
        UInt256 level = 10;
        byte[] inputData = Bytes.FromHexString(
            $"{setBrotliCompressionLevelMethodId}{level.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.BrotliCompressionLevel.Get().Should().Be(level.ToUInt64(null));
    }

    [Test]
    public void ParsesReleaseL1PricerSurplusFunds_RecognizedFundsGreaterThanPoolBalance_ReturnsZero()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        UInt256 poolBalance = 123;
        context.WorldState.AddToBalanceAndCreateIfNotExists(ArbosAddresses.L1PricerFundsPoolAddress, poolBalance, London.Instance);
        UInt256 recognized = poolBalance + 1; // greater than pool balance
        context.ArbosState.L1PricingState.L1FeesAvailableStorage.Set(recognized);

        // Setup input data
        string releaseL1PricerSurplusFundsMethodId = "0x314bcf05";
        UInt256 maxWeiToRelease = 111; // does not matter for that test case
        byte[] inputData = Bytes.FromHexString(
            $"{releaseL1PricerSurplusFundsMethodId}{maxWeiToRelease.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEquivalentTo(new byte[WordSize]);
        context.ArbosState.L1PricingState.L1FeesAvailableStorage.Get().Should().Be(recognized.ToUInt64(null));
    }

    [Test]
    public void ParsesReleaseL1PricerSurplusFunds_RecognizedFundsLowerThanPoolBalance_ReturnsWeiToTransfer()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        UInt256 poolBalance = 100;
        context.WorldState.AddToBalanceAndCreateIfNotExists(ArbosAddresses.L1PricerFundsPoolAddress, poolBalance, London.Instance);
        UInt256 recognized = 40; // lower (or equal) than pool balance
        context.ArbosState.L1PricingState.L1FeesAvailableStorage.Set(recognized);

        // Setup input data
        string releaseL1PricerSurplusFundsMethodId = "0x314bcf05";
        UInt256 maxWeiToRelease = 50; // lower than poolBalance - recognized
        byte[] inputData = Bytes.FromHexString(
            $"{releaseL1PricerSurplusFundsMethodId}{maxWeiToRelease.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        byte[] expectedResult = new byte[WordSize];
        expectedResult[WordSize - 1] = (byte)maxWeiToRelease;
        result.Should().BeEquivalentTo(expectedResult);
        context.ArbosState.L1PricingState.L1FeesAvailableStorage.Get().Should().Be((recognized + maxWeiToRelease).ToUInt64(null));
    }

    [Test]
    public void ParsesSetInkPrice_PriceGreaterThanUint24_Throws()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setInkPriceMethodId = "0x8c1d4fda";
        UInt256 inkPrice = 1 << 24; // bigger than 24 bits (uint24)
        byte[] inputData = Bytes.FromHexString(
            $"{setInkPriceMethodId}{inkPrice.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        Action action = () => arbOwnerParser.RunAdvanced(context, inputData);

        action.Should().Throw<InvalidOperationException>().WithMessage("ink price must be a positive uint24");
    }

    [Test]
    public void ParsesSetInkPrice_PriceFitsWithinUint24_SetsInkPrice()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setInkPriceMethodId = "0x8c1d4fda";
        UInt256 inkPrice = 123;
        byte[] inputData = Bytes.FromHexString(
            $"{setInkPriceMethodId}{inkPrice.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.Programs.GetParams().InkPrice.Should().Be(inkPrice.ToUInt32(null));
    }

    [Test]
    public void ParsesSetWasmMaxStackDepth_Always_SetsWasmMaxStackDepth()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setWasmMaxStackDepthMethodId = "0x4567cc8e";
        UInt256 maxStackDepth = 123;
        byte[] inputData = Bytes.FromHexString(
            $"{setWasmMaxStackDepthMethodId}{maxStackDepth.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.Programs.GetParams().MaxStackDepth.Should().Be(maxStackDepth.ToUInt32(null));
    }

    [Test]
    public void ParsesSetWasmFreePages_Always_SetsWasmFreePages()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setWasmMaxStackDepthMethodId = "0x3f37a846";
        UInt256 freePages = 123;
        byte[] inputData = Bytes.FromHexString(
            $"{setWasmMaxStackDepthMethodId}{freePages.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.Programs.GetParams().FreePages.Should().Be(freePages.ToUInt16(null));
    }

    [Test]
    public void ParsesSetWasmPageGas_Always_SetsWasmPageGas()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setWasmPageGasMethodId = "0xaaa619e0";
        UInt256 pageGas = 123;
        byte[] inputData = Bytes.FromHexString(
            $"{setWasmPageGasMethodId}{pageGas.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.Programs.GetParams().PageGas.Should().Be(pageGas.ToUInt16(null));
    }

    [Test]
    public void ParsesSetWasmPageLimit_Always_SetsWasmPageLimit()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setWasmPageLimitMethodId = "0x6595381a";
        UInt256 pageLimit = 123;
        byte[] inputData = Bytes.FromHexString(
            $"{setWasmPageLimitMethodId}{pageLimit.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.Programs.GetParams().PageLimit.Should().Be(pageLimit.ToUInt16(null));
    }

    [Test]
    public void ParsesSetWasmMinInitGas_Always_SetsWasmMinInitGas()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setWasmMinInitGasMethodId = "0x8293405e";
        // greater than byte.MaxValue once divided by MinInitGasUnits
        UInt256 gas = StylusParams.MinInitGasUnits * 1 << 8;
        UInt256 cached = StylusParams.MinCachedGasUnits + 1; // ceiling div gives 2
        byte[] inputData = Bytes.FromHexString(
            $"{setWasmMinInitGasMethodId}{gas.ToBigEndian().ToHexString(withZeroX: false)}{cached.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.Programs.GetParams().MinInitGas.Should().Be(byte.MaxValue); // got saturated
        context.ArbosState.Programs.GetParams().MinCachedInitGas.Should().Be(2); // ceiling div
    }

    [Test]
    public void ParsesSetWasmInitCostScalar_Always_SetsWasmInitCostScalar()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setWasmInitCostScalarMethodId = "0x67e0718f";
        UInt256 percent = StylusParams.CostScalarPercent; // ceiling div gives 1
        byte[] inputData = Bytes.FromHexString(
            $"{setWasmInitCostScalarMethodId}{percent.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.Programs.GetParams().InitCostScalar.Should().Be(1);
    }

    [Test]
    public void ParsesSetWasmExpiryDays_Always_SetsWasmExpiryDays()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setWasmExpiryDaysMethodId = "0xaac68018";
        UInt256 expiryDays = 123;
        byte[] inputData = Bytes.FromHexString(
            $"{setWasmExpiryDaysMethodId}{expiryDays.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.Programs.GetParams().ExpiryDays.Should().Be(expiryDays.ToUInt16(null));
    }

    [Test]
    public void ParsesSetWasmKeepaliveDays_Always_SetsWasmKeepaliveDays()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setWasmKeepaliveDaysMethodId = "0x2a9cbe3e";
        UInt256 keepaliveDays = 123;
        byte[] inputData = Bytes.FromHexString(
            $"{setWasmKeepaliveDaysMethodId}{keepaliveDays.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.Programs.GetParams().KeepaliveDays.Should().Be(keepaliveDays.ToUInt16(null));
    }

    [Test]
    public void ParsesSetWasmBlockCacheSize_Always_SetsWasmBlockCacheSize()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setWasmBlockCacheSizeMethodId = "0x380f1457";
        UInt256 blockCacheSize = 123;
        byte[] inputData = Bytes.FromHexString(
            $"{setWasmBlockCacheSizeMethodId}{blockCacheSize.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

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

        // Setup input data
        string setWasmMaxSizeMethodId = "0x455ec2eb";
        UInt256 maxWasmSize = 123;
        byte[] inputData = Bytes.FromHexString(
            $"{setWasmMaxSizeMethodId}{maxWasmSize.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.Programs.GetParams().MaxWasmSize.Should().Be(maxWasmSize.ToUInt32(null));
    }

    [Test]
    public void ParsesAddWasmCacheManager_Always_AddsWasmCacheManager()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string addWasmCacheManagerMethodId = "0xffdca515";
        Address manager = new("0x0000000000000000000000000000000000000123");
        byte[] inputData = Bytes.FromHexString(
            $"{addWasmCacheManagerMethodId}{manager.ToString(withZeroX: false, false).PadLeft(64, '0')}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.Programs.CacheManagersStorage.IsMember(manager).Should().BeTrue();
    }

    [Test]
    public void ParsesRemoveWasmCacheManager_IsNotManager_Throws()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string removeWasmCacheManagerMethodId = "0xbf197322";
        Address manager = new("0x0000000000000000000000000000000000000123");
        byte[] inputData = Bytes.FromHexString(
            $"{removeWasmCacheManagerMethodId}{manager.ToString(withZeroX: false, false).PadLeft(64, '0')}"
        );

        ArbOwnerParser arbOwnerParser = new();
        Action action = () => arbOwnerParser.RunAdvanced(context, inputData);

        action.Should().Throw<InvalidOperationException>().WithMessage("Tried to remove non-manager");
    }

    [Test]
    public void ParsesRemoveWasmCacheManager_IsManager_RemovesWasmCacheManager()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string removeWasmCacheManagerMethodId = "0xbf197322";
        Address manager = new("0x0000000000000000000000000000000000000123");
        byte[] inputData = Bytes.FromHexString(
            $"{removeWasmCacheManagerMethodId}{manager.ToString(withZeroX: false, false).PadLeft(64, '0')}"
        );

        context.ArbosState.Programs.CacheManagersStorage.Add(manager);
        Debug.Assert(context.ArbosState.Programs.CacheManagersStorage.IsMember(manager));

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.Programs.CacheManagersStorage.IsMember(manager).Should().BeFalse();
    }

    [Test]
    public void ParsesSetChainConfig_CallIsNonMutating_ReplacesChainConfig()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
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

        // Setup input data
        string setChainConfigMethodId = "0xeda73212";
        UInt256 offsetToDataSection = 32;
        UInt256 dataLength = (UInt256)newSerializedConfig.Length;
        byte[] inputData = Bytes.FromHexString(
            $"{setChainConfigMethodId}{offsetToDataSection.ToBigEndian().ToHexString(withZeroX: false)}{dataLength.ToBigEndian().ToHexString(withZeroX: false)}{newSerializedConfig.ToHexString(withZeroX: false)}"
        );

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.ChainConfigStorage.Get().Should().BeEquivalentTo(newSerializedConfig);
    }

    [Test]
    public void ParsesSetCalldataPriceIncrease_ToEnable_EnablesCalldataPriceIncrease()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setCalldataPriceIncreaseMethodId = "0x8eb911d9";
        UInt256 enabled = 1;
        byte[] inputData = Bytes.FromHexString(
            $"{setCalldataPriceIncreaseMethodId}{enabled.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        // Sets some initial random features
        Span<byte> bytes = stackalloc byte[32];
        Random rng = new();
        rng.NextBytes(bytes);
        bytes[31] &= 0xFE; // Ensure even number (IncreasedCalldataFeature corresponds to bit 0)
        UInt256 features = new(bytes, isBigEndian: true);

        context.ArbosState.Features.FeaturesStorage.Set(features);
        Debug.Assert(!context.ArbosState.Features.IsCalldataPriceIncreaseEnabled());

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.Features.IsCalldataPriceIncreaseEnabled().Should().Be(true);
        context.ArbosState.Features.FeaturesStorage.Get().Should().Be(features + 1);
    }

    [Test]
    public void ParsesSetCalldataPriceIncrease_ToDisable_DisablesCalldataPriceIncrease()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);
        PrecompileTestContextBuilder context = new(worldState, GasSupplied: ulong.MaxValue);
        context.WithArbosState();

        // Setup input data
        string setCalldataPriceIncreaseMethodId = "0x8eb911d9";
        UInt256 enabled = 0;
        byte[] inputData = Bytes.FromHexString(
            $"{setCalldataPriceIncreaseMethodId}{enabled.ToBigEndian().ToHexString(withZeroX: false)}"
        );

        // Sets some initial random features
        Span<byte> bytes = stackalloc byte[32];
        Random rng = new();
        rng.NextBytes(bytes);
        bytes[31] |= 0x1; // Ensures odd number (IncreasedCalldataFeature corresponds to bit 0)
        UInt256 features = new(bytes, isBigEndian: true);

        context.ArbosState.Features.FeaturesStorage.Set(features);
        Debug.Assert(context.ArbosState.Features.IsCalldataPriceIncreaseEnabled());

        ArbOwnerParser arbOwnerParser = new();
        byte[] result = arbOwnerParser.RunAdvanced(context, inputData);

        result.Should().BeEmpty();
        context.ArbosState.Features.IsCalldataPriceIncreaseEnabled().Should().Be(false);
        context.ArbosState.Features.FeaturesStorage.Get().Should().Be(features - 1);
    }
}
