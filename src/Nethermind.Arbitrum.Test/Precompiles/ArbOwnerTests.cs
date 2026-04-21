// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text;
using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public class ArbOwnerTests
{
    private const ulong DefaultBlockTimestamp = 1_700_000_000;

    private static readonly Address ExampleOwnerA = new("0x0000000000000000000000000000000000000aaa");
    private static readonly Address ExampleOwnerB = new("0x0000000000000000000000000000000000000bbb");

    // keccak256("OwnerActs(bytes4,address,bytes)")
    private static readonly Hash256 ExpectedOwnerActsTopic =
        new("0x3c9e6a772755407311e3b35b3ee56799df8f87395941b3a658eee9e08a67ebda");

    [Test]
    public void Abi_WhenParsed_ContainsExpectedFunctionSignatures()
    {
        Dictionary<uint, ArbitrumFunctionDescription> allFunctions = AbiMetadata.GetAllFunctionDescriptions(ArbOwner.Abi);

        allFunctions.Keys.Should().BeEquivalentTo(new[]
        {
            PrecompileHelper.GetMethodId("addChainOwner(address)"),
            PrecompileHelper.GetMethodId("removeChainOwner(address)"),
            PrecompileHelper.GetMethodId("isChainOwner(address)"),
            PrecompileHelper.GetMethodId("getAllChainOwners()"),
            PrecompileHelper.GetMethodId("setNativeTokenManagementFrom(uint64)"),
            PrecompileHelper.GetMethodId("addNativeTokenOwner(address)"),
            PrecompileHelper.GetMethodId("removeNativeTokenOwner(address)"),
            PrecompileHelper.GetMethodId("isNativeTokenOwner(address)"),
            PrecompileHelper.GetMethodId("getAllNativeTokenOwners()"),
            PrecompileHelper.GetMethodId("setL1BaseFeeEstimateInertia(uint64)"),
            PrecompileHelper.GetMethodId("setL2BaseFee(uint256)"),
            PrecompileHelper.GetMethodId("setMinimumL2BaseFee(uint256)"),
            PrecompileHelper.GetMethodId("setSpeedLimit(uint64)"),
            PrecompileHelper.GetMethodId("setMaxTxGasLimit(uint64)"),
            PrecompileHelper.GetMethodId("setMaxBlockGasLimit(uint64)"),
            PrecompileHelper.GetMethodId("setL2GasPricingInertia(uint64)"),
            PrecompileHelper.GetMethodId("setL2GasBacklogTolerance(uint64)"),
            PrecompileHelper.GetMethodId("getNetworkFeeAccount()"),
            PrecompileHelper.GetMethodId("getInfraFeeAccount()"),
            PrecompileHelper.GetMethodId("setNetworkFeeAccount(address)"),
            PrecompileHelper.GetMethodId("setInfraFeeAccount(address)"),
            PrecompileHelper.GetMethodId("scheduleArbOSUpgrade(uint64,uint64)"),
            PrecompileHelper.GetMethodId("setL1PricingEquilibrationUnits(uint256)"),
            PrecompileHelper.GetMethodId("setL1PricingInertia(uint64)"),
            PrecompileHelper.GetMethodId("setL1PricingRewardRecipient(address)"),
            PrecompileHelper.GetMethodId("setL1PricingRewardRate(uint64)"),
            PrecompileHelper.GetMethodId("setL1PricePerUnit(uint256)"),
            PrecompileHelper.GetMethodId("setPerBatchGasCharge(int64)"),
            PrecompileHelper.GetMethodId("setBrotliCompressionLevel(uint64)"),
            PrecompileHelper.GetMethodId("setAmortizedCostCapBips(uint64)"),
            PrecompileHelper.GetMethodId("releaseL1PricerSurplusFunds(uint256)"),
            PrecompileHelper.GetMethodId("setInkPrice(uint32)"),
            PrecompileHelper.GetMethodId("setWasmMaxStackDepth(uint32)"),
            PrecompileHelper.GetMethodId("setWasmFreePages(uint16)"),
            PrecompileHelper.GetMethodId("setWasmPageGas(uint16)"),
            PrecompileHelper.GetMethodId("setWasmPageLimit(uint16)"),
            PrecompileHelper.GetMethodId("setWasmMaxSize(uint32)"),
            PrecompileHelper.GetMethodId("setWasmMinInitGas(uint8,uint16)"),
            PrecompileHelper.GetMethodId("setWasmInitCostScalar(uint64)"),
            PrecompileHelper.GetMethodId("setWasmExpiryDays(uint16)"),
            PrecompileHelper.GetMethodId("setWasmKeepaliveDays(uint16)"),
            PrecompileHelper.GetMethodId("setWasmBlockCacheSize(uint16)"),
            PrecompileHelper.GetMethodId("addWasmCacheManager(address)"),
            PrecompileHelper.GetMethodId("removeWasmCacheManager(address)"),
            PrecompileHelper.GetMethodId("setChainConfig(string)"),
            PrecompileHelper.GetMethodId("setCalldataPriceIncrease(bool)"),
            PrecompileHelper.GetMethodId("setParentGasFloorPerToken(uint64)"),
            PrecompileHelper.GetMethodId("setGasBacklog(uint64)"),
            PrecompileHelper.GetMethodId("setGasPricingConstraints(uint64[3][])"),
        });
    }

    [Test]
    public void Abi_WhenParsed_ContainsExpectedEvents()
    {
        Dictionary<string, AbiEventDescription> allEvents = AbiMetadata.GetAllEventDescriptions(ArbOwner.Abi);

        allEvents.Keys.Should().BeEquivalentTo("OwnerActs");
    }

    [Test]
    public void Abi_WhenParsed_ContainsNoErrors()
    {
        AbiMetadata.GetAllErrorDescriptions(ArbOwner.Abi).Should().BeEmpty();
    }

    [Test]
    public void MethodIds_OwnershipManagement_MatchExpectedSelectors()
    {
        PrecompileHelper.GetMethodId("addChainOwner(address)").Should().Be(0x481f8dbfu);
        PrecompileHelper.GetMethodId("removeChainOwner(address)").Should().Be(0x8792701au);
        PrecompileHelper.GetMethodId("isChainOwner(address)").Should().Be(0x26ef7f68u);
        PrecompileHelper.GetMethodId("getAllChainOwners()").Should().Be(0x516b4e0fu);
        PrecompileHelper.GetMethodId("setNativeTokenManagementFrom(uint64)").Should().Be(0xbdb8f707u);
        PrecompileHelper.GetMethodId("addNativeTokenOwner(address)").Should().Be(0xaeb3a464u);
        PrecompileHelper.GetMethodId("removeNativeTokenOwner(address)").Should().Be(0x96a3751du);
        PrecompileHelper.GetMethodId("isNativeTokenOwner(address)").Should().Be(0xc686f4dbu);
        PrecompileHelper.GetMethodId("getAllNativeTokenOwners()").Should().Be(0x3f8601e4u);
    }

    [Test]
    public void MethodIds_GasAndFeeParameters_MatchExpectedSelectors()
    {
        PrecompileHelper.GetMethodId("setL1BaseFeeEstimateInertia(uint64)").Should().Be(0x718f7805u);
        PrecompileHelper.GetMethodId("setL2BaseFee(uint256)").Should().Be(0xd99bc80eu);
        PrecompileHelper.GetMethodId("setMinimumL2BaseFee(uint256)").Should().Be(0xa0188cdbu);
        PrecompileHelper.GetMethodId("setSpeedLimit(uint64)").Should().Be(0x4d7a060du);
        PrecompileHelper.GetMethodId("setMaxTxGasLimit(uint64)").Should().Be(0x39673611u);
        PrecompileHelper.GetMethodId("setMaxBlockGasLimit(uint64)").Should().Be(0xae105c80u);
        PrecompileHelper.GetMethodId("setL2GasPricingInertia(uint64)").Should().Be(0x3fd62a29u);
        PrecompileHelper.GetMethodId("setL2GasBacklogTolerance(uint64)").Should().Be(0x198e7157u);
        PrecompileHelper.GetMethodId("setL1PricingEquilibrationUnits(uint256)").Should().Be(0x152db696u);
        PrecompileHelper.GetMethodId("setL1PricingInertia(uint64)").Should().Be(0x775a82e9u);
        PrecompileHelper.GetMethodId("setL1PricingRewardRecipient(address)").Should().Be(0x934be07du);
        PrecompileHelper.GetMethodId("setL1PricingRewardRate(uint64)").Should().Be(0xf6739500u);
        PrecompileHelper.GetMethodId("setL1PricePerUnit(uint256)").Should().Be(0x2b352faeu);
        PrecompileHelper.GetMethodId("setPerBatchGasCharge(int64)").Should().Be(0xfad7f20bu);
        PrecompileHelper.GetMethodId("setBrotliCompressionLevel(uint64)").Should().Be(0x5399126fu);
        PrecompileHelper.GetMethodId("setAmortizedCostCapBips(uint64)").Should().Be(0x56191cc3u);
        PrecompileHelper.GetMethodId("releaseL1PricerSurplusFunds(uint256)").Should().Be(0x314bcf05u);
        PrecompileHelper.GetMethodId("setGasBacklog(uint64)").Should().Be(0x68fc808au);
        PrecompileHelper.GetMethodId("setGasPricingConstraints(uint64[3][])").Should().Be(0xcc0d556au);
    }

    [Test]
    public void MethodIds_InfrastructureAccounts_MatchExpectedSelectors()
    {
        PrecompileHelper.GetMethodId("getNetworkFeeAccount()").Should().Be(0x2d9125e9u);
        PrecompileHelper.GetMethodId("getInfraFeeAccount()").Should().Be(0xee95a824u);
        PrecompileHelper.GetMethodId("setNetworkFeeAccount(address)").Should().Be(0xfcdde2b4u);
        PrecompileHelper.GetMethodId("setInfraFeeAccount(address)").Should().Be(0x57f585dbu);
        PrecompileHelper.GetMethodId("scheduleArbOSUpgrade(uint64,uint64)").Should().Be(0xe388b381u);
        PrecompileHelper.GetMethodId("setChainConfig(string)").Should().Be(0xeda73212u);
        PrecompileHelper.GetMethodId("setCalldataPriceIncrease(bool)").Should().Be(0x8eb911d9u);
        PrecompileHelper.GetMethodId("setParentGasFloorPerToken(uint64)").Should().Be(0x3a930b0bu);
    }

    [Test]
    public void MethodIds_StylusAndWasm_MatchExpectedSelectors()
    {
        PrecompileHelper.GetMethodId("setInkPrice(uint32)").Should().Be(0x8c1d4fdau);
        PrecompileHelper.GetMethodId("setWasmMaxStackDepth(uint32)").Should().Be(0x4567cc8eu);
        PrecompileHelper.GetMethodId("setWasmFreePages(uint16)").Should().Be(0x3f37a846u);
        PrecompileHelper.GetMethodId("setWasmPageGas(uint16)").Should().Be(0xaaa619e0u);
        PrecompileHelper.GetMethodId("setWasmPageLimit(uint16)").Should().Be(0x6595381au);
        PrecompileHelper.GetMethodId("setWasmMaxSize(uint32)").Should().Be(0x455ec2ebu);
        PrecompileHelper.GetMethodId("setWasmMinInitGas(uint8,uint16)").Should().Be(0x8293405eu);
        PrecompileHelper.GetMethodId("setWasmInitCostScalar(uint64)").Should().Be(0x67e0718fu);
        PrecompileHelper.GetMethodId("setWasmExpiryDays(uint16)").Should().Be(0xaac68018u);
        PrecompileHelper.GetMethodId("setWasmKeepaliveDays(uint16)").Should().Be(0x2a9cbe3eu);
        PrecompileHelper.GetMethodId("setWasmBlockCacheSize(uint16)").Should().Be(0x380f1457u);
        PrecompileHelper.GetMethodId("addWasmCacheManager(address)").Should().Be(0xffdca515u);
        PrecompileHelper.GetMethodId("removeWasmCacheManager(address)").Should().Be(0xbf197322u);
    }

    [Test]
    public void EventTopics_AllEvents_MatchExpectedHashes()
    {
        ArbOwner.OwnerActsEvent.GetHash().Should().Be(ExpectedOwnerActsTopic);
    }

    [Test]
    public void SetGasPricingConstraints_BelowFiftyArbOSVersion_IsRejected()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);
        context.WithArbosVersion(ArbosVersion.Fifty - 1);

        bool result = ArbOwnerParser.Instance.TryCheckMethodVisibility(context, NullLogger.Instance,
            PrecompileHelper.GetMethodId("setGasPricingConstraints(uint64[3][])"), out bool shouldRevert, out _);

        result.Should().BeFalse();
        shouldRevert.Should().BeTrue();
    }

    [Test]
    public void SetGasPricingConstraints_AtFiftyArbOSVersion_IsDispatched()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);
        context = context.WithExecutingAccount(ArbOwnerParser.Address);

        bool result = ArbOwnerParser.Instance.TryCheckMethodVisibility(context, NullLogger.Instance,
            PrecompileHelper.GetMethodId("setGasPricingConstraints(uint64[3][])"), out bool _, out PrecompileHandler? handler);

        result.Should().BeTrue();
        handler.Should().NotBeNull();
    }

    [Test]
    public void AddChainOwner_NewAddress_AddsToOwnerSet()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.AddChainOwner(context, ExampleOwnerA);

        context.ArbosState.ChainOwners.IsMember(ExampleOwnerA).Should().BeTrue();
    }

    [Test]
    public void RemoveChainOwner_ExistingOwner_RemovesFromOwnerSet()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);
        context.ArbosState.ChainOwners.Add(ExampleOwnerA);

        ArbOwner.RemoveChainOwner(context, ExampleOwnerA);

        context.ArbosState.ChainOwners.IsMember(ExampleOwnerA).Should().BeFalse();
    }

    [Test]
    public void RemoveChainOwner_NonExistentOwner_Throws()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        Action act = () => ArbOwner.RemoveChainOwner(context, ExampleOwnerA);

        act.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*non-owner*");
    }

    [Test]
    public void IsChainOwner_NotInOwnerSet_ReturnsFalse()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.IsChainOwner(context, ExampleOwnerA).Should().BeFalse();
    }

    [Test]
    public void IsChainOwner_InOwnerSet_ReturnsTrue()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);
        context.ArbosState.ChainOwners.Add(ExampleOwnerA);

        ArbOwner.IsChainOwner(context, ExampleOwnerA).Should().BeTrue();
    }

    [Test]
    public void GetAllChainOwners_AfterMutations_ReflectsAddAndRemove()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        // Genesis seeds one initial chain owner; assert only the additions/removals we drove.
        ArbOwner.AddChainOwner(context, ExampleOwnerA);
        ArbOwner.AddChainOwner(context, ExampleOwnerB);
        ArbOwner.RemoveChainOwner(context, ExampleOwnerA);

        Address[] all = ArbOwner.GetAllChainOwners(context);
        all.Should().Contain(ExampleOwnerB);
        all.Should().NotContain(ExampleOwnerA);
    }

    [Test]
    public void AddChainOwner_NonOwnerCaller_PassesBusinessLogicUnchecked()
    {
        // Authorization (IsOwner) is enforced at the VM dispatch layer (OwnerPrecompileCall ->
        // FreeArbosState.ChainOwners.IsMember(caller)), not in business logic. This test pins
        // the contract: business logic has no caller gate, and the VM gate sees non-owner callers.
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwnerParser.Instance.IsOwner.Should().BeTrue("parser declares it requires owner gating at dispatch");

        Address nonOwner = ExampleOwnerA;
        context.ArbosState.ChainOwners.IsMember(nonOwner).Should().BeFalse("precondition: not a chain owner");

        ArbOwner.AddChainOwner(context.WithCaller(nonOwner), ExampleOwnerB);

        context.ArbosState.ChainOwners.IsMember(ExampleOwnerB).Should().BeTrue(
            "business logic has no caller check — authorization lives in VM dispatch layer");
    }

    [Test]
    public void SetNativeTokenManagementFrom_AtLeastSevenDaysInFuture_UpdatesEnabledTime()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);
        ulong enableTime = DefaultBlockTimestamp + ArbOwner.NativeTokenEnableDelay + 1;

        ArbOwner.SetNativeTokenManagementFrom(context, enableTime);

        context.ArbosState.NativeTokenEnabledTime.Get().Should().Be(enableTime);
    }

    [Test]
    public void SetNativeTokenManagementFrom_LessThanSevenDays_Throws()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);
        ulong tooSoon = DefaultBlockTimestamp + ArbOwner.NativeTokenEnableDelay - 1;

        Action act = () => ArbOwner.SetNativeTokenManagementFrom(context, tooSoon);

        act.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*at least 7 days in the future*");
    }

    [Test]
    public void SetNativeTokenManagementFrom_Zero_DisablesFeature()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);
        context.ArbosState.NativeTokenEnabledTime.Set(DefaultBlockTimestamp + ArbOwner.NativeTokenEnableDelay + 100);

        ArbOwner.SetNativeTokenManagementFrom(context, 0);

        context.ArbosState.NativeTokenEnabledTime.Get().Should().Be(0);
    }

    [Test]
    public void AddNativeTokenOwner_AfterEnabledTimeReached_AddsToOwnerSet()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);
        context.ArbosState.NativeTokenEnabledTime.Set(DefaultBlockTimestamp - 1);

        ArbOwner.AddNativeTokenOwner(context, ExampleOwnerA);

        context.ArbosState.NativeTokenOwners.IsMember(ExampleOwnerA).Should().BeTrue();
    }

    [Test]
    public void AddNativeTokenOwner_BeforeEnabledTime_Throws()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);
        context.ArbosState.NativeTokenEnabledTime.Set(DefaultBlockTimestamp + 1);

        Action act = () => ArbOwner.AddNativeTokenOwner(context, ExampleOwnerA);

        act.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*not enabled*");
    }

    [Test]
    public void RemoveNativeTokenOwner_ExistingOwner_RemovesFromOwnerSet()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);
        context.ArbosState.NativeTokenOwners.Add(ExampleOwnerA);

        ArbOwner.RemoveNativeTokenOwner(context, ExampleOwnerA);

        context.ArbosState.NativeTokenOwners.IsMember(ExampleOwnerA).Should().BeFalse();
    }

    [Test]
    public void RemoveNativeTokenOwner_NonExistent_Throws()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        Action act = () => ArbOwner.RemoveNativeTokenOwner(context, ExampleOwnerA);

        act.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*non native token owner*");
    }

    [Test]
    public void IsNativeTokenOwner_NotInOwnerSet_ReturnsFalse()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.IsNativeTokenOwner(context, ExampleOwnerA).Should().BeFalse();
    }

    [Test]
    public void IsNativeTokenOwner_InOwnerSet_ReturnsTrue()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);
        context.ArbosState.NativeTokenOwners.Add(ExampleOwnerA);

        ArbOwner.IsNativeTokenOwner(context, ExampleOwnerA).Should().BeTrue();
    }

    [Test]
    public void GetAllNativeTokenOwners_AfterMutations_ReturnsCorrectList()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);
        context.ArbosState.NativeTokenOwners.Add(ExampleOwnerA);
        context.ArbosState.NativeTokenOwners.Add(ExampleOwnerB);

        ArbOwner.GetAllNativeTokenOwners(context).Should().BeEquivalentTo(new[] { ExampleOwnerA, ExampleOwnerB });
    }

    [Test]
    public void SetL1BaseFeeEstimateInertia_Always_UpdatesInertiaStorage()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetL1BaseFeeEstimateInertia(context, 42);

        context.ArbosState.L1PricingState.InertiaStorage.Get().Should().Be(42);
    }

    [Test]
    public void SetL2BaseFee_Always_UpdatesBaseFeeStorage()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);
        UInt256 newBaseFee = 1_000_000_000; // 1 gwei

        ArbOwner.SetL2BaseFee(context, newBaseFee);

        context.ArbosState.L2PricingState.BaseFeeWeiStorage.Get().Should().Be(newBaseFee);
    }

    [Test]
    public void SetMinimumL2BaseFee_Always_UpdatesMinBaseFeeStorage()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);
        UInt256 newMin = 500_000_000;

        ArbOwner.SetMinimumL2BaseFee(context, newMin);

        context.ArbosState.L2PricingState.MinBaseFeeWeiStorage.Get().Should().Be(newMin);
    }

    [Test]
    public void SetSpeedLimit_NonZero_UpdatesSpeedLimitStorage()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetSpeedLimit(context, 123_456);

        context.ArbosState.L2PricingState.SpeedLimitPerSecondStorage.Get().Should().Be(123_456);
    }

    [Test]
    public void SetSpeedLimit_Zero_Throws()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        Action act = () => ArbOwner.SetSpeedLimit(context, 0);

        act.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*must be nonzero*");
    }

    [Test]
    public void SetMaxTxGasLimit_AtFiftyPlus_UpdatesPerTxLimit()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetMaxTxGasLimit(context, 20_000_000);

        context.ArbosState.L2PricingState.PerTxGasLimitStorage.Get().Should().Be(20_000_000);
    }

    [Test]
    public void SetMaxTxGasLimit_BelowFifty_UpdatesPerBlockLimit()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);
        context.WithArbosVersion(ArbosVersion.Forty);

        ArbOwner.SetMaxTxGasLimit(context, 40_000_000);

        context.ArbosState.L2PricingState.PerBlockGasLimitStorage.Get().Should().Be(40_000_000);
    }

    [Test]
    public void SetMaxBlockGasLimit_Always_UpdatesPerBlockLimit()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetMaxBlockGasLimit(context, 50_000_000);

        context.ArbosState.L2PricingState.PerBlockGasLimitStorage.Get().Should().Be(50_000_000);
    }

    [Test]
    public void SetL2GasPricingInertia_NonZero_UpdatesPricingInertiaStorage()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetL2GasPricingInertia(context, 77);

        context.ArbosState.L2PricingState.PricingInertiaStorage.Get().Should().Be(77);
    }

    [Test]
    public void SetL2GasPricingInertia_Zero_Throws()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        Action act = () => ArbOwner.SetL2GasPricingInertia(context, 0);

        act.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*must be nonzero*");
    }

    [Test]
    public void SetL2GasBacklogTolerance_Always_UpdatesBacklogToleranceStorage()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetL2GasBacklogTolerance(context, 15);

        context.ArbosState.L2PricingState.BacklogToleranceStorage.Get().Should().Be(15);
    }

    [Test]
    public void SetGasBacklog_Always_UpdatesGasBacklogStorage()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetGasBacklog(context, 9_999);

        context.ArbosState.L2PricingState.GasBacklogStorage.Get().Should().Be(9_999);
    }

    [Test]
    public void SetGasPricingConstraints_AtFiftyPlus_PersistsConstraints()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);
        ulong[][] constraints =
        {
            [1_000_000, 60, 5_000_000],
            [500_000, 120, 1_000_000],
        };

        ArbOwner.SetGasPricingConstraints(context, constraints);

        L2PricingState l2 = context.ArbosState.L2PricingState;
        l2.ConstraintsLength().Should().Be(2);

        GasConstraint first = l2.OpenConstraintAt(0);
        first.Target.Should().Be(1_000_000);
        first.AdjustmentWindow.Should().Be(60);
        first.Backlog.Should().Be(5_000_000);

        GasConstraint second = l2.OpenConstraintAt(1);
        second.Target.Should().Be(500_000);
        second.AdjustmentWindow.Should().Be(120);
        second.Backlog.Should().Be(1_000_000);
    }

    [Test]
    public void SetGasPricingConstraints_InvalidTarget_Throws()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);
        ulong[][] constraints = { [0, 60, 0] };

        Action act = () => ArbOwner.SetGasPricingConstraints(context, constraints);

        act.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*invalid constraint*");
    }

    [Test]
    public void SetGasPricingConstraints_ExceedsMaxCountAtFiftyOne_Throws()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);
        context.WithArbosVersion(ArbosVersion.FiftyOne);

        ulong[][] tooMany = new ulong[L2PricingState.GasConstraintsMaxNum + 1][];
        for (int i = 0; i < tooMany.Length; i++)
            tooMany[i] = [1_000_000, 60, 0];

        Action act = () => ArbOwner.SetGasPricingConstraints(context, tooMany);

        act.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*Too many constraints*");
    }

    [Test]
    public void GetNetworkFeeAccount_AfterSet_RoundTripsThroughStorage()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetNetworkFeeAccount(context, ExampleOwnerA);

        ArbOwner.GetNetworkFeeAccount(context).Should().Be(ExampleOwnerA);
    }

    [Test]
    public void GetInfraFeeAccount_AfterSet_RoundTripsThroughStorage()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetInfraFeeAccount(context, ExampleOwnerB);

        ArbOwner.GetInfraFeeAccount(context).Should().Be(ExampleOwnerB);
    }

    [Test]
    public void ScheduleArbOSUpgrade_Always_WritesVersionAndTimestamp()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.ScheduleArbOSUpgrade(context, ArbosVersion.FiftyOne, DefaultBlockTimestamp + 1_000);

        context.ArbosState.UpgradeVersion.Get().Should().Be(ArbosVersion.FiftyOne);
        context.ArbosState.UpgradeTimestamp.Get().Should().Be(DefaultBlockTimestamp + 1_000);
    }

    [Test]
    public void SetL1PricingEquilibrationUnits_Always_UpdatesEquilibrationUnitsStorage()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);
        UInt256 units = new(7_000_000_000);

        ArbOwner.SetL1PricingEquilibrationUnits(context, units);

        context.ArbosState.L1PricingState.EquilibrationUnitsStorage.Get().Should().Be(units);
    }

    [Test]
    public void SetL1PricingInertia_Always_UpdatesInertiaStorage()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetL1PricingInertia(context, 33);

        context.ArbosState.L1PricingState.InertiaStorage.Get().Should().Be(33);
    }

    [Test]
    public void SetL1PricingRewardRecipient_Always_UpdatesPayRewardsToStorage()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetL1PricingRewardRecipient(context, ExampleOwnerA);

        context.ArbosState.L1PricingState.PayRewardsToStorage.Get().Should().Be(ExampleOwnerA);
    }

    [Test]
    public void SetL1PricingRewardRate_Always_UpdatesPerUnitRewardStorage()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetL1PricingRewardRate(context, 2_500);

        context.ArbosState.L1PricingState.PerUnitRewardStorage.Get().Should().Be(2_500);
    }

    [Test]
    public void SetL1PricePerUnit_Always_UpdatesPricePerUnitStorage()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);
        UInt256 price = 12_345_678;

        ArbOwner.SetL1PricePerUnit(context, price);

        context.ArbosState.L1PricingState.PricePerUnitStorage.Get().Should().Be(price);
    }

    [Test]
    public void SetPerBatchGasCharge_Always_UpdatesPerBatchGasCostStorage()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetPerBatchGasCharge(context, 100_000);

        context.ArbosState.L1PricingState.PerBatchGasCostStorage.Get().Should().Be(100_000);
    }

    [Test]
    public void SetAmortizedCostCapBips_Always_UpdatesAmortizedCostCap()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetAmortizedCostCapBips(context, 500);

        context.ArbosState.L1PricingState.AmortizedCostCapBips().Should().Be(500);
    }

    [Test]
    public void SetParentGasFloorPerToken_Always_UpdatesGasFloorStorage()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetParentGasFloorPerToken(context, 10_000);

        context.ArbosState.L1PricingState.ParentGasFloorPerToken().Should().Be(10_000);
    }

    [Test]
    public void SetBrotliCompressionLevel_Always_UpdatesLevelStorage()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetBrotliCompressionLevel(context, 6);

        context.ArbosState.BrotliCompressionLevel.Get().Should().Be(6);
    }

    [Test]
    public void ReleaseL1PricerSurplusFunds_NoSurplus_ReturnsZero()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        // Genesis leaves pool balance == recognized fees, so no surplus is available.
        UInt256 result = ArbOwner.ReleaseL1PricerSurplusFunds(context, 1_000_000);

        result.Should().Be(UInt256.Zero);
    }

    [Test]
    public void ReleaseL1PricerSurplusFunds_WithSurplus_RecognizesFeesUpToCap()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState worldState);

        // Funds aren't moved on-chain — the pool balance stays; only recognized-fees accounting advances.
        UInt256 surplus = 1_000;
        worldState.CreateAccountIfNotExists(ArbosAddresses.L1PricerFundsPoolAddress, surplus);

        UInt256 cap = 400;
        UInt256 released = ArbOwner.ReleaseL1PricerSurplusFunds(context, cap);

        released.Should().Be(cap);
        context.ArbosState.L1PricingState.L1FeesAvailableStorage.Get().Should().Be(cap);
    }

    [Test]
    public void SetInkPrice_ValidValue_UpdatesInkPriceParam()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetInkPrice(context, 20_000);

        context.ArbosState.Programs.GetParams().InkPrice.Should().Be(20_000u);
    }

    [Test]
    public void SetInkPrice_Zero_Throws()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        Action act = () => ArbOwner.SetInkPrice(context, 0);

        act.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*ink price*");
    }

    [Test]
    public void SetInkPrice_AboveMax_Throws()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        Action act = () => ArbOwner.SetInkPrice(context, StylusParams.MaxInkPrice + 1);

        act.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*ink price*");
    }

    [Test]
    public void SetWasmMaxStackDepth_Always_UpdatesMaxStackDepthParam()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetWasmMaxStackDepth(context, 22_000);

        context.ArbosState.Programs.GetParams().MaxStackDepth.Should().Be(22_000u);
    }

    [Test]
    public void SetWasmFreePages_Always_UpdatesFreePagesParam()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetWasmFreePages(context, 4);

        context.ArbosState.Programs.GetParams().FreePages.Should().Be((ushort)4);
    }

    [Test]
    public void SetWasmPageGas_Always_UpdatesPageGasParam()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetWasmPageGas(context, 2_000);

        context.ArbosState.Programs.GetParams().PageGas.Should().Be((ushort)2_000);
    }

    [Test]
    public void SetWasmPageLimit_Always_UpdatesPageLimitParam()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetWasmPageLimit(context, 256);

        context.ArbosState.Programs.GetParams().PageLimit.Should().Be((ushort)256);
    }

    [Test]
    public void SetWasmMaxSize_Always_UpdatesMaxWasmSizeParam()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetWasmMaxSize(context, 256 * 1024);

        context.ArbosState.Programs.GetParams().MaxWasmSize.Should().Be(256u * 1024);
    }

    [Test]
    public void SetWasmMinInitGas_Always_UpdatesMinInitAndCachedGas()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        // Inputs convert via DivCeiling to unit counts: gas 256 / MinInitGasUnits(128) = 2;
        // cached 64 / MinCachedGasUnits(32) = 2.
        ArbOwner.SetWasmMinInitGas(context, 256, 64);

        StylusParams p = context.ArbosState.Programs.GetParams();
        p.MinInitGas.Should().Be((byte)2);
        p.MinCachedInitGas.Should().Be((byte)2);
    }

    [Test]
    public void SetWasmInitCostScalar_Always_UpdatesInitCostScalarParam()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        // DivCeiling(10, CostScalarPercent=2) = 5.
        ArbOwner.SetWasmInitCostScalar(context, 10);

        context.ArbosState.Programs.GetParams().InitCostScalar.Should().Be((byte)5);
    }

    [Test]
    public void SetWasmExpiryDays_Always_UpdatesExpiryDaysParam()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetWasmExpiryDays(context, 180);

        context.ArbosState.Programs.GetParams().ExpiryDays.Should().Be((ushort)180);
    }

    [Test]
    public void SetWasmKeepaliveDays_Always_UpdatesKeepaliveDaysParam()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetWasmKeepaliveDays(context, 14);

        context.ArbosState.Programs.GetParams().KeepaliveDays.Should().Be((ushort)14);
    }

    [Test]
    public void SetWasmBlockCacheSize_Always_UpdatesBlockCacheSizeParam()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetWasmBlockCacheSize(context, 64);

        context.ArbosState.Programs.GetParams().BlockCacheSize.Should().Be((ushort)64);
    }

    [Test]
    public void AddWasmCacheManager_Always_AddsToManagerSet()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.AddWasmCacheManager(context, ExampleOwnerA);

        context.ArbosState.Programs.CacheManagersStorage.IsMember(ExampleOwnerA).Should().BeTrue();
    }

    [Test]
    public void RemoveWasmCacheManager_ExistingManager_RemovesFromManagerSet()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);
        context.ArbosState.Programs.CacheManagersStorage.Add(ExampleOwnerA);

        ArbOwner.RemoveWasmCacheManager(context, ExampleOwnerA);

        context.ArbosState.Programs.CacheManagersStorage.IsMember(ExampleOwnerA).Should().BeFalse();
    }

    [Test]
    public void RemoveWasmCacheManager_NonExistent_Throws()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        Action act = () => ArbOwner.RemoveWasmCacheManager(context, ExampleOwnerA);

        act.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*non-manager*");
    }

    [Test]
    public void SetChainConfig_Always_UpdatesChainConfigStorage()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);
        byte[] payload = Encoding.UTF8.GetBytes("{\"chainId\":42}");

        ArbOwner.SetChainConfig(context, payload);

        context.ArbosState.ChainConfigStorage.Get().Should().BeEquivalentTo(payload);
    }

    [Test]
    public void SetCalldataPriceIncrease_EnableThenDisable_TogglesFeature()
    {
        using IDisposable scope = SetupContext(out PrecompileTestContextBuilder context, out IWorldState _);

        ArbOwner.SetCalldataPriceIncrease(context, true);
        context.ArbosState.Features.IsCalldataPriceIncreaseEnabled().Should().BeTrue();

        ArbOwner.SetCalldataPriceIncrease(context, false);
        context.ArbosState.Features.IsCalldataPriceIncreaseEnabled().Should().BeFalse();
    }

    [Test]
    public void OwnerActsEvent_BuildLogEntry_PinsTopicsAndDataLayout()
    {
        // Models the log built by OwnerLogic.EmitOwnerSuccessEvent after a successful owner call.
        // Event signature: OwnerActs(bytes4 indexed method, address indexed owner, bytes data).
        byte[] methodId = [0x48, 0x1f, 0x8d, 0xbf]; // addChainOwner(address)
        Address caller = ExampleOwnerA;
        byte[] calldata = [.. methodId, .. new byte[32]];

        LogEntry log = EventsEncoder.BuildLogEntryFromEvent(
            ArbOwner.OwnerActsEvent, ArbOwner.Address, methodId, caller, calldata);

        log.Address.Should().Be(ArbOwner.Address);
        log.Topics.Should().HaveCount(3);
        log.Topics[0].Should().Be(ExpectedOwnerActsTopic);
        log.Data.Should().NotBeEmpty("data topic carries the ABI-encoded calldata payload");
    }

    private static IDisposable SetupContext(out PrecompileTestContextBuilder context, out IWorldState worldState)
    {
        worldState = TestWorldStateFactory.CreateForTest();
        IDisposable worldStateScope = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        BlockHeader header = Build.A.BlockHeader.WithTimestamp(DefaultBlockTimestamp).TestObject;
        context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithBlockExecutionContext(header);

        return worldStateScope;
    }
}
