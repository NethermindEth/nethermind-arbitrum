// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public class ArbOwnerTests
{
    private const ulong DefaultGasSupplied = 1_000_000;

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
        // keccak256("OwnerActs(bytes4,address,bytes)")
        ArbOwner.OwnerActsEvent.GetHash().Should().Be(
            new Hash256("0x3c9e6a772755407311e3b35b3ee56799df8f87395941b3a658eee9e08a67ebda"));
    }

    [Test]
    public void SetGasPricingConstraints_BelowFiftyArbOSVersion_IsRejected()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateScope = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, DefaultGasSupplied)
            .WithArbosVersion(ArbosVersion.Fifty - 1);

        bool result = ArbOwnerParser.Instance.TryCheckMethodVisibility(context, NullLogger.Instance,
            PrecompileHelper.GetMethodId("setGasPricingConstraints(uint64[3][])"), out bool shouldRevert, out _);

        result.Should().BeFalse();
        shouldRevert.Should().BeTrue();
    }

    [Test]
    public void SetGasPricingConstraints_AtFiftyArbOSVersion_IsDispatched()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateScope = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, DefaultGasSupplied)
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithExecutingAccount(ArbOwnerParser.Address);

        bool result = ArbOwnerParser.Instance.TryCheckMethodVisibility(context, NullLogger.Instance,
            PrecompileHelper.GetMethodId("setGasPricingConstraints(uint64[3][])"), out bool _, out PrecompileHandler? handler);

        result.Should().BeTrue();
        handler.Should().NotBeNull();
    }
}
