// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Arbos.Storage;

public class L2PricingTests
{
    [Test]
    public void AddToGasPool_GasIsNegative_IncreasesBacklog()
    {
        using IDisposable disposable = CreateInitialized(out L2PricingState state);

        state.GasBacklogStorage.Set(150);
        state.AddToGasPool(-100);

        state.GasBacklogStorage.Get().Should().Be(250);
    }
    [Test]
    public void AddToGasPool_GasIsPositiveAndGasBacklogIsEmpty_DoesNothing()
    {
        using IDisposable disposable = CreateInitialized(out L2PricingState state);

        state.AddToGasPool(100);

        state.GasBacklogStorage.Get().Should().Be(0);
    }

    [Test]
    public void AddToGasPool_GasIsPositiveAndGasBacklogIsNotEmpty_DecreasesBacklog()
    {
        using IDisposable disposable = CreateInitialized(out L2PricingState state);

        state.GasBacklogStorage.Set(150);
        state.AddToGasPool(100);

        state.GasBacklogStorage.Get().Should().Be(50);
    }

    [Test]
    public void PerTxGasLimitStorage_AfterInitialization_IsZero()
    {
        using IDisposable disposable = CreateInitialized(out L2PricingState state);

        state.PerTxGasLimitStorage.Get().Should().Be(0);
    }

    [TestCase(10_000_000ul, 32_000_000ul)]
    [TestCase(100_000_000ul, 32_000_000ul)]
    [TestCase(10_000_000ul, 50_000_000ul)]
    public void SetMaxPerTxGasLimit_WithDifferentBlockLimit_StoresIndependently(ulong blockLimit, ulong txLimit)
    {
        using IDisposable disposable = CreateInitialized(out L2PricingState state);

        state.SetMaxPerBlockGasLimit(blockLimit);
        state.SetMaxPerTxGasLimit(txLimit);

        state.PerTxGasLimitStorage.Get().Should().Be(txLimit);
        state.PerBlockGasLimitStorage.Get().Should().Be(blockLimit);
    }

    [TestCase(0ul)]
    [TestCase(1_000_000ul)]
    [TestCase(16_000_000ul)]
    [TestCase(32_000_000ul)]
    [TestCase(L2PricingState.InitialPerTxGasLimit)]
    [TestCase(64_000_000ul)]
    [TestCase(ulong.MaxValue)]
    public void SetMaxPerTxGasLimit_WithValue_StoresCorrectly(ulong limit)
    {
        using IDisposable disposable = CreateInitialized(out L2PricingState state);

        state.SetMaxPerTxGasLimit(limit);

        state.PerTxGasLimitStorage.Get().Should().Be(limit);
    }

    [Test]
    public void UpdatePricingModel_BacklogAboveTolerance_IncreasesBaseFee()
    {
        using IDisposable disposable = CreateInitialized(out L2PricingState state);

        ulong speedLimit = 1_000_000;
        ulong tolerance = 10;
        ulong timePassed = 1;
        ulong backlog = tolerance * speedLimit + speedLimit * 10; // Well above tolerance

        state.SpeedLimitPerSecondStorage.Set(speedLimit);
        state.BacklogToleranceStorage.Set(tolerance);
        state.GasBacklogStorage.Set(backlog);
        state.PricingInertiaStorage.Set(100);

        state.UpdatePricingModel(timePassed);

        state.BaseFeeWeiStorage.Get().Should().Be(109410000);
    }

    [Test]
    public void UpdatePricingModel_BacklogAtULongMax_CalculatesCorrectly()
    {
        using IDisposable disposable = CreateInitialized(out L2PricingState state);

        ulong speedLimit = 1_000_000;
        ulong timePassed = 100;

        state.SpeedLimitPerSecondStorage.Set(speedLimit);
        state.GasBacklogStorage.Set(ulong.MaxValue);

        state.UpdatePricingModel(timePassed);

        // Should decrease from ulong.MaxValue by (timePassed * speedLimit)
        ulong expectedBacklog = ulong.MaxValue - timePassed * speedLimit;
        state.GasBacklogStorage.Get().Should().Be(expectedBacklog);
    }

    [Test]
    public void UpdatePricingModel_BacklogBelowTolerance_SetsBaseFeeToMinimum()
    {
        using IDisposable disposable = CreateInitialized(out L2PricingState state);

        ulong speedLimit = 1_000_000;
        ulong tolerance = 10;
        ulong backlog = tolerance * speedLimit - 1; // Just below tolerance

        state.SpeedLimitPerSecondStorage.Set(speedLimit);
        state.BacklogToleranceStorage.Set(tolerance);
        state.GasBacklogStorage.Set(backlog);

        state.UpdatePricingModel(1);

        state.BaseFeeWeiStorage.Get().Should().Be(state.MinBaseFeeWeiStorage.Get());
    }

