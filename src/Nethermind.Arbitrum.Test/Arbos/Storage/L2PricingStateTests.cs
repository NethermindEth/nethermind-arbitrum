using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Test;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Test.Arbos.Storage;

[TestFixture]
public class L2PricingStateTests
{
    [Test]
    public void PerTxGasLimitStorage_SetAndGet_WorksCorrectly()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Test various values
        const ulong testValue1 = 10_000_000;
        const ulong testValue2 = 32_000_000;
        const ulong testValue3 = 50_000_000;

        // Set and verify the first value
        l2Pricing.SetMaxPerTxGasLimit(testValue1);
        ulong retrieved1 = l2Pricing.PerTxGasLimitStorage.Get();
        retrieved1.Should().Be(testValue1, "PerTxGasLimit should store and retrieve the first value correctly");

        // Set and verify the second value
        l2Pricing.SetMaxPerTxGasLimit(testValue2);
        ulong retrieved2 = l2Pricing.PerTxGasLimitStorage.Get();
        retrieved2.Should().Be(testValue2, "PerTxGasLimit should store and retrieve the second value correctly");

        // Set and verify a third value
        l2Pricing.SetMaxPerTxGasLimit(testValue3);
        ulong retrieved3 = l2Pricing.PerTxGasLimitStorage.Get();
        retrieved3.Should().Be(testValue3, "PerTxGasLimit should store and retrieve the third value correctly");
    }

    [Test]
    public void SetMaxPerTxGasLimit_ToV50Value_StoresCorrectly()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Verify initial value is 0 or default
        ulong initialValue = l2Pricing.PerTxGasLimitStorage.Get();

        // Set to v50 initial value (32M)
        l2Pricing.SetMaxPerTxGasLimit(L2PricingState.InitialPerTxGasLimitV50);

        // Verify it was stored correctly
        ulong storedValue = l2Pricing.PerTxGasLimitStorage.Get();
        storedValue.Should().Be(L2PricingState.InitialPerTxGasLimitV50, "SetMaxPerTxGasLimit should store the v50 initial value (32M) correctly");
        storedValue.Should().Be(32_000_000);
        storedValue.Should().NotBe(initialValue, "Stored value should be different from initial value");
    }
}
