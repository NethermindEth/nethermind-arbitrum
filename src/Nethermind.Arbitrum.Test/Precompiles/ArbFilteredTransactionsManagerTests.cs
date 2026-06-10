// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Precompiles.Parser;
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

    // -------------------------------------------------------------------------
    // GasConsumptionPolicy shape
    // -------------------------------------------------------------------------

    [Test]
    public void ShouldConsumeGas_ForFiltererCaller_ReturnsFreePolicy()
    {
        using IDisposable scope = CreateTestContext(out PrecompileTestContextBuilder ctx,
            filterer: TestItem.AddressA, authorizeFilterer: true);

        GasConsumptionPolicy policy = ArbFilteredTransactionsManagerParser.Instance.ShouldConsumeGas(ctx.WorldState, ctx.Caller);

        policy.IsFree.Should().BeTrue("registered filterers must get free access");
        policy.CheckCost.Should().Be(0, "no check cost should be charged to a filterer");
    }

    [Test]
    public void ShouldConsumeGas_ForNonFiltererCaller_ReturnsCheckCostPolicy()
    {
        using IDisposable scope = CreateTestContext(out PrecompileTestContextBuilder ctx,
            filterer: TestItem.AddressB, authorizeFilterer: false);

        GasConsumptionPolicy policy = ArbFilteredTransactionsManagerParser.Instance.ShouldConsumeGas(ctx.WorldState, ctx.Caller);

        policy.IsFree.Should().BeFalse("non-filterers do not get free access");
        policy.CheckCost.Should().Be(ArbosStorage.StorageReadCost,
            "non-filterers pay only the cost of the filterer membership check, " +
            "matching Nitro's FreeAccessPrecompile burner.GasLeft() return");
    }

    [Test]
    public void ShouldConsumeGas_DefaultInterfaceImpl_ReturnsDefaultPolicy()
    {
        // Any precompile that does not override ShouldConsumeGas should return the
        // no-op default so that normal gas accounting is unchanged.
        using IDisposable scope = CreateTestContext(out PrecompileTestContextBuilder ctx,
            filterer: TestItem.AddressA, authorizeFilterer: false);

        GasConsumptionPolicy policy = ((IArbitrumPrecompile)ArbInfoParser.Instance).ShouldConsumeGas(ctx.WorldState, ctx.Caller);

        policy.IsFree.Should().BeFalse("default policy must not grant free access");
        policy.CheckCost.Should().Be(0, "default policy must not override gas on failure");
    }

    // -------------------------------------------------------------------------
    // Filterer path: context.Free = true suppresses all Burn() calls
    // -------------------------------------------------------------------------

    [Test]
    public void AddFilteredTransaction_WithFreeFlag_BurnsNoGas()
    {
        using IDisposable scope = CreateTestContext(out PrecompileTestContextBuilder ctx,
            filterer: TestItem.AddressA, authorizeFilterer: true);

        // Capture baseline after setup (ArbosState open may have already charged some gas).
        ulong gasLeftBefore = ctx.GasLeft;
        ulong multiGasBefore = ctx.BurnedMultiGas.Total;
        ctx.Free = true;

        ArbFilteredTransactionsManager.AddFilteredTransaction(ctx, SampleTxHash);

        ctx.GasLeft.Should().Be(gasLeftBefore,
            "setting Free=true (filterer path) must suppress all Burn() calls");
        ctx.BurnedMultiGas.Total.Should().Be(multiGasBefore,
            "no additional multigas should be recorded for a free call");
    }

    [Test]
    public void DeleteFilteredTransaction_WithFreeFlag_BurnsNoGas()
    {
        using IDisposable scope = CreateTestContext(out PrecompileTestContextBuilder ctx,
            filterer: TestItem.AddressA, authorizeFilterer: true, gasSupplied: 500_000);

        ArbFilteredTransactionsManager.AddFilteredTransaction(ctx, SampleTxHash);
        ctx.ResetGasLeft();

        ulong gasLeftBefore = ctx.GasLeft;
        ulong multiGasBefore = ctx.BurnedMultiGas.Total;
        ctx.Free = true;

        ArbFilteredTransactionsManager.DeleteFilteredTransaction(ctx, SampleTxHash);

        ctx.GasLeft.Should().Be(gasLeftBefore,
            "setting Free=true must suppress all Burn() calls for delete as well");
        ctx.BurnedMultiGas.Total.Should().Be(multiGasBefore,
            "no additional multigas should be recorded for a free call");
    }

    [Test]
    public void IsTransactionFiltered_WithFreeFlag_BurnsNoGas()
    {
        using IDisposable scope = CreateTestContext(out PrecompileTestContextBuilder ctx,
            filterer: TestItem.AddressA, authorizeFilterer: true);

        ulong gasLeftBefore = ctx.GasLeft;
        ulong multiGasBefore = ctx.BurnedMultiGas.Total;
        ctx.Free = true;

        ArbFilteredTransactionsManager.IsTransactionFiltered(ctx, SampleTxHash);

        ctx.GasLeft.Should().Be(gasLeftBefore,
            "Free=true must suppress gas charges for view methods too");
        ctx.BurnedMultiGas.Total.Should().Be(multiGasBefore,
            "no additional multigas should be recorded for a free call");
    }

    [Test]
    public void AddFilteredTransaction_WithFreeFlag_StillMutatesState()
    {
        // Free only skips gas accounting — the actual state write must still happen.
        using IDisposable scope = CreateTestContext(out PrecompileTestContextBuilder ctx,
            filterer: TestItem.AddressA, authorizeFilterer: true);

        ctx.Free = true;
        ArbFilteredTransactionsManager.AddFilteredTransaction(ctx, SampleTxHash);

        // Turn Free off so the read charges gas normally (avoids conflating the two concerns).
        ctx.Free = false;
        ArbFilteredTransactionsManager.IsTransactionFiltered(ctx, SampleTxHash)
            .Should().BeTrue("state mutation must happen even when Free=true");
    }

    [Test]
    public void DeleteFilteredTransaction_WithFreeFlag_StillMutatesState()
    {
        using IDisposable scope = CreateTestContext(out PrecompileTestContextBuilder ctx,
            filterer: TestItem.AddressA, authorizeFilterer: true, gasSupplied: 500_000);

        ArbFilteredTransactionsManager.AddFilteredTransaction(ctx, SampleTxHash);
        ctx.Free = true;

        ArbFilteredTransactionsManager.DeleteFilteredTransaction(ctx, SampleTxHash);

        ctx.Free = false;
        ArbFilteredTransactionsManager.IsTransactionFiltered(ctx, SampleTxHash)
            .Should().BeFalse("state mutation must happen even when Free=true");
    }

    // -------------------------------------------------------------------------
    // Non-filterer path: BurnOut still fires at the precompile level
    // (the VM's CheckCost override restores gas externally after the exception)
    // -------------------------------------------------------------------------

    [Test]
    public void AddFilteredTransaction_NonFiltererWithoutFreeFlag_BurnOutStillFires()
    {
        // The inner precompile's BurnOut fires as normal for non-filterers. The VM is
        // responsible for overriding state.Gas to GasSupplied - CheckCost afterwards.
        // This test confirms that the context-level behaviour is unchanged and the VM
        // truly needs the external override to avoid charging full gas.
        using IDisposable scope = CreateTestContext(out PrecompileTestContextBuilder ctx,
            filterer: TestItem.AddressB, authorizeFilterer: false);

        Action act = () => ArbFilteredTransactionsManager.AddFilteredTransaction(ctx, SampleTxHash);

        ArbitrumPrecompileException exception = act.Should()
            .Throw<ArbitrumPrecompileException>("BurnOut must still be thrown for non-filterers")
            .Which;

        exception.OutOfGas.Should().BeTrue();
        ctx.GasLeft.Should().Be(0,
            "BurnOut drains context.GasLeft to zero; the VM overrides state.Gas externally");
    }

    [Test]
    public void DeleteFilteredTransaction_NonFiltererWithoutFreeFlag_BurnOutStillFires()
    {
        using IDisposable scope = CreateTestContext(out PrecompileTestContextBuilder ctx,
            filterer: TestItem.AddressB, authorizeFilterer: false);

        Action act = () => ArbFilteredTransactionsManager.DeleteFilteredTransaction(ctx, SampleTxHash);

        act.Should().Throw<ArbitrumPrecompileException>()
            .Which.OutOfGas.Should().BeTrue();
        ctx.GasLeft.Should().Be(0);
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
