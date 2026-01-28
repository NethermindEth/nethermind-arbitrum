using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Test.Arbos.Storage;

public class AddressSetTests
{
    [Test]
    public void Initialize_NewSet_SetsSizeToZero()
    {
        using var disposable = TestArbosStorage.Create(out TestArbitrumWorldState state, out ArbosStorage storage);
        AddressSet.Initialize(storage);

        storage.GetULong(0).Should().Be(0);
        state.SetRecords.Should().HaveCount(1);
    }

    [Test]
    public void Size_EmptySet_ReturnsZero()
    {
        using var disposable = TestArbosStorage.Create(out _, out ArbosStorage storage);
        AddressSet.Initialize(storage);
        AddressSet addressSet = new(storage);

        addressSet.Size().Should().Be(0);
    }

    [TestCase(1u)]
    [TestCase(2u)]
    [TestCase(3u)]
    public void Size_HasMember_ReturnsCorrectSize(ulong size)
    {
        using var disposable = TestArbosStorage.Create(out _, out ArbosStorage storage);
        AddressSet.Initialize(storage);
        AddressSet addressSet = new(storage);

        for (ulong i = 0; i < size; i++)
        {
            addressSet.Add(new Address(RandomNumberGenerator.GetBytes(Address.Size)));
        }

        addressSet.Size().Should().Be(size);
    }

    [Test]
    public void IsMember_EmptySet_ReturnsFalse()
    {
        using var disposable = TestArbosStorage.Create(out _, out ArbosStorage storage);
        AddressSet.Initialize(storage);
        AddressSet addressSet = new(storage);

        Address address = new(RandomNumberGenerator.GetBytes(Address.Size));

        addressSet.IsMember(address).Should().BeFalse();
    }

    [Test]
    public void IsMember_HasMember_ReturnsTrue()
    {
        using var disposable = TestArbosStorage.Create(out _, out ArbosStorage storage);
        AddressSet.Initialize(storage);
        AddressSet addressSet = new(storage);

        Address address = new(RandomNumberGenerator.GetBytes(Address.Size));
        addressSet.Add(address);

        addressSet.IsMember(address).Should().BeTrue();
    }

    [Test]
    public void AllMembers_EmptySet_ReturnsEmptyCollection()
    {
        using var disposable = TestArbosStorage.Create(out _, out ArbosStorage storage);
        AddressSet.Initialize(storage);
        AddressSet addressSet = new(storage);

        addressSet.AllMembers(10).Should().BeEmpty();
    }

    [TestCase(5, 3)]
    [TestCase(5, 6)]
    public void AllMembers_QueryWithConstraint_ReturnsCorrectNumber(byte totalMembers, byte queryCount)
    {
        using var disposable = TestArbosStorage.Create(out _, out ArbosStorage storage);
        AddressSet.Initialize(storage);
        AddressSet addressSet = new(storage);

        var addresses = Enumerable.Range(0, totalMembers).Select(_ => new Address(RandomNumberGenerator.GetBytes(Address.Size))).ToList();
        foreach (Address address in addresses)
        {
            addressSet.Add(address);
        }

        var members = addressSet.AllMembers(queryCount);

        members.Should().BeEquivalentTo(addresses.Take(queryCount));
    }

    [Test]
    public void ClearList_HasMembers_ClearsAllMembers()
    {
        using var disposable = TestArbosStorage.Create(out _, out ArbosStorage storage);
        AddressSet.Initialize(storage);
        AddressSet addressSet = new(storage);

        addressSet.Add(new Address(RandomNumberGenerator.GetBytes(Address.Size)));
        addressSet.Add(new Address(RandomNumberGenerator.GetBytes(Address.Size)));

        addressSet.ClearList();

        addressSet.Size().Should().Be(0);
        addressSet.AllMembers(10).Should().BeEmpty();
    }

