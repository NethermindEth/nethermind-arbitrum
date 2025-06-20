using System.Numerics;
using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Test.Arbos.Storage;

public class BatchPostersTableTests
{
    [Test]
    public void Initialize_NewTable_InitializesTotalFundsDueAndAddressSet()
    {
        (ArbosStorage storage, TrackingWorldState state) = TestArbosStorage.Create();

        BatchPostersTable.Initialize(storage);

        storage.GetULong(0).Should().Be(0); // Total funds due
        storage.OpenSubStorage([0]).GetULong(0).Should().Be(0); // Address set
        state.SetRecords.Should().HaveCount(2);
    }

    [Test]
    public void AddPoster_NewPoster_AddsPosterAndInitializesStorage()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        BatchPostersTable.Initialize(storage);
        BatchPostersTable postersTable = new(storage);

        Address posterAddress = new(RandomNumberGenerator.GetBytes(20));
        Address payToAddress = new(RandomNumberGenerator.GetBytes(20));

        BatchPostersTable.BatchPoster poster = postersTable.AddPoster(posterAddress, payToAddress);

        poster.GetFundsDue().Should().Be(0);
        poster.GetPayTo().Should().Be(payToAddress);
    }

    [Test]
    public void AddPoster_ExistingPoster_Throws()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        BatchPostersTable.Initialize(storage);
        BatchPostersTable postersTable = new(storage);

        Address posterAddress = new(RandomNumberGenerator.GetBytes(20));
        Address payToAddress = new(RandomNumberGenerator.GetBytes(20));

        postersTable.AddPoster(posterAddress, payToAddress);

        Action act = () => postersTable.AddPoster(posterAddress, payToAddress);
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ContainsPoster_NoPoster_ReturnsFalse()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        BatchPostersTable.Initialize(storage);
        BatchPostersTable postersTable = new(storage);

        postersTable.ContainsPoster(new(RandomNumberGenerator.GetBytes(20))).Should().BeFalse();
    }

    [Test]
    public void ContainsPoster_HasPoster_ReturnsTrue()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        BatchPostersTable.Initialize(storage);
        BatchPostersTable postersTable = new(storage);

        Address posterAddress = new(RandomNumberGenerator.GetBytes(20));
        Address payToAddress = new(RandomNumberGenerator.GetBytes(20));
        postersTable.AddPoster(posterAddress, payToAddress);

        postersTable.ContainsPoster(posterAddress).Should().BeTrue();
    }

    [Test]
    public void GetAllPosters_EmptyTable_ReturnsEmptyCollection()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        BatchPostersTable.Initialize(storage);
        BatchPostersTable postersTable = new(storage);

        postersTable.GetAllPosters(10).Should().BeEmpty();
    }

    [TestCase(3u, 2u)]
    [TestCase(3u, 5u)]
    public void GetAllPosters_HasPosters_ReturnsCorrectCount(ulong numPosters, ulong maxNumToReturn)
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        BatchPostersTable.Initialize(storage);
        BatchPostersTable postersTable = new(storage);

        List<Address> posters = new();
        for (ulong i = 0; i < numPosters; i++)
        {
            posters.Add(new(RandomNumberGenerator.GetBytes(20)));
            postersTable.AddPoster(posters[^1], new(RandomNumberGenerator.GetBytes(20)));
        }

        var allPosters = postersTable.GetAllPosters(maxNumToReturn);
        allPosters.Should().BeEquivalentTo(posters.Take((int)maxNumToReturn));
    }

    [Test]
    public void OpenPoster_HasPoster_ReturnsPoster()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        BatchPostersTable.Initialize(storage);
        BatchPostersTable postersTable = new(storage);

        Address posterAddress = new(RandomNumberGenerator.GetBytes(20));
        Address payToAddress = new(RandomNumberGenerator.GetBytes(20));
        postersTable.AddPoster(posterAddress, payToAddress);

        var poster = postersTable.OpenPoster(posterAddress, false);

        poster.GetFundsDue().Should().Be(0);
        poster.GetPayTo().Should().Be(payToAddress);
    }

    [Test]
    public void OpenPoster_NoPosterAndAutocreate_CreatesPoster()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        BatchPostersTable.Initialize(storage);
        BatchPostersTable postersTable = new(storage);

        Address posterAddress = new(RandomNumberGenerator.GetBytes(20));
        var poster = postersTable.OpenPoster(posterAddress, true);

        poster.GetFundsDue().Should().Be(0);
        poster.GetPayTo().Should().Be(posterAddress);
    }

    [Test]
    public void OpenPoster_NoPosterDontAutocreate_Throws()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        BatchPostersTable.Initialize(storage);
        BatchPostersTable postersTable = new(storage);

        Address posterAddress = new(RandomNumberGenerator.GetBytes(20));

        Action act = () => postersTable.OpenPoster(posterAddress, false);
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void SetFundsDueSaturating_Always_SetsPostersFundsDueAndUpdatesTotal()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        BatchPostersTable.Initialize(storage);
        BatchPostersTable postersTable = new(storage);

        Address posterAddress = new(RandomNumberGenerator.GetBytes(20));
        var poster = postersTable.OpenPoster(posterAddress, true);

        const ulong fundsDue = 100;
        poster.SetFundsDueSaturating(fundsDue);

        poster.GetFundsDue().Should().Be(fundsDue);
        postersTable.GetTotalFundsDue().Should().Be(fundsDue);
    }

    [Test]
    public void SetFundsDueSaturating_ChangePostersFundsDue_AppliesChangeCorrectly()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        BatchPostersTable.Initialize(storage);
        BatchPostersTable postersTable = new(storage);

        Address posterAddress = new(RandomNumberGenerator.GetBytes(20));
        var poster = postersTable.OpenPoster(posterAddress, true);

        poster.SetFundsDueSaturating(100);
        poster.SetFundsDueSaturating(120);

        poster.GetFundsDue().Should().Be(120);
        postersTable.GetTotalFundsDue().Should().Be(120);
    }

    [Test]
    public void SetFundsDueSaturating_MultiplePostersFundsDue_CalculatesTotalCorrectly()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        BatchPostersTable.Initialize(storage);
        BatchPostersTable postersTable = new(storage);

        var poster1 = postersTable.OpenPoster(new(RandomNumberGenerator.GetBytes(20)), true);
        var poster2 = postersTable.OpenPoster(new(RandomNumberGenerator.GetBytes(20)), true);

        poster1.SetFundsDueSaturating(100);
        poster2.SetFundsDueSaturating(120);

        postersTable.GetTotalFundsDue().Should().Be(220);
    }

    [Test]
    public void SetFundsDueSaturating_SaturatedFundsDue_SetsSaturatedValue()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        BatchPostersTable.Initialize(storage);
        BatchPostersTable postersTable = new(storage);

        var poster1 = postersTable.OpenPoster(new(RandomNumberGenerator.GetBytes(20)), true);
        var poster2 = postersTable.OpenPoster(new(RandomNumberGenerator.GetBytes(20)), true);

        BigInteger almostSaturatedFundsDue = ArbosStorageBackedBigInteger.MaxValue;
        bool saturatedAfterPoster1 = poster1.SetFundsDueSaturating(almostSaturatedFundsDue);
        bool saturatedAfterPoster2 = poster2.SetFundsDueSaturating(1); // Adds 1 to max value to saturate it.

        saturatedAfterPoster1.Should().BeFalse();
        saturatedAfterPoster2.Should().BeTrue();
        postersTable.GetTotalFundsDue().Should().Be(almostSaturatedFundsDue); // Saturated value.
    }
}
