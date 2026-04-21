// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Abi;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public class ArbOwnerTests
{
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
}
