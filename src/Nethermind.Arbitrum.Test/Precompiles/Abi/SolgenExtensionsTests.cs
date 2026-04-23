// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Collections.Frozen;
using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Crypto;
using Solgen = Nethermind.Arbitrum.Precompiles.Solgen;

namespace Nethermind.Arbitrum.Test.Precompiles.Abi;

[TestFixture]
public class SolgenExtensionsTests
{
    [TestCase(8)]
    [TestCase(16)]
    [TestCase(64)]
    [TestCase(256)]
    public void ToAbiType_ForUInt_MapsToAbiUInt(int size)
    {
        AbiType adapted = Solgen.AbiType.UInt(size).ToAbiType();

        adapted.Should().BeOfType<AbiUInt>();
        adapted.Name.Should().Be($"uint{size}");
    }

    [TestCase(8)]
    [TestCase(256)]
    public void ToAbiType_ForInt_MapsToAbiInt(int size)
    {
        AbiType adapted = Solgen.AbiType.Int(size).ToAbiType();

        adapted.Should().BeOfType<AbiInt>();
        adapted.Name.Should().Be($"int{size}");
    }

    [Test]
    public void ToAbiType_ForFixedBytes_MapsToAbiBytes()
    {
        AbiType adapted = Solgen.AbiType.Bytes(32).ToAbiType();

        adapted.Should().BeOfType<AbiBytes>();
        adapted.Name.Should().Be("bytes32");
    }

    [Test]
    public void ToAbiType_ForPrimitives_MapSemantically()
    {
        Solgen.AbiType.DynamicBytes.ToAbiType().Should().BeSameAs(AbiType.DynamicBytes);
        Solgen.AbiType.String.ToAbiType().Should().BeSameAs(AbiType.String);
        Solgen.AbiType.Bool.ToAbiType().Should().BeSameAs(AbiType.Bool);
        Solgen.AbiType.Address.ToAbiType().Should().BeSameAs(AbiType.Address);
    }

    [Test]
    public void ToAbiType_ForDynamicArray_MapsRecursively()
    {
        AbiType adapted = Solgen.AbiType.Array(Solgen.AbiType.UInt(256)).ToAbiType();

        adapted.Should().BeOfType<AbiArray>();
        ((AbiArray)adapted).ElementType.Should().BeOfType<AbiUInt>();
        adapted.Name.Should().Be("uint256[]");
    }

    [Test]
    public void ToAbiType_ForFixedArray_MapsRecursivelyWithLength()
    {
        AbiType adapted = Solgen.AbiType.FixedArray(Solgen.AbiType.UInt(64), 3).ToAbiType();

        adapted.Should().BeOfType<AbiFixedLengthArray>();
        AbiFixedLengthArray fixedArray = (AbiFixedLengthArray)adapted;
        fixedArray.Length.Should().Be(3);
        fixedArray.ElementType.Should().BeOfType<AbiUInt>();
        adapted.Name.Should().Be("uint64[3]");
    }

    [Test]
    public void ToAbiType_ForNestedArrayOfFixedArray_MatchesArbOwnerGasPricingConstraints()
    {
        AbiType adapted = Solgen.AbiType.Array(Solgen.AbiType.FixedArray(Solgen.AbiType.UInt(64), 3)).ToAbiType();

        adapted.Name.Should().Be("uint64[3][]");
    }

    [Test]
    public void ToAbiType_ForTuple_MapsAllElements()
    {
        AbiType adapted = Solgen.AbiType.Tuple(Solgen.AbiType.UInt(256), Solgen.AbiType.Bool, Solgen.AbiType.Address).ToAbiType();

        adapted.Should().BeOfType<AbiTuple>();
        adapted.Name.Should().Be("(uint256,bool,address)");
    }

    [Test]
    public void ToArbitrumFunctionDescription_ForArbSys_PreservesMethodIdsAndSignatures()
    {
        FrozenDictionary<uint, ArbitrumFunctionDescription> adapted = Solgen.ArbSys.Functions.All
            .ToFrozenDictionary(f => f.Key, f => f.Value.ToArbitrumFunctionDescription());

        adapted[Solgen.ArbSys.Methods.ArbBlockNumber].AbiFunctionDescription.Name
            .Should().Be("arbBlockNumber");

        adapted[Solgen.ArbSys.Methods.SendMerkleTreeState].AbiFunctionDescription.Outputs
            .Select(o => o.Type.Name).Should().Equal("uint256", "bytes32", "bytes32[]");
    }

    [Test]
    public void ToArbitrumFunctionDescription_ReturnsMutableValue_EnablingArbosVersionCustomization()
    {
        ArbitrumFunctionDescription target = Solgen.ArbOwner.Functions.All[Solgen.ArbOwner.Methods.SetInfraFeeAccount]
            .ToArbitrumFunctionDescription();
        target.ArbOSVersion.Should().Be(0);

        target.ArbOSVersion = ArbosVersion.Five;

        target.ArbOSVersion.Should().Be(ArbosVersion.Five);
    }

