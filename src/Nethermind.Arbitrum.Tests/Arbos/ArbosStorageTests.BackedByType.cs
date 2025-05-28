using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Tests.Infrastructure;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Tests.Arbos;

public partial class ArbosStorageTests
{
    [TestCase(0ul, 0u)]
    [TestCase(1ul, 1u)]
    [TestCase(9ul, 9u)]
    [TestCase(10ul, 100u)]
    [TestCase(11ul, uint.MaxValue)]
    [TestCase(ulong.MaxValue, uint.MaxValue)]
    public void GetSetStorageBackedByUInt_Always_SetsAndGetsTheSameValue(ulong offset, uint value)
    {
        TrackingWorldState worldState = CreateWorldState();
        ArbosStorage storage = new(worldState, new SystemBurner(Logger), TestAccount);
        ArbosStorageBackedUInt backedStorage = new(storage, offset);

        backedStorage.Set(value);

        uint actual = backedStorage.Get();
        actual.Should().Be(value);
    }

    [TestCase(0ul, 0u)]
    [TestCase(1ul, 1u)]
    [TestCase(9ul, 9u)]
    [TestCase(10ul, 100u)]
    [TestCase(11ul, ulong.MaxValue)]
    [TestCase(ulong.MaxValue, ulong.MaxValue)]
    public void GetSetStorageBackedByULong_Always_SetsAndGetsTheSameValue(ulong offset, ulong value)
    {
        TrackingWorldState worldState = CreateWorldState();
        ArbosStorage storage = new(worldState, new SystemBurner(Logger), TestAccount);
        ArbosStorageBackedULong backedStorage = new(storage, offset);

        backedStorage.Set(value);

        ulong actual = backedStorage.Get();
        actual.Should().Be(value);
    }

    [Test]
    public void IncrementStorageBackedByULong_Always_SetsAndGetsTheSameValue()
    {
        TrackingWorldState worldState = CreateWorldState();
        ArbosStorage storage = new(worldState, new SystemBurner(Logger), TestAccount);
        ArbosStorageBackedULong backedStorage = new(storage, 10);

        backedStorage.Get().Should().Be(0);
        backedStorage.Increment();
        backedStorage.Get().Should().Be(1);
        backedStorage.Increment();
        backedStorage.Get().Should().Be(2);
    }

    [TestCase(0ul, "0")]
    [TestCase(1ul, "1")]
    [TestCase(9ul, "9")]
    [TestCase(10ul, "100")]
    [TestCase(11ul, "18446744073709551615")] // ulong.MaxValue
    [TestCase(ulong.MaxValue, "18446744073709551615999999999999")] // much larger than ulong.MaxValue
    public void GetSetStorageBackedByUInt256_Always_SetsAndGetsTheSameValue(ulong offset, string rawValue)
    {
        TrackingWorldState worldState = CreateWorldState();
        ArbosStorage storage = new(worldState, new SystemBurner(Logger), TestAccount);
        ArbosStorageBackedUInt256 backedStorage = new(storage, offset);
        UInt256 value = UInt256.Parse(rawValue);

        backedStorage.Set(value);

        UInt256 actual = backedStorage.Get();
        actual.Should().Be(value);
    }

    [TestCase(0ul, "0x0000000000000000000000000000000000000000")]
    [TestCase(99ul, "0xffffffffffffffffffffffffffffffffffffffff")]
    [TestCase(100ul, "0x123456ffffffffffffffffffffffffffffffffff")]
    public void GetSetStorageBackedByAddress_Always_SetsAndGetsTheSameValue(ulong offset, string rawAddress)
    {
        TrackingWorldState worldState = CreateWorldState();
        ArbosStorage storage = new(worldState, new SystemBurner(Logger), TestAccount);
        ArbosStorageBackedAddress backedStorage = new(storage, offset);
        Address value = new(rawAddress);

        backedStorage.Set(value);

        Address actual = backedStorage.Get();
        actual.Should().Be(value);
    }

    [TestCase(1, 4)]
    [TestCase(2, 7)]
    [TestCase(3, 32)]
    [TestCase(4, 100)]
    [TestCase(5, 200)]
    public void GetSetStorageBackedByBytes_Always_SetsAndGetsTheSameValue(byte offset, int length)
    {
        TrackingWorldState worldState = CreateWorldState();
        ArbosStorage storage = new(worldState, new SystemBurner(Logger), TestAccount);
        ArbosStorageBackedBytes backedStorage = new(storage.OpenSubStorage([offset]));
        byte[] value = RandomNumberGenerator.GetBytes(length);

        backedStorage.Set(value);

        byte[] actual = backedStorage.Get();
        actual.Should().BeEquivalentTo(value);
    }
}
