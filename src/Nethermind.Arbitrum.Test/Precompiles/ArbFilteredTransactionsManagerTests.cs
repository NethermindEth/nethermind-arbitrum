// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Solgen = Nethermind.Arbitrum.Precompiles.Solgen;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public class ArbFilteredTransactionsManagerTests
{
    private static readonly Hash256 SampleTxHash = new("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");
    private static readonly Hash256 AnotherTxHash = new("0xdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef");

    [Test]
    public void AddFilteredTransaction_WithAuthorizedFilterer_MarksTxAsFiltered()
    {
        using IDisposable scope = CreateTestContext(out PrecompileTestContextBuilder context,
            filterer: TestItem.AddressA, authorizeFilterer: true);

        ArbFilteredTransactionsManager.AddFilteredTransaction(context, SampleTxHash);

        ArbFilteredTransactionsManager.IsTransactionFiltered(context, SampleTxHash)
            .Should().BeTrue();
    }

    [Test]
    public void AddFilteredTransaction_WithUnauthorizedCaller_BurnsAllGas()
    {
        using IDisposable scope = CreateTestContext(out PrecompileTestContextBuilder context,
            filterer: TestItem.AddressA, authorizeFilterer: false);

        Action action = () => ArbFilteredTransactionsManager.AddFilteredTransaction(context, SampleTxHash);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        exception.OutOfGas.Should().BeTrue("should burn all gas on unauthorized access");
        context.GasLeft.Should().Be(0, "all gas should be burned");
    }

    [Test]
    public void DeleteFilteredTransaction_WithAuthorizedFilterer_RemovesTxFromFiltered()
    {
        using IDisposable scope = CreateTestContext(out PrecompileTestContextBuilder context,
            filterer: TestItem.AddressA, authorizeFilterer: true, gasSupplied: 500_000);

        ArbFilteredTransactionsManager.AddFilteredTransaction(context, SampleTxHash);
        ArbFilteredTransactionsManager.DeleteFilteredTransaction(context, SampleTxHash);

        ArbFilteredTransactionsManager.IsTransactionFiltered(context, SampleTxHash)
            .Should().BeFalse();
    }

    [Test]
    public void DeleteFilteredTransaction_WithUnauthorizedCaller_BurnsAllGas()
    {
        using IDisposable scope = CreateTestContext(out PrecompileTestContextBuilder context,
            filterer: TestItem.AddressA, authorizeFilterer: false);

        Action action = () => ArbFilteredTransactionsManager.DeleteFilteredTransaction(context, SampleTxHash);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        exception.OutOfGas.Should().BeTrue("should burn all gas on unauthorized access");
        context.GasLeft.Should().Be(0, "all gas should be burned");
    }

    [Test]
    public void IsTransactionFiltered_WhenTxNotFiltered_ReturnsFalse()
    {
        using IDisposable scope = CreateTestContext(out PrecompileTestContextBuilder context,
            filterer: TestItem.AddressA, authorizeFilterer: false);

        bool result = ArbFilteredTransactionsManager.IsTransactionFiltered(context, SampleTxHash);

        result.Should().BeFalse();
    }

    [Test]
    public void IsTransactionFiltered_WithAnyCaller_DoesNotThrow()
    {
        using IDisposable scope = CreateTestContext(out PrecompileTestContextBuilder context,
            filterer: TestItem.AddressA, authorizeFilterer: false);

        Action action = () => ArbFilteredTransactionsManager.IsTransactionFiltered(context, SampleTxHash);

        action.Should().NotThrow();
    }

    [Test]
    public void AddFilteredTransaction_WhenCalled_EmitsFilteredTransactionAddedEvent()
    {
        using IDisposable scope = CreateTestContext(out PrecompileTestContextBuilder context,
            filterer: TestItem.AddressA, authorizeFilterer: true, gasSupplied: 500_000);

        ArbFilteredTransactionsManager.AddFilteredTransaction(context, SampleTxHash);

        context.EventLogs.Should().HaveCount(1);
        context.EventLogs[0].Address.Should().Be(ArbFilteredTransactionsManager.Address);
        context.EventLogs[0].Topics[0].Should().Be(
            new Hash256(Solgen.ArbFilteredTransactionsManager.Events.FilteredTransactionAdded.Topic0Hex),
            "first topic should be event signature");
        context.EventLogs[0].Topics[1].Should().Be(SampleTxHash,
            "second topic should be the indexed txHash");
    }

    [Test]
    public void DeleteFilteredTransaction_WhenCalled_EmitsFilteredTransactionDeletedEvent()
    {
        using IDisposable scope = CreateTestContext(out PrecompileTestContextBuilder context,
            filterer: TestItem.AddressA, authorizeFilterer: true, gasSupplied: 500_000);

        ArbFilteredTransactionsManager.AddFilteredTransaction(context, SampleTxHash);
        context.EventLogs.Clear();

        ArbFilteredTransactionsManager.DeleteFilteredTransaction(context, SampleTxHash);

        context.EventLogs.Should().HaveCount(1);
        context.EventLogs[0].Address.Should().Be(ArbFilteredTransactionsManager.Address);
        context.EventLogs[0].Topics[0].Should().Be(
            new Hash256(Solgen.ArbFilteredTransactionsManager.Events.FilteredTransactionDeleted.Topic0Hex),
            "first topic should be event signature");
        context.EventLogs[0].Topics[1].Should().Be(SampleTxHash,
            "second topic should be the indexed txHash");
    }

    [Test]
    public void AddFilteredTransaction_MultipleTxHashes_FiltersEachIndependently()
    {
        using IDisposable scope = CreateTestContext(out PrecompileTestContextBuilder context,
            filterer: TestItem.AddressA, authorizeFilterer: true, gasSupplied: 500_000);

        ArbFilteredTransactionsManager.AddFilteredTransaction(context, SampleTxHash);
        ArbFilteredTransactionsManager.AddFilteredTransaction(context, AnotherTxHash);

        ArbFilteredTransactionsManager.IsTransactionFiltered(context, SampleTxHash).Should().BeTrue();
        ArbFilteredTransactionsManager.IsTransactionFiltered(context, AnotherTxHash).Should().BeTrue();
    }

    [Test]
    public void DeleteFilteredTransaction_WithMultipleTxHashes_OnlyDeletesSpecifiedOne()
    {
        using IDisposable scope = CreateTestContext(out PrecompileTestContextBuilder context,
            filterer: TestItem.AddressA, authorizeFilterer: true, gasSupplied: 500_000);

        ArbFilteredTransactionsManager.AddFilteredTransaction(context, SampleTxHash);
        ArbFilteredTransactionsManager.AddFilteredTransaction(context, AnotherTxHash);
        ArbFilteredTransactionsManager.DeleteFilteredTransaction(context, SampleTxHash);

        ArbFilteredTransactionsManager.IsTransactionFiltered(context, SampleTxHash).Should().BeFalse();
        ArbFilteredTransactionsManager.IsTransactionFiltered(context, AnotherTxHash).Should().BeTrue();
    }

    [Test]
    public void MethodIds_AllFunctions_MatchExpectedSelectors()
    {
        PrecompileTestAbiHelpers.GetMethodId("addFilteredTransaction(bytes32)")
            .Should().Be(Solgen.ArbFilteredTransactionsManager.Methods.AddFilteredTransaction);
        PrecompileTestAbiHelpers.GetMethodId("deleteFilteredTransaction(bytes32)")
            .Should().Be(Solgen.ArbFilteredTransactionsManager.Methods.DeleteFilteredTransaction);
        PrecompileTestAbiHelpers.GetMethodId("isTransactionFiltered(bytes32)")
            .Should().Be(Solgen.ArbFilteredTransactionsManager.Methods.IsTransactionFiltered);
    }

    [Test]
    public void Abi_WhenParsed_ContainsExpectedFunctionSignatures()
    {
        Dictionary<uint, ArbitrumFunctionDescription> allFunctions =
            PrecompileTestAbiHelpers.GetAllFunctionDescriptions(Solgen.ArbFilteredTransactionsManager.Abi);

        allFunctions.Keys.Should().BeEquivalentTo(new[]
        {
            PrecompileTestAbiHelpers.GetMethodId("addFilteredTransaction(bytes32)"),
            PrecompileTestAbiHelpers.GetMethodId("deleteFilteredTransaction(bytes32)"),
            PrecompileTestAbiHelpers.GetMethodId("isTransactionFiltered(bytes32)"),
        });
    }

    [Test]
    public void Abi_WhenParsed_ContainsExpectedEvents()
    {
        Dictionary<string, Nethermind.Abi.AbiEventDescription> allEvents =
            PrecompileTestAbiHelpers.GetAllEventDescriptions(Solgen.ArbFilteredTransactionsManager.Abi);

        allEvents.Keys.Should().BeEquivalentTo("FilteredTransactionAdded", "FilteredTransactionDeleted");
    }

    [Test]
    public void Abi_WhenParsed_ContainsNoErrors()
    {
        PrecompileTestAbiHelpers.GetAllErrorDescriptions(Solgen.ArbFilteredTransactionsManager.Abi)
            .Should().BeEmpty();
    }

    private static IDisposable CreateTestContext(
        out PrecompileTestContextBuilder context,
        Address filterer,
        bool authorizeFilterer,
        ulong gasSupplied = 100_000)
    {
        IDisposable scope = PrecompileTestContextBuilder.Create(out context, setup: c =>
        {
            PrecompileTestContextBuilder result = c
                .WithArbosState()
                .WithArbosVersion(ArbosVersion.Sixty)
                .WithCaller(filterer)
                .WithReleaseSpec();

            if (authorizeFilterer)
                result = result.WithTransactionFilterers(filterer);

            return result.WithGasSupplied(gasSupplied);
        });
        return scope;
    }
}
