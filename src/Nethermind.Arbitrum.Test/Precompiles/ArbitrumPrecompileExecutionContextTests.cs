// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Test;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public sealed class ArbitrumPrecompileExecutionContextTests
{
    [Test]
    public void BurnOut_FullGasSupply_CreditsRemainingGasToComputation()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);
        PrecompileTestContextBuilder context = new(worldState, 1000);

        Action action = context.BurnOut;

        ShouldThrowOutOfGas(action);
        context.GasLeft.Should().Be(0);
        context.BurnedMultiGas.Get(ResourceKind.Computation).Should().Be(1000);
        context.BurnedMultiGas.Total.Should().Be(1000);
    }

    [Test]
    public void BurnOut_AfterPartialBurn_CreditsOnlyRemainder()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);
        PrecompileTestContextBuilder context = new(worldState, 1000);
        context.Burn(ResourceKind.SingleDim, 300);

        Action action = context.BurnOut;

        ShouldThrowOutOfGas(action);
        context.GasLeft.Should().Be(0);
        context.BurnedMultiGas.Get(ResourceKind.SingleDim).Should().Be(300);
        context.BurnedMultiGas.Get(ResourceKind.Computation).Should().Be(700);
        context.BurnedMultiGas.Total.Should().Be(1000);
    }

    [Test]
    public void BurnByKind_ExceedingGasLeft_CreditsRemainderToComputation()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);
        PrecompileTestContextBuilder context = new(worldState, 100);

        Action action = () => context.Burn(ResourceKind.SingleDim, 500);

        ShouldThrowOutOfGas(action);
        context.GasLeft.Should().Be(0);
        // The failed burn never partial-credits to the caller's kind; only the BurnOut credit lands.
        context.BurnedMultiGas.Get(ResourceKind.SingleDim).Should().Be(0);
        context.BurnedMultiGas.Get(ResourceKind.Computation).Should().Be(100);
        context.BurnedMultiGas.Total.Should().Be(100);
    }

    [Test]
    public void BurnMultiGas_TotalExceedingGasLeft_CreditsRemainderToComputation()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);
        PrecompileTestContextBuilder context = new(worldState, 100);
        MultiGas amount = new();
        amount.Increment(ResourceKind.SingleDim, 500);

        Action action = () => context.Burn(in amount);

        ShouldThrowOutOfGas(action);
        context.GasLeft.Should().Be(0);
        context.BurnedMultiGas.Get(ResourceKind.SingleDim).Should().Be(0);
        context.BurnedMultiGas.Get(ResourceKind.Computation).Should().Be(100);
        context.BurnedMultiGas.Total.Should().Be(100);
    }

    [Test]
    public void BurnByKind_ExactlyGasLeft_SucceedsWithoutBurnOut()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);
        PrecompileTestContextBuilder context = new(worldState, 100);

        context.Burn(ResourceKind.SingleDim, 100);

        context.GasLeft.Should().Be(0);
        context.BurnedMultiGas.Get(ResourceKind.SingleDim).Should().Be(100);
        context.BurnedMultiGas.Get(ResourceKind.Computation).Should().Be(0);
        context.BurnedMultiGas.Total.Should().Be(100);
    }

    [Test]
    public void BurnByKind_LessThanGasLeft_DeductsAndCreditsKind()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);
        PrecompileTestContextBuilder context = new(worldState, 100);

        context.Burn(ResourceKind.SingleDim, 50);

        context.GasLeft.Should().Be(50);
        context.BurnedMultiGas.Get(ResourceKind.SingleDim).Should().Be(50);
        context.BurnedMultiGas.Get(ResourceKind.Computation).Should().Be(0);
        context.BurnedMultiGas.Total.Should().Be(50);
    }

    [Test]
    public void Free_BurnOnInsufficientGas_DoesNotThrow()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);
        PrecompileTestContextBuilder context = new(worldState, 100) { Free = true };

        context.Burn(ResourceKind.SingleDim, ulong.MaxValue);

        context.GasLeft.Should().Be(100);
        context.BurnedMultiGas.IsZero().Should().BeTrue();
    }

    private static void ShouldThrowOutOfGas(Action action)
    {
        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateOutOfGasException();
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }
}