    [Test]
    public void UpdatePricingModel_BacklogExcessOverflow_HandlesWithoutCrashing()
    {
        using IDisposable disposable = CreateInitialized(out L2PricingState state);

        ulong speedLimit = 1_000;
        ulong tolerance = 1;
        ulong timePassed = 0; // Don't pass time to keep backlog at max
        ulong backlog = ulong.MaxValue; // Extremely large backlog

        state.SpeedLimitPerSecondStorage.Set(speedLimit);
        state.BacklogToleranceStorage.Set(tolerance);
        state.GasBacklogStorage.Set(backlog);
        state.PricingInertiaStorage.Set(1); // Very low inertia to ensure large exponentBips

        state.UpdatePricingModel(timePassed);

        // With extreme backlog, saturating multiplication produces a huge positive exponent.
        // This matches Nitro: exponentBips = int64.MaxValue/1000, ApproxExp returns ~1.84e15,
        // baseFee = minBaseFee * 1.84e15 / 10000 ≈ 1.84e19
        state.BaseFeeWeiStorage.Get().Should().Be(UInt256.Parse("18446744073809550000"));
    }

    [Test]
    public void UpdatePricingModel_InertiaSpeedLimitOverflow_SaturatesCorrectly()
    {
        using IDisposable disposable = CreateInitialized(out L2PricingState state);

        ulong speedLimit = ulong.MaxValue / 2;
        ulong inertia = ulong.MaxValue / 2;
        ulong tolerance = 1;
        ulong backlog = tolerance * speedLimit + 1_000_000;

        state.SpeedLimitPerSecondStorage.Set(speedLimit);
        state.BacklogToleranceStorage.Set(tolerance);
        state.GasBacklogStorage.Set(backlog);
        state.PricingInertiaStorage.Set(inertia);

        // inertia.SaturateMul(speedLimit) will overflow and saturate
        state.UpdatePricingModel(1);

        // Should handle the saturated multiplication correctly
        state.BaseFeeWeiStorage.Get().Should().Be(100000000);
    }

    [Test]
    public void UpdatePricingModel_MaxULongTimePassed_HandlesCorrectly()
    {
        using IDisposable disposable = CreateInitialized(out L2PricingState state);

        ulong timePassed = ulong.MaxValue;
        ulong speedLimit = ulong.MaxValue;
        ulong initialBacklog = ulong.MaxValue;

        state.SpeedLimitPerSecondStorage.Set(speedLimit);
        state.GasBacklogStorage.Set(initialBacklog);

        state.UpdatePricingModel(timePassed);

        // The multiplication will saturate to ulong.MaxValue, then ToLongSafe caps at long.MaxValue
        ulong expectedBacklog = initialBacklog - long.MaxValue;
        state.GasBacklogStorage.Get().Should().Be(expectedBacklog);
    }

    [Test]
    public void UpdatePricingModel_SmallTimePassed_DecreasesBacklogCorrectly()
    {
        using IDisposable disposable = CreateInitialized(out L2PricingState state);

        ulong initialBacklog = 10_000_000;
        ulong speedLimit = 1_000_000;
        ulong timePassed = 5;

        state.GasBacklogStorage.Set(initialBacklog);
        state.SpeedLimitPerSecondStorage.Set(speedLimit);

        state.UpdatePricingModel(timePassed);

        ulong expectedBacklog = initialBacklog - timePassed * speedLimit;
        state.GasBacklogStorage.Get().Should().Be(expectedBacklog);
    }

    [Test]
    public void UpdatePricingModel_TimePassedCausesOverflow_SaturatesAtLongMaxValue()
    {
        using IDisposable disposable = CreateInitialized(out L2PricingState state);

        ulong timePassed = ulong.MaxValue / 2;
        ulong speedLimit = 10;
        ulong initialBacklog = ulong.MaxValue;

        state.SpeedLimitPerSecondStorage.Set(speedLimit);
        state.GasBacklogStorage.Set(initialBacklog);

        state.UpdatePricingModel(timePassed);

        // The backlog should decrease by long.MaxValue (the saturated value)
        ulong expectedBacklog = initialBacklog - long.MaxValue;
        state.GasBacklogStorage.Get().Should().Be(expectedBacklog);
    }

    [Test]
    public void UpdatePricingModel_VeryLargeTimeAndSpeed_TriggersToLongSafe()
    {
        using IDisposable disposable = CreateInitialized(out L2PricingState state);

        ulong timePassed = (ulong)long.MaxValue / 1000;
        ulong speedLimit = 2000; // This multiplication exceeds long.MaxValue
        ulong initialBacklog = ulong.MaxValue / 2;

        state.SpeedLimitPerSecondStorage.Set(speedLimit);
        state.GasBacklogStorage.Set(initialBacklog);

        state.UpdatePricingModel(timePassed);

        // The product would be ~2 * long.MaxValue, so ToLongSafe caps it at long.MaxValue
        ulong expectedBacklog = initialBacklog - long.MaxValue;
        state.GasBacklogStorage.Get().Should().Be(expectedBacklog);
    }

    [Test]
    public void UpdatePricingModel_ZeroTimePassed_DoesNotChangeBacklog()
    {
        using IDisposable disposable = CreateInitialized(out L2PricingState state);

        ulong initialBacklog = 1000;
        state.GasBacklogStorage.Set(initialBacklog);

        state.UpdatePricingModel(0);

        state.GasBacklogStorage.Get().Should().Be(initialBacklog);
    }

    private static IDisposable CreateInitialized(out L2PricingState state)
    {
        IDisposable disposable = TestArbosStorage.Create(out _, out ArbosStorage storage);

        L2PricingState.Initialize(storage);
        state = new(storage, ArbosVersion.FiftyOne);

        return disposable;
    }
}
