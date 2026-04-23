// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Abi;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public class ArbBlsTests
{
    // ArbBls is disabled in Arbitrum; the ABI is the empty JSON array "[]" by design.
    // If the string were ever set to null/empty, AbiMetadata.GetAll* short-circuits to
    // an empty dictionary — so this guard asserts the parser actually ran.
    [Test]
    public void Abi_Always_IsEmptyJsonArray()
    {
        ArbBls.Abi.Should().Be("[]");
    }

    [Test]
    public void Abi_WhenParsed_ContainsNoFunctionSignatures()
    {
        AbiMetadata.GetAllFunctionDescriptions(ArbBls.Abi).Should().BeEmpty();
    }

    [Test]
    public void Abi_WhenParsed_ContainsNoEvents()
    {
        AbiMetadata.GetAllEventDescriptions(ArbBls.Abi).Should().BeEmpty();
    }

    [Test]
    public void Abi_WhenParsed_ContainsNoErrors()
    {
        AbiMetadata.GetAllErrorDescriptions(ArbBls.Abi).Should().BeEmpty();
    }
}
