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

        BatchPoster poster = postersTable.AddPoster(posterAddress, payToAddress);

        poster.FundsDue.Should().Be(0);
        poster.PayTo.Should().Be(payToAddress);
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

        BatchPoster poster = postersTable.OpenPoster(posterAddress, false);

        poster.FundsDue.Should().Be(0);
        poster.PayTo.Should().Be(payToAddress);
    }

    [Test]
    public void OpenPoster_NoPosterAndAutocreate_CreatesPoster()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create();
        BatchPostersTable.Initialize(storage);
        BatchPostersTable postersTable = new(storage);

        Address posterAddress = new(RandomNumberGenerator.GetBytes(20));
        BatchPoster poster = postersTable.OpenPoster(posterAddress, true);

        poster.FundsDue.Should().Be(0);
        poster.PayTo.Should().Be(posterAddress);
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
}
