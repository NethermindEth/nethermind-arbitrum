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

namespace Nethermind.Arbitrum.Test.Arbos.Storage
{
    internal class L1PricingTests
    {
        [Test]
        [TestCaseSource(nameof(GetL1PricingTests))]
        public void UpdateForBatchPosterSpending_CorrectlyCalculatesFundsDue(L1PricingTestData testItem)
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

            l1Pricing.UpdateForBatchPosterSpending(1, 3, firstPosterAddress, testItem.FundsSpent, testItem.FundsSpent, arbosState,
                worldState, FullChainSimulationReleaseSpec.Instance);

            //assert
            worldState.GetBalance(rewardsAddress).Should().Be(expectedResult.RewardRecipientBalance);
            l1Pricing.UnitsSinceStorage.Get().Should().Be(expectedResult.UnitsRemaining);
            worldState.GetBalance(posterPayAddress).Should().Be(expectedResult.FundsReceived);
            var fundsWithheld = worldState.GetBalance(ArbosAddresses.L1PricerFundsPoolAddress);
            fundsWithheld.Should().Be(expectedResult.FundsStillHeld);
            fundsWithheld.Should().Be(l1Pricing.L1FeesAvailableStorage.Get());
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
                UnitReward = 10,
                UnitsPerSecond = 78,
                FundsCollectedPerSecond = (ulong)7800.GWei(),
                FundsSpent = (ulong)3000.GWei(),
                AmortizationCapBips = ulong.MaxValue,
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
                                               (ulong)input.AmortizationCapBips.ToLongSafe() * Utils.BipsMultiplier;

                if (availableFundsCap < availableFunds)
                {
                    availableFunds = availableFundsCap;
                }
            }

            UInt256 fundsWantedForRewards = input.UnitReward * input.UnitsPerSecond;
            UInt256 unitsAllocated = input.UnitsPerSecond;

            if (availableFunds < fundsWantedForRewards)
            {
                ret.RewardRecipientBalance = availableFunds;
            }
            else
            {
                ret.RewardRecipientBalance = fundsWantedForRewards;
            }

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
