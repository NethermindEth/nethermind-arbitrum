using System.Globalization;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Math;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.State;
using FluentAssertions;
using Nethermind.Arbitrum.Execution;

namespace Nethermind.Arbitrum.Test.Arbos.Storage
{
    internal class L1PricingTests
    {
        [Test]
        [TestCaseSource(nameof(GetL1PricingTests))]
        public void UpdateForBatchPosterSpending_CorrectlyCalculates_FundsDue(L1PricingTestData testItem)
        {
            (ArbosStorage storage, IWorldState worldState) = TestArbosStorage.Create();

            worldState.CreateAccountIfNotExists(TestArbosStorage.DefaultTestAccount, UInt256.Zero, UInt256.One);
            storage.Set(ArbosStateOffsets.VersionOffset, 32);

            ArbosState arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), LimboLogs.Instance.GetLogger(""));

            var posterPayAddress = TestItem.AddressB;
            var rewardsAddress = TestItem.AddressC;

            L1PricingState.Initialize(arbosState.BackingStorage, TestItem.AddressA, testItem.L1BasefeeGwei);

            L1PricingState l1Pricing = new L1PricingState(arbosState.BackingStorage);

            var expectedResult = ExpectedResultsForL1Test(testItem);

            var allPosters = l1Pricing.BatchPosterTable.GetAllPosters(1000);
            var firstPosterAddress = allPosters.First();
            var firstPoster = l1Pricing.BatchPosterTable.OpenPoster(firstPosterAddress, true);
            firstPoster.SetPayTo(posterPayAddress);

            l1Pricing.PerUnitRewardStorage.Set(testItem.UnitReward);
            l1Pricing.PayRewardsToStorage.Set(rewardsAddress);

            var pricerBalance = testItem.FundsCollectedPerSecond * 3;
            var unitsAdded = testItem.UnitsPerSecond * 3;
            worldState.AddToBalanceAndCreateIfNotExists(ArbosAddresses.L1PricerFundsPoolAddress, pricerBalance,
                FullChainSimulationReleaseSpec.Instance);

            l1Pricing.SetL1FeesAvailable(pricerBalance);
            l1Pricing.UnitsSinceStorage.Set(unitsAdded);

            l1Pricing.SetAmortizedCostCapBips(testItem.AmortizationCapBips);

            l1Pricing.UpdateForBatchPosterSpending(1, 3, firstPosterAddress, testItem.FundsSpent, testItem.L1BasefeeGwei * 1.GWei(), arbosState,
                worldState, FullChainSimulationReleaseSpec.Instance);

