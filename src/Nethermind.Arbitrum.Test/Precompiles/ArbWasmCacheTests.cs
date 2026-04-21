// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Abi;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public class ArbWasmCacheTests
{
    [Test]
    public void Abi_WhenParsed_ContainsExpectedFunctionSignatures()
    {
        Dictionary<uint, ArbitrumFunctionDescription> allFunctions = AbiMetadata.GetAllFunctionDescriptions(ArbWasmCache.Abi);

        allFunctions.Keys.Should().BeEquivalentTo(new[]
        {
            PrecompileHelper.GetMethodId("isCacheManager(address)"),
            PrecompileHelper.GetMethodId("allCacheManagers()"),
            PrecompileHelper.GetMethodId("cacheCodehash(bytes32)"),
            PrecompileHelper.GetMethodId("cacheProgram(address)"),
            PrecompileHelper.GetMethodId("evictCodehash(bytes32)"),
            PrecompileHelper.GetMethodId("codehashIsCached(bytes32)"),
        });
    }

    [Test]
    public void Abi_WhenParsed_ContainsExpectedEvents()
    {
        Dictionary<string, AbiEventDescription> allEvents = AbiMetadata.GetAllEventDescriptions(ArbWasmCache.Abi);

        allEvents.Keys.Should().BeEquivalentTo("UpdateProgramCache");
    }

    [Test]
    public void Abi_WhenParsed_ContainsNoErrors()
    {
        AbiMetadata.GetAllErrorDescriptions(ArbWasmCache.Abi).Should().BeEmpty();
    }

    [Test]
    public void MethodIds_AllFunctions_MatchExpectedSelectors()
    {
        PrecompileHelper.GetMethodId("isCacheManager(address)").Should().Be(0x85e2de85u);
        PrecompileHelper.GetMethodId("allCacheManagers()").Should().Be(0x0ec1d773u);
        PrecompileHelper.GetMethodId("cacheCodehash(bytes32)").Should().Be(0x4ceac817u);
        PrecompileHelper.GetMethodId("cacheProgram(address)").Should().Be(0xe73ac9f2u);
        PrecompileHelper.GetMethodId("evictCodehash(bytes32)").Should().Be(0xce972013u);
        PrecompileHelper.GetMethodId("codehashIsCached(bytes32)").Should().Be(0xa72f179bu);
    }
}
