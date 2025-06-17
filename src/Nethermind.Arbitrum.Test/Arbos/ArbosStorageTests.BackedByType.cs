using System.Numerics;
using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Arbos;

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
        (ArbosStorage storage, _) = TestArbosStorage.Create(TestAccount);
        ArbosStorageBackedUInt backedStorage = new(storage, offset);

        backedStorage.Set(value);

        uint actual = backedStorage.Get();
        actual.Should().Be(value);
    }

    [TestCase(0ul, 0ul)]
    [TestCase(1ul, 1ul)]
    [TestCase(9ul, 9ul)]
    [TestCase(10ul, 100ul)]
    [TestCase(11ul, ulong.MaxValue)]
    [TestCase(ulong.MaxValue, ulong.MaxValue)]
    public void GetSetStorageBackedByULong_Always_SetsAndGetsTheSameValue(ulong offset, ulong value)
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create(TestAccount);
        ArbosStorageBackedULong backedStorage = new(storage, offset);

        backedStorage.Set(value);

        ulong actual = backedStorage.Get();
        actual.Should().Be(value);
    }

    [Test]
    public void IncrementStorageBackedByULong_IncrementULongMax_Throws()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create(TestAccount);
        ArbosStorageBackedULong backedStorage = new(storage, 10);

        backedStorage.Set(ulong.MaxValue);

        var underflow = () => backedStorage.Increment();
        underflow.Should().Throw<OverflowException>();
    }

    [Test]
    public void IncrementStorageBackedByULong_Always_IncrementsCorrectly()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create(TestAccount);
        ArbosStorageBackedULong backedStorage = new(storage, 10);

        backedStorage.Get().Should().Be(0);
        backedStorage.Increment().Should().Be(1);
        backedStorage.Get().Should().Be(1);
        backedStorage.Increment().Should().Be(2);
        backedStorage.Get().Should().Be(2);
    }

    [Test]
    public void DecrementStorageBackedByULong_DecrementZero_Throws()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create(TestAccount);
        ArbosStorageBackedULong backedStorage = new(storage, 10);

        var underflow = () => backedStorage.Decrement();
        underflow.Should().Throw<OverflowException>();
    }

    [Test]
    public void DecrementStorageBackedByULong_Always_DecrementsCorrectly()
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create(TestAccount);
        ArbosStorageBackedULong backedStorage = new(storage, 10);

        backedStorage.Set(3);

        backedStorage.Get().Should().Be(3);
        backedStorage.Decrement().Should().Be(2);
        backedStorage.Get().Should().Be(2);
        backedStorage.Decrement().Should().Be(1);
        backedStorage.Get().Should().Be(1);
    }

    [TestCase(0ul, "0")]
    [TestCase(1ul, "1")]
    [TestCase(9ul, "9")]
    [TestCase(10ul, "100")]
    [TestCase(11ul, "18446744073709551615")] // ulong.MaxValue
    [TestCase(ulong.MaxValue, "18446744073709551615999999999999")] // much larger than ulong.MaxValue
    public void GetSetStorageBackedByUInt256_Always_SetsAndGetsTheSameValue(ulong offset, string rawValue)
    {
        (ArbosStorage storage, _) = TestArbosStorage.Create(TestAccount);
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
        (ArbosStorage storage, _) = TestArbosStorage.Create(TestAccount);
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
        (ArbosStorage storage, _) = TestArbosStorage.Create(TestAccount);
        ArbosStorageBackedBytes backedStorage = new(storage.OpenSubStorage([offset]));
        byte[] value = RandomNumberGenerator.GetBytes(length);

        backedStorage.Set(value);

        byte[] actual = backedStorage.Get();
        actual.Should().BeEquivalentTo(value);
    }

    // Test hashes are captured from Nitro's tests
    [TestCase("0", "0x0000000000000000000000000000000000000000000000000000000000000000")]
    [TestCase("1", "0x0000000000000000000000000000000000000000000000000000000000000001")]
    [TestCase("33", "0x0000000000000000000000000000000000000000000000000000000000000021")]
    [TestCase("31591083", "0x0000000000000000000000000000000000000000000000000000000001e20aab")]
    [TestCase("-1", "0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")]
    [TestCase("-33", "0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffdf")]
    [TestCase("-31591083", "0xfffffffffffffffffffffffffffffffffffffffffffffffffffffffffe1df555")]
    [TestCase(
        "57896044618658097711785492504343953926634992332820282019728792003956564819967",
        "0x7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")] // 2^255 - 1
    [TestCase(
        "-57896044618658097711785492504343953926634992332820282019728792003956564819968",
        "0x8000000000000000000000000000000000000000000000000000000000000000")] // -2^255
    public void GetSetStorageBackedByBigInteger_Always_SetsAndGetsTheSameValue(string strValue, string expectedHash)
    {
        const ulong offset = 0ul;
        (ArbosStorage storage, _) = TestArbosStorage.Create(TestAccount);
        ArbosStorageBackedBigInteger backedStorage = new(storage, offset);

        BigInteger value = BigInteger.Parse(strValue);
        backedStorage.SetChecked(value);

        BigInteger storedBigInteger = backedStorage.Get();
        ValueHash256 storedHash = storage.Get(offset);

        storedBigInteger.Should().Be(value);
        storedHash.Should().Be(new ValueHash256(expectedHash));
    }

    [TestCase("57896044618658097711785492504343953926634992332820282019728792003956564819968")] // 2^255
    [TestCase("-57896044618658097711785492504343953926634992332820282019728792003956564819969")] // -2^255 - 1
    [TestCase("115792089237316195423570985008687907853269984665640564039457584007913129639934")] // 2^256 * 2
    [TestCase("-115792089237316195423570985008687907853269984665640564039457584007913129639936")] // (-2^255 - 1) * 2
    [TestCase("max")]
    [TestCase("min")]
    public void SetCheckedBackedByBigInteger_Overflow_Throws(string strValue)
    {
        const ulong offset = 0ul;
        (ArbosStorage storage, _) = TestArbosStorage.Create(TestAccount);
        ArbosStorageBackedBigInteger backedStorage = new(storage, offset);

        BigInteger value = strValue switch
        {
            "max" => BigInteger.Pow(2, 1024) - 1,
            "min" => BigInteger.Pow(2, 1024) * -1,
            _ => BigInteger.Parse(strValue)
        };

        Action action = () => backedStorage.SetChecked(value);
        action.Should().Throw<OverflowException>();
    }

    // Test hashes are captured from Nitro's tests
    [TestCase("0", "0x0000000000000000000000000000000000000000000000000000000000000000")]
    [TestCase("1", "0x0000000000000000000000000000000000000000000000000000000000000001")]
    [TestCase("33", "0x0000000000000000000000000000000000000000000000000000000000000021")]
    [TestCase("31591083", "0x0000000000000000000000000000000000000000000000000000000001e20aab")]
    [TestCase("-1", "0x0000000000000000000000000000000000000000000000000000000000000001")]
    [TestCase("-33", "0x0000000000000000000000000000000000000000000000000000000000000021")]
    [TestCase("-31591083", "0x0000000000000000000000000000000000000000000000000000000001e20aab")]
    [TestCase(
        "57896044618658097711785492504343953926634992332820282019728792003956564819967",
        "0x7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")]
    [TestCase(
        "-57896044618658097711785492504343953926634992332820282019728792003956564819968",
        "0x8000000000000000000000000000000000000000000000000000000000000000")]
    public void SetPreVersion7BackedByBigInteger_Always_HandlesNegativeValuesIncorrectly(string strValue, string expectedHash)
    {
        const ulong offset = 0ul;
        (ArbosStorage storage, _) = TestArbosStorage.Create(TestAccount);
        ArbosStorageBackedBigInteger backedStorage = new(storage, offset);

        BigInteger value = BigInteger.Parse(strValue);
        backedStorage.SetPreVersion7(value);

        ValueHash256 storedHash = storage.Get(offset);
        storedHash.Should().Be(new ValueHash256(expectedHash));
    }
}