            //assert
            worldState.GetBalance(rewardsAddress).Should().Be(expectedResult.RewardRecipientBalance);
            l1Pricing.UnitsSinceStorage.Get().Should().Be(expectedResult.UnitsRemaining);
            worldState.GetBalance(posterPayAddress).Should().Be(expectedResult.FundsReceived);
            var fundsWithheld = worldState.GetBalance(ArbosAddresses.L1PricerFundsPoolAddress);
            fundsWithheld.Should().Be(expectedResult.FundsStillHeld);
            fundsWithheld.Should().Be(l1Pricing.L1FeesAvailableStorage.Get());
        }

        [Test(Description = "Verifies if updated L1 price after 10 iterations is within 1% tolerance of the expected price (change in L1 fee). On each step a fixed number of units is processed, no rewards are paid. Note: tests against v3!")]
        [TestCase(1_000_000_000UL, 5_000_000_000UL)]
        [TestCase(5_000_000_000UL, 1_000_000_000UL)]
        [TestCase(2_000_000_000UL, 2_000_000_000UL)]
        public void UpdateForBatchPosterSpending_CorrectlyCalculates_PriceChange(ulong initialL1BasefeeEstimate, ulong equilibriumL1BasefeeEstimate)
        {
            (ArbosStorage storage, IWorldState worldState) = TestArbosStorage.Create();

            worldState.CreateAccountIfNotExists(TestArbosStorage.DefaultTestAccount, UInt256.Zero, UInt256.One);
            storage.Set(ArbosStateOffsets.VersionOffset, 3);

            ArbosState arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), LimboLogs.Instance.GetLogger(""));

            L1PricingState.Initialize(arbosState.BackingStorage, TestItem.AddressA, initialL1BasefeeEstimate);

            L1PricingState l1Pricing = new L1PricingState(arbosState.BackingStorage);
            l1Pricing.PerUnitRewardStorage.Set(0);
            l1Pricing.PricePerUnitStorage.Set(initialL1BasefeeEstimate);
            l1Pricing.EquilibrationUnitsStorage.Set(L1PricingState.InitialEquilibrationUnitsV6);

            for (ulong i = 0; i < 10; i++)
            {
                var unitsToAdd = L1PricingState.InitialEquilibrationUnitsV6;
                l1Pricing.UnitsSinceStorage.Set(l1Pricing.UnitsSinceStorage.Get() + unitsToAdd);

                var feesToAdd = l1Pricing.PricePerUnitStorage.Get() * unitsToAdd;

                ArbitrumTransactionProcessor.MintBalance(ArbosAddresses.L1PricerFundsPoolAddress, feesToAdd, arbosState,
                    worldState, FullChainSimulationReleaseSpec.Instance);

                l1Pricing.UpdateForBatchPosterSpending(10UL * (i + 1), 10UL * (i + 1) + 5, TestItem.AddressB,
                    equilibriumL1BasefeeEstimate * unitsToAdd, equilibriumL1BasefeeEstimate, arbosState,
                    worldState, FullChainSimulationReleaseSpec.Instance);
            }

            //assert
            long expectedMovement = equilibriumL1BasefeeEstimate.ToLongSafe() - initialL1BasefeeEstimate.ToLongSafe();
            var actualPricePerUnit = l1Pricing.PricePerUnitStorage.Get();
            long actualMovement = actualPricePerUnit.ToInt64(CultureInfo.InvariantCulture) - initialL1BasefeeEstimate.ToLongSafe();

            (actualMovement < 0).Should().Be(expectedMovement < 0);

            expectedMovement = long.Abs(expectedMovement);
            actualMovement = long.Abs(actualMovement);
            System.Math.Abs((double)(actualMovement - expectedMovement)).Should().BeLessThanOrEqualTo(expectedMovement * 0.01);
        }

        [Test(Description = "Check returned result when not enough fees are available to transfer. Note - it's not possible to get the same error for version <= 10 as they don't use L1FeesAvailableStorage storage, but always check actual balance on the account")]
        [TestCase(1UL, true, null)]
        [TestCase(9UL, true, null)]
        [TestCase(32UL, false, "insufficient sender balance")]
        public void UpdateForBatchPosterSpending_NotEnoughBalanceForL1Fees_ReturnsCorrectResult(ulong version, bool success, string? error)
        {
            (ArbosStorage storage, IWorldState worldState) = TestArbosStorage.Create();

            worldState.CreateAccountIfNotExists(TestArbosStorage.DefaultTestAccount, UInt256.Zero, UInt256.One);
            storage.Set(ArbosStateOffsets.VersionOffset, version);

            ArbosState arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(), LimboLogs.Instance.GetLogger(""));

            ulong initialL1BasefeeEstimate = 10_000;
            ulong equilibriumL1BasefeeEstimate = 10_000;

            L1PricingState.Initialize(arbosState.BackingStorage, TestItem.AddressA, initialL1BasefeeEstimate);

            L1PricingState l1Pricing = new L1PricingState(arbosState.BackingStorage);
            l1Pricing.PerUnitRewardStorage.Set(0);
            l1Pricing.PricePerUnitStorage.Set(initialL1BasefeeEstimate);
            l1Pricing.EquilibrationUnitsStorage.Set(L1PricingState.InitialEquilibrationUnitsV6);


            var unitsToAdd = L1PricingState.InitialEquilibrationUnitsV6;
            l1Pricing.UnitsSinceStorage.Set(l1Pricing.UnitsSinceStorage.Get() + unitsToAdd);

            var feesToAdd = l1Pricing.PricePerUnitStorage.Get() * unitsToAdd;

            //set L1 available funds enough to fulfill the transfer
            l1Pricing.L1FeesAvailableStorage.Set(feesToAdd);

            //mint only half of the funds on the actual account
            ArbitrumTransactionProcessor.MintBalance(ArbosAddresses.L1PricerFundsPoolAddress, feesToAdd / 2, arbosState,
                worldState, FullChainSimulationReleaseSpec.Instance);

            var updateResult = l1Pricing.UpdateForBatchPosterSpending(10UL, 10UL + 5, TestItem.AddressB,
                equilibriumL1BasefeeEstimate * unitsToAdd, equilibriumL1BasefeeEstimate, arbosState,
                worldState, FullChainSimulationReleaseSpec.Instance);

            //assert
            updateResult.Success.Should().Be(success);
            if (error is not null)
                updateResult.Should().Be(new ArbosStorageUpdateResult(error));
        }

        public static IEnumerable<L1PricingTestData> GetL1PricingTests()
        {
            yield return new L1PricingTestData()
            {
                UnitReward = 10,
                UnitsPerSecond = 78,
                FundsCollectedPerSecond = 7800,
                FundsSpent = 3000,
                AmortizationCapBips = ulong.MaxValue,
                L1BasefeeGwei = 10
            };
            yield return new L1PricingTestData()
            {
                UnitReward = 10,
                UnitsPerSecond = 78,
                FundsCollectedPerSecond = 1313,
                FundsSpent = 3000,
                AmortizationCapBips = ulong.MaxValue,
                L1BasefeeGwei = 10
            };
            yield return new L1PricingTestData()
            {
                UnitReward = 10,
                UnitsPerSecond = 78,
                FundsCollectedPerSecond = 31,
                FundsSpent = 3000,
                AmortizationCapBips = ulong.MaxValue,
                L1BasefeeGwei = 10
            };
            yield return new L1PricingTestData()
            {
                UnitReward = 10,
                UnitsPerSecond = 78,
                FundsCollectedPerSecond = 7800,
                FundsSpent = 3000,
                AmortizationCapBips = 100,
                L1BasefeeGwei = 10
            };
            yield return new L1PricingTestData()
            {
                UnitReward = 0,
                UnitsPerSecond = 78,
                FundsCollectedPerSecond = (ulong)7800.GWei(),
                FundsSpent = (ulong)3000.GWei(),
                AmortizationCapBips = 100,
                L1BasefeeGwei = 10
            };
        }

        internal record L1PricingTestData
        {
            public ulong FundsCollectedPerSecond;
            public ulong AmortizationCapBips;
            public ulong UnitsPerSecond;
            public ulong L1BasefeeGwei;
            public ulong UnitReward;
            public ulong FundsSpent;
        }

        internal record L1TestExpectedResults
        {
            public UInt256 RewardRecipientBalance;
            public ulong UnitsRemaining;
            public UInt256 FundsReceived;
            public UInt256 FundsStillHeld;
        }

        internal static L1TestExpectedResults ExpectedResultsForL1Test(L1PricingTestData input)
        {
            var ret = new L1TestExpectedResults();

            UInt256 availableFunds = 3 * input.FundsCollectedPerSecond;
            UInt256 uncappedAvailableFunds = availableFunds;

            if (input.AmortizationCapBips != 0)
            {
                UInt256 availableFundsCap = (input.UnitsPerSecond * input.L1BasefeeGwei * 1.GWei()) *
                                               (ulong)input.AmortizationCapBips.ToLongSafe() / Utils.BipsMultiplier;

                if (availableFundsCap < availableFunds)
                {
                    availableFunds = availableFundsCap;
                }
            }

            UInt256 fundsWantedForRewards = input.UnitReward * input.UnitsPerSecond;
            UInt256 unitsAllocated = input.UnitsPerSecond;

            ret.RewardRecipientBalance = availableFunds < fundsWantedForRewards ? availableFunds : fundsWantedForRewards;

            availableFunds -= ret.RewardRecipientBalance;
            uncappedAvailableFunds -= ret.RewardRecipientBalance;

            ret.UnitsRemaining = (3 * input.UnitsPerSecond) - (ulong)unitsAllocated;

            UInt256 maxCollectable = input.FundsSpent;

            if (availableFunds < maxCollectable)
            {
                maxCollectable = availableFunds;
            }

            ret.FundsReceived = maxCollectable;
            uncappedAvailableFunds -= maxCollectable;
            ret.FundsStillHeld = uncappedAvailableFunds;

            return ret;
        }
    }
}