    [Test]
    public void ToAbiEventDescription_Topic0_MatchesPackageTopic0Hex()
    {
        AssertEventTopicMatchesPackage(Solgen.ArbSys.Events.SendMerkleUpdate);
        AssertEventTopicMatchesPackage(Solgen.ArbSys.Events.L2ToL1Tx);
        AssertEventTopicMatchesPackage(Solgen.ArbSys.Events.L2ToL1Transaction);
        AssertEventTopicMatchesPackage(Solgen.ArbOwner.Events.OwnerActs);
        AssertEventTopicMatchesPackage(Solgen.ArbRetryableTx.Events.RedeemScheduled);
        AssertEventTopicMatchesPackage(Solgen.ArbRetryableTx.Events.TicketCreated);
        AssertEventTopicMatchesPackage(Solgen.ArbWasm.Events.ProgramActivated);
        AssertEventTopicMatchesPackage(Solgen.ArbWasm.Events.ProgramLifetimeExtended);
        AssertEventTopicMatchesPackage(Solgen.ArbWasmCache.Events.UpdateProgramCache);
        AssertEventTopicMatchesPackage(Solgen.ArbNativeTokenManager.Events.NativeTokenMinted);
        AssertEventTopicMatchesPackage(Solgen.ArbNativeTokenManager.Events.NativeTokenBurned);
        AssertEventTopicMatchesPackage(Solgen.ArbOwnerPublic.Events.ChainOwnerRectified);
        AssertEventTopicMatchesPackage(Solgen.ArbDebug.Events.Basic);
        AssertEventTopicMatchesPackage(Solgen.ArbDebug.Events.Mixed);
        AssertEventTopicMatchesPackage(Solgen.ArbDebug.Events.Store);
    }

    [Test]
    public void ToAbiErrorDescription_Selector_MatchesPackageSelector()
    {
        AssertErrorSelectorMatchesPackage(Solgen.ArbSys.Errors.InvalidBlockNumber);
        AssertErrorSelectorMatchesPackage(Solgen.ArbosActs.Errors.CallerNotArbOS);
        AssertErrorSelectorMatchesPackage(Solgen.ArbRetryableTx.Errors.NoTicketWithID);
        AssertErrorSelectorMatchesPackage(Solgen.ArbRetryableTx.Errors.NotCallable);
        AssertErrorSelectorMatchesPackage(Solgen.ArbWasm.Errors.ProgramNotWasm);
        AssertErrorSelectorMatchesPackage(Solgen.ArbWasm.Errors.ProgramNotActivated);
        AssertErrorSelectorMatchesPackage(Solgen.ArbWasm.Errors.ProgramNeedsUpgrade);
        AssertErrorSelectorMatchesPackage(Solgen.ArbWasm.Errors.ProgramExpired);
        AssertErrorSelectorMatchesPackage(Solgen.ArbWasm.Errors.ProgramUpToDate);
        AssertErrorSelectorMatchesPackage(Solgen.ArbWasm.Errors.ProgramKeepaliveTooSoon);
        AssertErrorSelectorMatchesPackage(Solgen.ArbWasm.Errors.ProgramInsufficientValue);
        AssertErrorSelectorMatchesPackage(Solgen.ArbDebug.Errors.Custom);
        AssertErrorSelectorMatchesPackage(Solgen.ArbDebug.Errors.Unused);
    }

    [Test]
    public void ToAbiFunctionDescription_ForPayable_PreservesStateMutability()
    {
        AbiFunctionDescription adapted = Solgen.ArbSys.Functions.All[Solgen.ArbSys.Methods.SendTxToL1]
            .ToAbiFunctionDescription();

        adapted.StateMutability.Should().Be(StateMutability.Payable);
    }

    [Test]
    public void ToAbiFunctionDescription_ForView_PreservesStateMutability()
    {
        AbiFunctionDescription adapted = Solgen.ArbSys.Functions.All[Solgen.ArbSys.Methods.ArbBlockNumber]
            .ToAbiFunctionDescription();

        adapted.StateMutability.Should().Be(StateMutability.View);
    }

    [Test]
    public void PrecompileFunctionDescription_UsedByParser_ContainsExpectedKeys()
    {
        ArbSysParser.PrecompileFunctionDescription.Keys
            .Should().BeEquivalentTo(Solgen.ArbSys.Functions.All.Keys);
    }

    private static void AssertEventTopicMatchesPackage(Solgen.EventDescriptor descriptor)
    {
        AbiEventDescription adapted = descriptor.ToAbiEventDescription();
        Hash256 topic0 = adapted.GetHash();

        topic0.ToString().Should().Be(descriptor.Topic0Hex, $"Topic0 for event {descriptor.Name} must agree with package");
    }

    private static void AssertErrorSelectorMatchesPackage(Solgen.ErrorDescriptor descriptor)
    {
        AbiErrorDescription adapted = descriptor.ToAbiErrorDescription();

        adapted.GetSelector().Should().Be(descriptor.Selector, $"Selector for error {descriptor.Name} must agree with package");
    }
}
