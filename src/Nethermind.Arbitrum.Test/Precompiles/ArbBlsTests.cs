// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Arbitrum.Test.Precompiles.Abi;
using Solgen = Nethermind.Arbitrum.Precompiles.Solgen;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public class ArbBlsTests
{
    // ArbBls is disabled in Arbitrum; the package ships an empty JSON array "[]" by design.
    // The guards below assert the parser runs and returns empty collections — if the string
    // were ever null/empty, PrecompileTestAbiHelpers short-circuits to empty, so the "[]"
    // check above is what proves the package supplied the expected marker value.
    [Test]
    public void Abi_Always_IsEmptyJsonArray()
    {
        Solgen.ArbBLS.Abi.Should().Be("[]");
    }

    [Test]
    public void Abi_WhenParsed_ContainsNoFunctionSignatures()
    {
        PrecompileTestAbiHelpers.GetAllFunctionDescriptions(Solgen.ArbBLS.Abi).Should().BeEmpty();
    }

    [Test]
    public void Abi_WhenParsed_ContainsNoEvents()
    {
        PrecompileTestAbiHelpers.GetAllEventDescriptions(Solgen.ArbBLS.Abi).Should().BeEmpty();
    }

    [Test]
    public void Abi_WhenParsed_ContainsNoErrors()
    {
        PrecompileTestAbiHelpers.GetAllErrorDescriptions(Solgen.ArbBLS.Abi).Should().BeEmpty();
    }
}