    [Test]
    public void Remove_NoSuchMember_DoesNothing()
    {
        using var disposable = TestArbosStorage.Create(out _, out ArbosStorage storage);
        AddressSet.Initialize(storage);
        AddressSet addressSet = new(storage);

        Address address = new(RandomNumberGenerator.GetBytes(Address.Size));
        addressSet.Remove(address, 1);

        addressSet.Size().Should().Be(0);
        addressSet.IsMember(address).Should().BeFalse();
    }

    [Test]
    public void Remove_HasMember_RemovesMember()
    {
        using var disposable = TestArbosStorage.Create(out _, out ArbosStorage storage);
        AddressSet.Initialize(storage);
        AddressSet addressSet = new(storage);

        Address address = new(RandomNumberGenerator.GetBytes(Address.Size));
        addressSet.Add(address);
        addressSet.Remove(address, 1);

        addressSet.Size().Should().Be(0);
        addressSet.IsMember(address).Should().BeFalse();
    }

    [Test]
    public void Remove_HasMultipleMembers_ReordersAfterRemoval()
    {
        using var disposable = TestArbosStorage.Create(out _, out ArbosStorage storage);
        AddressSet.Initialize(storage);
        AddressSet addressSet = new(storage);

        var addresses = Enumerable.Range(0, 3).Select(_ => new Address(RandomNumberGenerator.GetBytes(Address.Size))).ToList();
        foreach (Address address in addresses)
        {
            addressSet.Add(address);
        }

        addressSet.Remove(addresses[0], 1); // Remove the first address

        addressSet.Size().Should().Be(2);
        addressSet.AllMembers(3).Should().BeEquivalentTo([addresses[2], addresses[1]]); // Last address fills the gap of the removed
    }

    [Test]
    public void Remove_RemoveAllMembers_ClearsAllMembers()
    {
        using var disposable = TestArbosStorage.Create(out _, out ArbosStorage storage);
        AddressSet.Initialize(storage);
        AddressSet addressSet = new(storage);

        var addresses = Enumerable.Range(0, 5).Select(_ => new Address(RandomNumberGenerator.GetBytes(Address.Size))).ToList();
        foreach (Address address in addresses)
        {
            addressSet.Add(address);
        }

        foreach (Address address in addresses)
        {
            addressSet.Remove(address, 1);
        }

        addressSet.Size().Should().Be(0);
        addressSet.AllMembers(10).Should().BeEmpty();
    }

    [Test]
    public void RectifyMapping_AddressIsNotMember_Throws()
    {
        using var disposable = TestArbosStorage.Create(out _, out ArbosStorage storage);
        AddressSet.Initialize(storage);
        AddressSet addressSet = new(storage);

        Address address = new(RandomNumberGenerator.GetBytes(Address.Size));

        var remove = () => addressSet.RectifyMapping(address);
        remove.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void RectifyMapping_MappingIsCorrect_Throws()
    {
        using var disposable = TestArbosStorage.Create(out _, out ArbosStorage storage);
        AddressSet.Initialize(storage);
        AddressSet addressSet = new(storage);

        Address address = new(RandomNumberGenerator.GetBytes(Address.Size));
        addressSet.Add(address);

        var remove = () => addressSet.RectifyMapping(address);
        remove.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void RectifyMapping_MappingIsIncorrect_CorrectsMapping()
    {
        using var disposable = TestArbosStorage.Create(out _, out ArbosStorage storage);
        AddressSet.Initialize(storage);
        AddressSet addressSet = new(storage);

        Address address1 = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address address2 = new(RandomNumberGenerator.GetBytes(Address.Size));
        addressSet.Add(address1);
        addressSet.Add(address2);

        // Remove with ArbOS version 1, which had a bug in the mapping
        addressSet.Remove(address1, 1);

        // Fixes the mapping
        addressSet.RectifyMapping(address2);

        // Yeah, first address is removed, but RectifyMapping increments the size
        addressSet.Size().Should().Be(2);

        // Address2 is still a member, and address1 is not
        addressSet.IsMember(address1).Should().BeFalse();
        addressSet.IsMember(address2).Should().BeTrue();
    }
}
