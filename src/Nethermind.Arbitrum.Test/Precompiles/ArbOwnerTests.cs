// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Buffers.Binary;
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
using Nethermind.Int256;
using Nethermind.Logging;
using Solgen = Nethermind.Arbitrum.Precompiles.Solgen;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public class ArbOwnerTests
{
    private static readonly Address ExampleOwnerA = new("0x0000000000000000000000000000000000000aaa");
    private static readonly Address ExampleOwnerB = new("0x0000000000000000000000000000000000000bbb");

    private static readonly Hash256 ExpectedOwnerActsTopic = new(Solgen.ArbOwner.Events.OwnerActs.Topic0Hex);

    [Test]
    public void Abi_WhenParsed_ContainsExpectedFunctionSignatures()
    {
        Dictionary<uint, ArbitrumFunctionDescription> allFunctions = PrecompileTestAbiHelpers.GetAllFunctionDescriptions(Solgen.ArbOwner.Abi);

        allFunctions.Keys.Should().BeEquivalentTo(new[]
        {
            PrecompileTestAbiHelpers.GetMethodId("addChainOwner(address)"),
            PrecompileTestAbiHelpers.GetMethodId("removeChainOwner(address)"),
            PrecompileTestAbiHelpers.GetMethodId("isChainOwner(address)"),
            PrecompileTestAbiHelpers.GetMethodId("getAllChainOwners()"),
            PrecompileTestAbiHelpers.GetMethodId("setNativeTokenManagementFrom(uint64)"),
            PrecompileTestAbiHelpers.GetMethodId("addNativeTokenOwner(address)"),
            PrecompileTestAbiHelpers.GetMethodId("removeNativeTokenOwner(address)"),
            PrecompileTestAbiHelpers.GetMethodId("isNativeTokenOwner(address)"),
            PrecompileTestAbiHelpers.GetMethodId("getAllNativeTokenOwners()"),
            PrecompileTestAbiHelpers.GetMethodId("setL1BaseFeeEstimateInertia(uint64)"),
            PrecompileTestAbiHelpers.GetMethodId("setL2BaseFee(uint256)"),
            PrecompileTestAbiHelpers.GetMethodId("setMinimumL2BaseFee(uint256)"),
            PrecompileTestAbiHelpers.GetMethodId("setSpeedLimit(uint64)"),
            PrecompileTestAbiHelpers.GetMethodId("setMaxTxGasLimit(uint64)"),
            PrecompileTestAbiHelpers.GetMethodId("setMaxBlockGasLimit(uint64)"),
            PrecompileTestAbiHelpers.GetMethodId("setL2GasPricingInertia(uint64)"),
            PrecompileTestAbiHelpers.GetMethodId("setL2GasBacklogTolerance(uint64)"),
            PrecompileTestAbiHelpers.GetMethodId("getNetworkFeeAccount()"),
            PrecompileTestAbiHelpers.GetMethodId("getInfraFeeAccount()"),
            PrecompileTestAbiHelpers.GetMethodId("setNetworkFeeAccount(address)"),
            PrecompileTestAbiHelpers.GetMethodId("setInfraFeeAccount(address)"),
            PrecompileTestAbiHelpers.GetMethodId("scheduleArbOSUpgrade(uint64,uint64)"),
            PrecompileTestAbiHelpers.GetMethodId("setL1PricingEquilibrationUnits(uint256)"),
            PrecompileTestAbiHelpers.GetMethodId("setL1PricingInertia(uint64)"),
            PrecompileTestAbiHelpers.GetMethodId("setL1PricingRewardRecipient(address)"),
            PrecompileTestAbiHelpers.GetMethodId("setL1PricingRewardRate(uint64)"),
            PrecompileTestAbiHelpers.GetMethodId("setL1PricePerUnit(uint256)"),
            PrecompileTestAbiHelpers.GetMethodId("setPerBatchGasCharge(int64)"),
            PrecompileTestAbiHelpers.GetMethodId("setBrotliCompressionLevel(uint64)"),
            PrecompileTestAbiHelpers.GetMethodId("setAmortizedCostCapBips(uint64)"),
            PrecompileTestAbiHelpers.GetMethodId("releaseL1PricerSurplusFunds(uint256)"),
            PrecompileTestAbiHelpers.GetMethodId("setInkPrice(uint32)"),
            PrecompileTestAbiHelpers.GetMethodId("setWasmMaxStackDepth(uint32)"),
            PrecompileTestAbiHelpers.GetMethodId("setWasmFreePages(uint16)"),
            PrecompileTestAbiHelpers.GetMethodId("setWasmPageGas(uint16)"),
            PrecompileTestAbiHelpers.GetMethodId("setWasmPageLimit(uint16)"),
            PrecompileTestAbiHelpers.GetMethodId("setWasmMaxSize(uint32)"),
            PrecompileTestAbiHelpers.GetMethodId("setWasmMinInitGas(uint8,uint16)"),
            PrecompileTestAbiHelpers.GetMethodId("setWasmInitCostScalar(uint64)"),
            PrecompileTestAbiHelpers.GetMethodId("setWasmExpiryDays(uint16)"),
            PrecompileTestAbiHelpers.GetMethodId("setWasmKeepaliveDays(uint16)"),
            PrecompileTestAbiHelpers.GetMethodId("setWasmBlockCacheSize(uint16)"),
            PrecompileTestAbiHelpers.GetMethodId("addWasmCacheManager(address)"),
            PrecompileTestAbiHelpers.GetMethodId("removeWasmCacheManager(address)"),
            PrecompileTestAbiHelpers.GetMethodId("setChainConfig(string)"),
            PrecompileTestAbiHelpers.GetMethodId("setCalldataPriceIncrease(bool)"),
            PrecompileTestAbiHelpers.GetMethodId("setParentGasFloorPerToken(uint64)"),
            PrecompileTestAbiHelpers.GetMethodId("setGasBacklog(uint64)"),
            PrecompileTestAbiHelpers.GetMethodId("setGasPricingConstraints(uint64[3][])"),
            PrecompileTestAbiHelpers.GetMethodId("addTransactionFilterer(address)"),
            PrecompileTestAbiHelpers.GetMethodId("removeTransactionFilterer(address)"),
            PrecompileTestAbiHelpers.GetMethodId("isTransactionFilterer(address)"),
            PrecompileTestAbiHelpers.GetMethodId("getAllTransactionFilterers()"),
            PrecompileTestAbiHelpers.GetMethodId("setFilteredFundsRecipient(address)"),
            PrecompileTestAbiHelpers.GetMethodId("getFilteredFundsRecipient()"),
            PrecompileTestAbiHelpers.GetMethodId("setTransactionFilteringFrom(uint64)"),
            PrecompileTestAbiHelpers.GetMethodId("setCollectTips(bool)"),
            PrecompileTestAbiHelpers.GetMethodId("setMaxStylusContractFragments(uint8)"),
            // `(()[])` mirrors the test parser: AbiTypeConverter returns ()[] for JSON tuple[].
            // (canonical signature is setMultiGasPricingConstraints(((uint8,uint64)[],uint32,uint64,uint64)[])).
            // See Abi_SetMultiGasPricingConstraints_ContainsExpectedFunctionSignatures for the additional verification
            PrecompileTestAbiHelpers.GetMethodId("setMultiGasPricingConstraints(()[])"),
            PrecompileTestAbiHelpers.GetMethodId("setWasmActivationGas(uint64)"),
        });
    }

    // The test is sanity check for the complex signature of
    //   function setMultiGasPricingConstraints(
    //       ((uint8,uint64)[],uint32,uint64,uint64)[] constraints
    //   ) returns ();
    //
    // Unfolded from ArbOwner.g.cs Functions entry 0x2b05bb39:
    //   AbiType.Array(AbiType.Tuple(
    //       AbiType.Array(AbiType.Tuple(AbiType.UInt(8), AbiType.UInt(64))),
    //       AbiType.UInt(32), AbiType.UInt(64), AbiType.UInt(64)))
    [Test]
    public void Abi_SetMultiGasPricingConstraints_ContainsExpectedFunctionSignatures()
    {
        AbiType resourceConstraint = new AbiTuple(
            new AbiArray(new AbiTuple(AbiUInt.UInt8, AbiUInt.UInt64)),
            AbiUInt.UInt32,
            AbiUInt.UInt64,
            AbiUInt.UInt64);

        AbiSignature signature = new("setMultiGasPricingConstraints", new AbiArray(resourceConstraint));

        uint canonicalMethodId = BinaryPrimitives.ReadUInt32BigEndian(signature.Address);
        uint placeholderMethodId = PrecompileTestAbiHelpers.GetMethodId("setMultiGasPricingConstraints(()[])");

        // Sanity-check the canonical signature string that Nethermind's ABI primitives produce.
        signature.ToString().Should().Be("setMultiGasPricingConstraints(((uint8,uint64)[],uint32,uint64,uint64)[])");

        // The Nethermind-ABI-derived selector must equal the package's authoritative constant.
        canonicalMethodId.Should().Be(Solgen.ArbOwner.Methods.SetMultiGasPricingConstraints);

        // The "(()[])" placeholder is self-consistent with PrecompileTestAbiHelpers.GetAllFunctionDescriptions
        placeholderMethodId.Should().NotBe(canonicalMethodId);
    }

    [Test]
    public void Abi_WhenParsed_ContainsExpectedEvents()
    {
        Dictionary<string, AbiEventDescription> allEvents = PrecompileTestAbiHelpers.GetAllEventDescriptions(Solgen.ArbOwner.Abi);

        allEvents.Keys.Should().BeEquivalentTo(
            "OwnerActs",
            "ChainOwnerAdded",
            "ChainOwnerRemoved",
            "NativeTokenOwnerAdded",
            "NativeTokenOwnerRemoved",
            "TransactionFiltererAdded",
            "TransactionFiltererRemoved",
            "FilteredFundsRecipientSet");
    }

    [Test]
    public void Abi_WhenParsed_ContainsNoErrors()
    {
        PrecompileTestAbiHelpers.GetAllErrorDescriptions(Solgen.ArbOwner.Abi).Should().BeEmpty();
    }

    [Test]
    public void MethodIds_OwnershipManagement_MatchExpectedSelectors()
    {
        PrecompileTestAbiHelpers.GetMethodId("addChainOwner(address)").Should().Be(Solgen.ArbOwner.Methods.AddChainOwner);
        PrecompileTestAbiHelpers.GetMethodId("removeChainOwner(address)").Should().Be(Solgen.ArbOwner.Methods.RemoveChainOwner);
        PrecompileTestAbiHelpers.GetMethodId("isChainOwner(address)").Should().Be(Solgen.ArbOwner.Methods.IsChainOwner);
        PrecompileTestAbiHelpers.GetMethodId("getAllChainOwners()").Should().Be(Solgen.ArbOwner.Methods.GetAllChainOwners);
        PrecompileTestAbiHelpers.GetMethodId("setNativeTokenManagementFrom(uint64)").Should().Be(Solgen.ArbOwner.Methods.SetNativeTokenManagementFrom);
        PrecompileTestAbiHelpers.GetMethodId("addNativeTokenOwner(address)").Should().Be(Solgen.ArbOwner.Methods.AddNativeTokenOwner);
        PrecompileTestAbiHelpers.GetMethodId("removeNativeTokenOwner(address)").Should().Be(Solgen.ArbOwner.Methods.RemoveNativeTokenOwner);
        PrecompileTestAbiHelpers.GetMethodId("isNativeTokenOwner(address)").Should().Be(Solgen.ArbOwner.Methods.IsNativeTokenOwner);
        PrecompileTestAbiHelpers.GetMethodId("getAllNativeTokenOwners()").Should().Be(Solgen.ArbOwner.Methods.GetAllNativeTokenOwners);
    }

    [Test]
    public void MethodIds_GasAndFeeParameters_MatchExpectedSelectors()
    {
        PrecompileTestAbiHelpers.GetMethodId("setL1BaseFeeEstimateInertia(uint64)").Should().Be(Solgen.ArbOwner.Methods.SetL1BaseFeeEstimateInertia);
        PrecompileTestAbiHelpers.GetMethodId("setL2BaseFee(uint256)").Should().Be(Solgen.ArbOwner.Methods.SetL2BaseFee);
        PrecompileTestAbiHelpers.GetMethodId("setMinimumL2BaseFee(uint256)").Should().Be(Solgen.ArbOwner.Methods.SetMinimumL2BaseFee);
        PrecompileTestAbiHelpers.GetMethodId("setSpeedLimit(uint64)").Should().Be(Solgen.ArbOwner.Methods.SetSpeedLimit);
        PrecompileTestAbiHelpers.GetMethodId("setMaxTxGasLimit(uint64)").Should().Be(Solgen.ArbOwner.Methods.SetMaxTxGasLimit);
        PrecompileTestAbiHelpers.GetMethodId("setMaxBlockGasLimit(uint64)").Should().Be(Solgen.ArbOwner.Methods.SetMaxBlockGasLimit);
        PrecompileTestAbiHelpers.GetMethodId("setL2GasPricingInertia(uint64)").Should().Be(Solgen.ArbOwner.Methods.SetL2GasPricingInertia);
        PrecompileTestAbiHelpers.GetMethodId("setL2GasBacklogTolerance(uint64)").Should().Be(Solgen.ArbOwner.Methods.SetL2GasBacklogTolerance);
        PrecompileTestAbiHelpers.GetMethodId("setL1PricingEquilibrationUnits(uint256)").Should().Be(Solgen.ArbOwner.Methods.SetL1PricingEquilibrationUnits);
        PrecompileTestAbiHelpers.GetMethodId("setL1PricingInertia(uint64)").Should().Be(Solgen.ArbOwner.Methods.SetL1PricingInertia);
        PrecompileTestAbiHelpers.GetMethodId("setL1PricingRewardRecipient(address)").Should().Be(Solgen.ArbOwner.Methods.SetL1PricingRewardRecipient);
        PrecompileTestAbiHelpers.GetMethodId("setL1PricingRewardRate(uint64)").Should().Be(Solgen.ArbOwner.Methods.SetL1PricingRewardRate);
        PrecompileTestAbiHelpers.GetMethodId("setL1PricePerUnit(uint256)").Should().Be(Solgen.ArbOwner.Methods.SetL1PricePerUnit);
        PrecompileTestAbiHelpers.GetMethodId("setPerBatchGasCharge(int64)").Should().Be(Solgen.ArbOwner.Methods.SetPerBatchGasCharge);
        PrecompileTestAbiHelpers.GetMethodId("setBrotliCompressionLevel(uint64)").Should().Be(Solgen.ArbOwner.Methods.SetBrotliCompressionLevel);
        PrecompileTestAbiHelpers.GetMethodId("setAmortizedCostCapBips(uint64)").Should().Be(Solgen.ArbOwner.Methods.SetAmortizedCostCapBips);
        PrecompileTestAbiHelpers.GetMethodId("releaseL1PricerSurplusFunds(uint256)").Should().Be(Solgen.ArbOwner.Methods.ReleaseL1PricerSurplusFunds);
        PrecompileTestAbiHelpers.GetMethodId("setGasBacklog(uint64)").Should().Be(Solgen.ArbOwner.Methods.SetGasBacklog);
        PrecompileTestAbiHelpers.GetMethodId("setGasPricingConstraints(uint64[3][])").Should().Be(Solgen.ArbOwner.Methods.SetGasPricingConstraints);
    }

    [Test]
    public void MethodIds_InfrastructureAccounts_MatchExpectedSelectors()
    {
        PrecompileTestAbiHelpers.GetMethodId("getNetworkFeeAccount()").Should().Be(Solgen.ArbOwner.Methods.GetNetworkFeeAccount);
        PrecompileTestAbiHelpers.GetMethodId("getInfraFeeAccount()").Should().Be(Solgen.ArbOwner.Methods.GetInfraFeeAccount);
        PrecompileTestAbiHelpers.GetMethodId("setNetworkFeeAccount(address)").Should().Be(Solgen.ArbOwner.Methods.SetNetworkFeeAccount);
        PrecompileTestAbiHelpers.GetMethodId("setInfraFeeAccount(address)").Should().Be(Solgen.ArbOwner.Methods.SetInfraFeeAccount);
        PrecompileTestAbiHelpers.GetMethodId("scheduleArbOSUpgrade(uint64,uint64)").Should().Be(Solgen.ArbOwner.Methods.ScheduleArbOSUpgrade);
        PrecompileTestAbiHelpers.GetMethodId("setChainConfig(string)").Should().Be(Solgen.ArbOwner.Methods.SetChainConfig);
        PrecompileTestAbiHelpers.GetMethodId("setCalldataPriceIncrease(bool)").Should().Be(Solgen.ArbOwner.Methods.SetCalldataPriceIncrease);
        PrecompileTestAbiHelpers.GetMethodId("setParentGasFloorPerToken(uint64)").Should().Be(Solgen.ArbOwner.Methods.SetParentGasFloorPerToken);
    }

    [Test]
    public void MethodIds_StylusAndWasm_MatchExpectedSelectors()
    {
        PrecompileTestAbiHelpers.GetMethodId("setInkPrice(uint32)").Should().Be(Solgen.ArbOwner.Methods.SetInkPrice);
        PrecompileTestAbiHelpers.GetMethodId("setWasmMaxStackDepth(uint32)").Should().Be(Solgen.ArbOwner.Methods.SetWasmMaxStackDepth);
        PrecompileTestAbiHelpers.GetMethodId("setWasmFreePages(uint16)").Should().Be(Solgen.ArbOwner.Methods.SetWasmFreePages);
        PrecompileTestAbiHelpers.GetMethodId("setWasmPageGas(uint16)").Should().Be(Solgen.ArbOwner.Methods.SetWasmPageGas);
        PrecompileTestAbiHelpers.GetMethodId("setWasmPageLimit(uint16)").Should().Be(Solgen.ArbOwner.Methods.SetWasmPageLimit);
        PrecompileTestAbiHelpers.GetMethodId("setWasmMaxSize(uint32)").Should().Be(Solgen.ArbOwner.Methods.SetWasmMaxSize);
        PrecompileTestAbiHelpers.GetMethodId("setWasmMinInitGas(uint8,uint16)").Should().Be(Solgen.ArbOwner.Methods.SetWasmMinInitGas);
        PrecompileTestAbiHelpers.GetMethodId("setWasmInitCostScalar(uint64)").Should().Be(Solgen.ArbOwner.Methods.SetWasmInitCostScalar);
        PrecompileTestAbiHelpers.GetMethodId("setWasmExpiryDays(uint16)").Should().Be(Solgen.ArbOwner.Methods.SetWasmExpiryDays);
        PrecompileTestAbiHelpers.GetMethodId("setWasmKeepaliveDays(uint16)").Should().Be(Solgen.ArbOwner.Methods.SetWasmKeepaliveDays);
        PrecompileTestAbiHelpers.GetMethodId("setWasmBlockCacheSize(uint16)").Should().Be(Solgen.ArbOwner.Methods.SetWasmBlockCacheSize);
        PrecompileTestAbiHelpers.GetMethodId("addWasmCacheManager(address)").Should().Be(Solgen.ArbOwner.Methods.AddWasmCacheManager);
        PrecompileTestAbiHelpers.GetMethodId("removeWasmCacheManager(address)").Should().Be(Solgen.ArbOwner.Methods.RemoveWasmCacheManager);
    }

    [Test]
    public void EventTopics_AllEvents_MatchExpectedHashes()
    {
        ArbOwner.OwnerActsEvent.GetHash().Should().Be(ExpectedOwnerActsTopic);
    }

    [Test]
    public void SetGasPricingConstraints_BelowFiftyArbOSVersion_IsRejected()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        context.WithArbosVersion(ArbosVersion.Fifty - 1);

        bool result = ArbOwnerParser.Instance.TryCheckMethodVisibility(context, NullLogger.Instance,
            PrecompileTestAbiHelpers.GetMethodId("setGasPricingConstraints(uint64[3][])"), out bool shouldRevert, out _);

        result.Should().BeFalse();
        shouldRevert.Should().BeTrue();
    }

    [Test]
    public void SetGasPricingConstraints_AtFiftyArbOSVersion_IsDispatched()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        context = context.WithExecutingAccount(ArbOwnerParser.Address);

        bool result = ArbOwnerParser.Instance.TryCheckMethodVisibility(context, NullLogger.Instance,
            PrecompileTestAbiHelpers.GetMethodId("setGasPricingConstraints(uint64[3][])"), out bool _, out PrecompileHandler? handler);

        result.Should().BeTrue();
        handler.Should().NotBeNull();
    }

    [Test]
    public void AddChainOwner_NewAddress_AddsToOwnerSet()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.AddChainOwner(context, ExampleOwnerA);

        context.ArbosState.ChainOwners.IsMember(ExampleOwnerA).Should().BeTrue();
    }

    [Test]
    public void RemoveChainOwner_ExistingOwner_RemovesFromOwnerSet()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        context.ArbosState.ChainOwners.Add(ExampleOwnerA);

        ArbOwner.RemoveChainOwner(context, ExampleOwnerA);

        context.ArbosState.ChainOwners.IsMember(ExampleOwnerA).Should().BeFalse();
    }

    [Test]
    public void RemoveChainOwner_NonExistentOwner_Throws()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        Action act = () => ArbOwner.RemoveChainOwner(context, ExampleOwnerA);

        act.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*non-owner*");
    }

    [Test]
    public void IsChainOwner_NotInOwnerSet_ReturnsFalse()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.IsChainOwner(context, ExampleOwnerA).Should().BeFalse();
    }

    [Test]
    public void IsChainOwner_InOwnerSet_ReturnsTrue()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        context.ArbosState.ChainOwners.Add(ExampleOwnerA);

        ArbOwner.IsChainOwner(context, ExampleOwnerA).Should().BeTrue();
    }

    [Test]
    public void GetAllChainOwners_AfterMutations_ReflectsAddAndRemove()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

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
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

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
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        ulong enableTime = context.BlockExecutionContext.Header.Timestamp + ArbOwner.NativeTokenEnableDelay + 1;

        ArbOwner.SetNativeTokenManagementFrom(context, enableTime);

        context.ArbosState.NativeTokenEnabledTime.Get().Should().Be(enableTime);
    }

    [Test]
    public void SetNativeTokenManagementFrom_LessThanSevenDays_Throws()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        ulong tooSoon = context.BlockExecutionContext.Header.Timestamp + ArbOwner.NativeTokenEnableDelay - 1;

        Action act = () => ArbOwner.SetNativeTokenManagementFrom(context, tooSoon);

        act.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*at least 7 days in the future*");
    }

    [Test]
    public void SetNativeTokenManagementFrom_Zero_DisablesFeature()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        context.ArbosState.NativeTokenEnabledTime.Set(context.BlockExecutionContext.Header.Timestamp + ArbOwner.NativeTokenEnableDelay + 100);

        ArbOwner.SetNativeTokenManagementFrom(context, 0);

        context.ArbosState.NativeTokenEnabledTime.Get().Should().Be(0);
    }

    [Test]
    public void AddNativeTokenOwner_AfterEnabledTimeReached_AddsToOwnerSet()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        context.ArbosState.NativeTokenEnabledTime.Set(context.BlockExecutionContext.Header.Timestamp - 1);

        ArbOwner.AddNativeTokenOwner(context, ExampleOwnerA);

        context.ArbosState.NativeTokenOwners.IsMember(ExampleOwnerA).Should().BeTrue();
    }

    [Test]
    public void AddNativeTokenOwner_BeforeEnabledTime_Throws()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        context.ArbosState.NativeTokenEnabledTime.Set(context.BlockExecutionContext.Header.Timestamp + 1);

        Action act = () => ArbOwner.AddNativeTokenOwner(context, ExampleOwnerA);

        act.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*not enabled*");
    }

    [Test]
    public void RemoveNativeTokenOwner_ExistingOwner_RemovesFromOwnerSet()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        context.ArbosState.NativeTokenOwners.Add(ExampleOwnerA);

        ArbOwner.RemoveNativeTokenOwner(context, ExampleOwnerA);

        context.ArbosState.NativeTokenOwners.IsMember(ExampleOwnerA).Should().BeFalse();
    }

    [Test]
    public void RemoveNativeTokenOwner_NonExistent_Throws()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        Action act = () => ArbOwner.RemoveNativeTokenOwner(context, ExampleOwnerA);

        act.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*non native token owner*");
    }

    [Test]
    public void IsNativeTokenOwner_NotInOwnerSet_ReturnsFalse()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.IsNativeTokenOwner(context, ExampleOwnerA).Should().BeFalse();
    }

    [Test]
    public void IsNativeTokenOwner_InOwnerSet_ReturnsTrue()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        context.ArbosState.NativeTokenOwners.Add(ExampleOwnerA);

        ArbOwner.IsNativeTokenOwner(context, ExampleOwnerA).Should().BeTrue();
    }

    [Test]
    public void GetAllNativeTokenOwners_AfterMutations_ReturnsCorrectList()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        context.ArbosState.NativeTokenOwners.Add(ExampleOwnerA);
        context.ArbosState.NativeTokenOwners.Add(ExampleOwnerB);

        ArbOwner.GetAllNativeTokenOwners(context).Should().BeEquivalentTo(new[] { ExampleOwnerA, ExampleOwnerB });
    }

    [Test]
    public void SetL1BaseFeeEstimateInertia_Always_UpdatesInertiaStorage()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetL1BaseFeeEstimateInertia(context, 42);

        context.ArbosState.L1PricingState.InertiaStorage.Get().Should().Be(42);
    }

    [Test]
    public void SetL2BaseFee_Always_UpdatesBaseFeeStorage()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        UInt256 newBaseFee = 1_000_000_000; // 1 gwei

        ArbOwner.SetL2BaseFee(context, newBaseFee);

        context.ArbosState.L2PricingState.BaseFeeWeiStorage.Get().Should().Be(newBaseFee);
    }

    [Test]
    public void SetMinimumL2BaseFee_Always_UpdatesMinBaseFeeStorage()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        UInt256 newMin = 500_000_000;

        ArbOwner.SetMinimumL2BaseFee(context, newMin);

        context.ArbosState.L2PricingState.MinBaseFeeWeiStorage.Get().Should().Be(newMin);
    }

    [Test]
    public void SetSpeedLimit_NonZero_UpdatesSpeedLimitStorage()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetSpeedLimit(context, 123_456);

        context.ArbosState.L2PricingState.SpeedLimitPerSecondStorage.Get().Should().Be(123_456);
    }

    [Test]
    public void SetSpeedLimit_Zero_Throws()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        Action act = () => ArbOwner.SetSpeedLimit(context, 0);

        act.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*must be nonzero*");
    }

    [Test]
    public void SetMaxTxGasLimit_AtFiftyPlus_UpdatesPerTxLimit()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetMaxTxGasLimit(context, 20_000_000);

        context.ArbosState.L2PricingState.PerTxGasLimitStorage.Get().Should().Be(20_000_000);
    }

    [Test]
    public void SetMaxTxGasLimit_BelowFifty_UpdatesPerBlockLimit()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        context.WithArbosVersion(ArbosVersion.Forty);

        ArbOwner.SetMaxTxGasLimit(context, 40_000_000);

        context.ArbosState.L2PricingState.PerBlockGasLimitStorage.Get().Should().Be(40_000_000);
    }

    [Test]
    public void SetMaxBlockGasLimit_Always_UpdatesPerBlockLimit()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetMaxBlockGasLimit(context, 50_000_000);

        context.ArbosState.L2PricingState.PerBlockGasLimitStorage.Get().Should().Be(50_000_000);
    }

    [Test]
    public void SetL2GasPricingInertia_NonZero_UpdatesPricingInertiaStorage()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetL2GasPricingInertia(context, 77);

        context.ArbosState.L2PricingState.PricingInertiaStorage.Get().Should().Be(77);
    }

    [Test]
    public void SetL2GasPricingInertia_Zero_Throws()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        Action act = () => ArbOwner.SetL2GasPricingInertia(context, 0);

        act.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*must be nonzero*");
    }

    [Test]
    public void SetL2GasBacklogTolerance_Always_UpdatesBacklogToleranceStorage()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetL2GasBacklogTolerance(context, 15);

        context.ArbosState.L2PricingState.BacklogToleranceStorage.Get().Should().Be(15);
    }

    [Test]
    public void SetGasBacklog_Always_UpdatesGasBacklogStorage()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetGasBacklog(context, 9_999);

        context.ArbosState.L2PricingState.GasBacklogStorage.Get().Should().Be(9_999);
    }

    [Test]
    public void SetGasPricingConstraints_AtFiftyPlus_PersistsConstraints()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        ulong[][] constraints =
        [
            [1_000_000, 60, 5_000_000],
            [500_000, 120, 1_000_000]
        ];

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
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        ulong[][] constraints = [[0, 60, 0]];

        Action act = () => ArbOwner.SetGasPricingConstraints(context, constraints);

        act.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*invalid constraint*");
    }

    [Test]
    public void SetGasPricingConstraints_ExceedsMaxCountAtFiftyOne_Throws()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
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
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetNetworkFeeAccount(context, ExampleOwnerA);

        ArbOwner.GetNetworkFeeAccount(context).Should().Be(ExampleOwnerA);
    }

    [Test]
    public void GetInfraFeeAccount_AfterSet_RoundTripsThroughStorage()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetInfraFeeAccount(context, ExampleOwnerB);

        ArbOwner.GetInfraFeeAccount(context).Should().Be(ExampleOwnerB);
    }

    [Test]
    public void ScheduleArbOSUpgrade_Always_WritesVersionAndTimestamp()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.ScheduleArbOSUpgrade(context, ArbosVersion.FiftyOne, context.BlockExecutionContext.Header.Timestamp + 1_000);

        context.ArbosState.UpgradeVersion.Get().Should().Be(ArbosVersion.FiftyOne);
        context.ArbosState.UpgradeTimestamp.Get().Should().Be(context.BlockExecutionContext.Header.Timestamp + 1_000);
    }

    [Test]
    public void SetL1PricingEquilibrationUnits_Always_UpdatesEquilibrationUnitsStorage()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        UInt256 units = new(7_000_000_000);

        ArbOwner.SetL1PricingEquilibrationUnits(context, units);

        context.ArbosState.L1PricingState.EquilibrationUnitsStorage.Get().Should().Be(units);
    }

    [Test]
    public void SetL1PricingInertia_Always_UpdatesInertiaStorage()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetL1PricingInertia(context, 33);

        context.ArbosState.L1PricingState.InertiaStorage.Get().Should().Be(33);
    }

    [Test]
    public void SetL1PricingRewardRecipient_Always_UpdatesPayRewardsToStorage()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetL1PricingRewardRecipient(context, ExampleOwnerA);

        context.ArbosState.L1PricingState.PayRewardsToStorage.Get().Should().Be(ExampleOwnerA);
    }

    [Test]
    public void SetL1PricingRewardRate_Always_UpdatesPerUnitRewardStorage()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetL1PricingRewardRate(context, 2_500);

        context.ArbosState.L1PricingState.PerUnitRewardStorage.Get().Should().Be(2_500);
    }

    [Test]
    public void SetL1PricePerUnit_Always_UpdatesPricePerUnitStorage()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        UInt256 price = 12_345_678;

        ArbOwner.SetL1PricePerUnit(context, price);

        context.ArbosState.L1PricingState.PricePerUnitStorage.Get().Should().Be(price);
    }

    [Test]
    public void SetPerBatchGasCharge_Always_UpdatesPerBatchGasCostStorage()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetPerBatchGasCharge(context, 100_000);

        context.ArbosState.L1PricingState.PerBatchGasCostStorage.Get().Should().Be(100_000);
    }

    [Test]
    public void SetAmortizedCostCapBips_Always_UpdatesAmortizedCostCap()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetAmortizedCostCapBips(context, 500);

        context.ArbosState.L1PricingState.AmortizedCostCapBips().Should().Be(500);
    }

    [Test]
    public void SetParentGasFloorPerToken_Always_UpdatesGasFloorStorage()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetParentGasFloorPerToken(context, 10_000);

        context.ArbosState.L1PricingState.ParentGasFloorPerToken().Should().Be(10_000);
    }

    [Test]
    public void SetBrotliCompressionLevel_Always_UpdatesLevelStorage()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetBrotliCompressionLevel(context, 6);

        context.ArbosState.BrotliCompressionLevel.Get().Should().Be(6);
    }

    [Test]
    public void ReleaseL1PricerSurplusFunds_NoSurplus_ReturnsZero()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        // Genesis leaves pool balance == recognized fees, so no surplus is available.
        UInt256 result = ArbOwner.ReleaseL1PricerSurplusFunds(context, 1_000_000);

        result.Should().Be(UInt256.Zero);
    }

    [Test]
    public void ReleaseL1PricerSurplusFunds_WithSurplus_RecognizesFeesUpToCap()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        // Funds aren't moved on-chain — the pool balance stays; only recognized-fees accounting advances.
        UInt256 surplus = 1_000;
        context.WorldState.CreateAccountIfNotExists(ArbosAddresses.L1PricerFundsPoolAddress, surplus);

        UInt256 cap = 400;
        UInt256 released = ArbOwner.ReleaseL1PricerSurplusFunds(context, cap);

        released.Should().Be(cap);
        context.ArbosState.L1PricingState.L1FeesAvailableStorage.Get().Should().Be(cap);
    }

    [Test]
    public void SetInkPrice_ValidValue_UpdatesInkPriceParam()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetInkPrice(context, 20_000);

        context.ArbosState.Programs.GetParams().InkPrice.Should().Be(20_000u);
    }

    [Test]
    public void SetInkPrice_Zero_Throws()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        Action act = () => ArbOwner.SetInkPrice(context, 0);

        act.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*ink price*");
    }

    [Test]
    public void SetInkPrice_AboveMax_Throws()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        Action act = () => ArbOwner.SetInkPrice(context, StylusParams.MaxInkPrice + 1);

        act.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*ink price*");
    }

    [Test]
    public void SetWasmMaxStackDepth_Always_UpdatesMaxStackDepthParam()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetWasmMaxStackDepth(context, 22_000);

        context.ArbosState.Programs.GetParams().MaxStackDepth.Should().Be(22_000u);
    }

    [Test]
    public void SetWasmFreePages_Always_UpdatesFreePagesParam()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetWasmFreePages(context, 4);

        context.ArbosState.Programs.GetParams().FreePages.Should().Be(4);
    }

    [Test]
    public void SetWasmPageGas_Always_UpdatesPageGasParam()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetWasmPageGas(context, 2_000);

        context.ArbosState.Programs.GetParams().PageGas.Should().Be(2_000);
    }

    [Test]
    public void SetWasmPageLimit_Always_UpdatesPageLimitParam()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetWasmPageLimit(context, 256);

        context.ArbosState.Programs.GetParams().PageLimit.Should().Be(256);
    }

    [Test]
    public void SetWasmMaxSize_Always_UpdatesMaxWasmSizeParam()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetWasmMaxSize(context, 256 * 1024);

        context.ArbosState.Programs.GetParams().MaxWasmSize.Should().Be(256u * 1024);
    }

    [Test]
    public void SetMaxStylusContractFragments_AtSixty_UpdatesMaxFragmentCountParam()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context, arbosVersion: ArbosVersion.Sixty);

        ArbOwner.SetMaxStylusContractFragments(context, 7);

        context.ArbosState.Programs.GetParams().MaxFragmentCount.Should().Be(7);
    }

    [TestCase((byte)0)]
    [TestCase((byte)1)]
    [TestCase(byte.MaxValue)]
    public void SetMaxStylusContractFragments_BoundaryValueAtSixty_RoundTrips(byte value)
    {
        // Nitro accepts the full uint8 range; activation enforces len(fragments) in [1, MaxFragmentCount] elsewhere.
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context, arbosVersion: ArbosVersion.Sixty);

        ArbOwner.SetMaxStylusContractFragments(context, value);

        context.ArbosState.Programs.GetParams().MaxFragmentCount.Should().Be(value);
    }

    [Test]
    public void SetMaxStylusContractFragments_BelowSixtyArbOSVersion_IsRejected()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        context.WithArbosVersion(ArbosVersion.Sixty - 1);

        bool result = ArbOwnerParser.Instance.TryCheckMethodVisibility(context, NullLogger.Instance,
            Solgen.ArbOwner.Methods.SetMaxStylusContractFragments, out bool shouldRevert, out _);

        result.Should().BeFalse();
        shouldRevert.Should().BeTrue();
    }

    [Test]
    public void SetMaxStylusContractFragments_AtSixtyArbOSVersion_IsDispatched()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context, arbosVersion: ArbosVersion.Sixty);
        context = context.WithExecutingAccount(ArbOwnerParser.Address);

        bool result = ArbOwnerParser.Instance.TryCheckMethodVisibility(context, NullLogger.Instance,
            Solgen.ArbOwner.Methods.SetMaxStylusContractFragments, out bool _, out PrecompileHandler? handler);

        result.Should().BeTrue();
        handler.Should().NotBeNull();
    }

    [Test]
    public void SetWasmMinInitGas_Always_UpdatesMinInitAndCachedGas()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        // Inputs convert via DivCeiling to unit counts: gas 256 / MinInitGasUnits(128) = 2;
        // cached 64 / MinCachedGasUnits(32) = 2.
        ArbOwner.SetWasmMinInitGas(context, 256, 64);

        StylusParams p = context.ArbosState.Programs.GetParams();
        p.MinInitGas.Should().Be(2);
        p.MinCachedInitGas.Should().Be(2);
    }

    [Test]
    public void SetWasmInitCostScalar_Always_UpdatesInitCostScalarParam()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        // DivCeiling(10, CostScalarPercent=2) = 5.
        ArbOwner.SetWasmInitCostScalar(context, 10);

        context.ArbosState.Programs.GetParams().InitCostScalar.Should().Be(5);
    }

    [Test]
    public void SetWasmExpiryDays_Always_UpdatesExpiryDaysParam()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetWasmExpiryDays(context, 180);

        context.ArbosState.Programs.GetParams().ExpiryDays.Should().Be(180);
    }

    [Test]
    public void SetWasmKeepaliveDays_Always_UpdatesKeepaliveDaysParam()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetWasmKeepaliveDays(context, 14);

        context.ArbosState.Programs.GetParams().KeepaliveDays.Should().Be(14);
    }

    [Test]
    public void SetWasmBlockCacheSize_Always_UpdatesBlockCacheSizeParam()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.SetWasmBlockCacheSize(context, 64);

        context.ArbosState.Programs.GetParams().BlockCacheSize.Should().Be(64);
    }

    [Test]
    public void AddWasmCacheManager_Always_AddsToManagerSet()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        ArbOwner.AddWasmCacheManager(context, ExampleOwnerA);

        context.ArbosState.Programs.CacheManagersStorage.IsMember(ExampleOwnerA).Should().BeTrue();
    }

    [Test]
    public void RemoveWasmCacheManager_ExistingManager_RemovesFromManagerSet()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        context.ArbosState.Programs.CacheManagersStorage.Add(ExampleOwnerA);

        ArbOwner.RemoveWasmCacheManager(context, ExampleOwnerA);

        context.ArbosState.Programs.CacheManagersStorage.IsMember(ExampleOwnerA).Should().BeFalse();
    }

    [Test]
    public void RemoveWasmCacheManager_NonExistent_Throws()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

        Action act = () => ArbOwner.RemoveWasmCacheManager(context, ExampleOwnerA);

        act.Should().Throw<ArbitrumPrecompileException>()
            .WithMessage("*non-manager*");
    }

    [Test]
    public void SetChainConfig_Always_UpdatesChainConfigStorage()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);
        byte[] payload = "{\"chainId\":42}"u8.ToArray();

        ArbOwner.SetChainConfig(context, payload);

        context.ArbosState.ChainConfigStorage.Get().Should().BeEquivalentTo(payload);
    }

    [Test]
    public void SetCalldataPriceIncrease_EnableThenDisable_TogglesFeature()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context);

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
}
