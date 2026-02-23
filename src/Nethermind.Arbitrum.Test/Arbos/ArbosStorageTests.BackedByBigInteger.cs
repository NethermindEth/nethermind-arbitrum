// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test;
using System.Numerics;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Test.Arbos;

public partial class ArbosStorageTests
{
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
    public void SetCheckedStorageBackedByBigInteger_Always_SetsAndGetsTheSameValue(string strValue, string expectedHash)
    {
        const ulong offset = 0ul;
        using var disposable = TestArbosStorage.Create(out _, out ArbosStorage storage, TestAccount);
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
    public void SetCheckedStorageBackedByBigInteger_Overflow_Throws(string strValue)
    {
        const ulong offset = 0ul;
        using var disposable = TestArbosStorage.Create(out _, out ArbosStorage storage, TestAccount);
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
    public void SetSaturatingStorageBackedByBigInteger_Always_SetsAndGetsTheSameValue(string strValue, string expectedHash)
    {
        const ulong offset = 0ul;
        using var disposable = TestArbosStorage.Create(out _, out ArbosStorage storage, TestAccount);
        ArbosStorageBackedBigInteger backedStorage = new(storage, offset);

        BigInteger value = BigInteger.Parse(strValue);
        bool saturated = backedStorage.SetSaturating(value);

        BigInteger storedBigInteger = backedStorage.Get();
        ValueHash256 storedHash = storage.Get(offset);

        saturated.Should().BeFalse();
        storedBigInteger.Should().Be(value);
        storedHash.Should().Be(new ValueHash256(expectedHash));
    }

    [TestCase("-57896044618658097711785492504343953926634992332820282019728792003956564819969")] // -2^255 - 1
    [TestCase("-115792089237316195423570985008687907853269984665640564039457584007913129639936")] // (-2^255 - 1) * 2
    [TestCase("min")]
    public void SetSaturatingStorageBackedByBigInteger_Underflow_UsesMaxUInt256Value(string strValue)
    {
        ValueHash256 expectedHash = new("0x8000000000000000000000000000000000000000000000000000000000000000");
        BigInteger expectedValue = ArbosStorageBackedBigInteger.TwoToThe255 * -1;

        const ulong offset = 0ul;
        using var disposable = TestArbosStorage.Create(out _, out ArbosStorage storage, TestAccount);
        ArbosStorageBackedBigInteger backedStorage = new(storage, offset);

        BigInteger value = strValue == "min"
            ? BigInteger.Pow(2, 1024) * -1
            : BigInteger.Parse(strValue);

        bool saturated = backedStorage.SetSaturating(value);

        ValueHash256 storedHash = storage.Get(offset);
        BigInteger storedBigInteger = backedStorage.Get();

        saturated.Should().BeTrue();
        storedHash.Should().Be(expectedHash);
        storedBigInteger.Should().Be(expectedValue);
    }

    [TestCase("57896044618658097711785492504343953926634992332820282019728792003956564819968")] // 2^255
    [TestCase("115792089237316195423570985008687907853269984665640564039457584007913129639934")] // 2^256 * 2
    [TestCase("max")]
    public void SetSaturatingStorageBackedByBigInteger_Overflow_UsesMaxUInt256MinusOneValue(string strValue)
    {
        ValueHash256 expectedHash = new("0x7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");
        BigInteger expectedValue = ArbosStorageBackedBigInteger.TwoToThe255MinusOne;

        const ulong offset = 0ul;
        using var disposable = TestArbosStorage.Create(out _, out ArbosStorage storage, TestAccount);
        ArbosStorageBackedBigInteger backedStorage = new(storage, offset);

        BigInteger value = strValue == "max"
            ? BigInteger.Pow(2, 1024) - 1
            : BigInteger.Parse(strValue);

        bool saturated = backedStorage.SetSaturating(value);

        ValueHash256 storedHash = storage.Get(offset);
        BigInteger storedBigInteger = backedStorage.Get();

        saturated.Should().BeTrue();
        storedHash.Should().Be(expectedHash);
        storedBigInteger.Should().Be(expectedValue);
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
    public void SetPreVersion7StorageBackedByBigInteger_Always_HandlesNegativeValuesIncorrectly(string strValue, string expectedHash)
    {
        const ulong offset = 0ul;
        using var disposable = TestArbosStorage.Create(out _, out ArbosStorage storage, TestAccount);
        ArbosStorageBackedBigInteger backedStorage = new(storage, offset);

        BigInteger value = BigInteger.Parse(strValue);
        backedStorage.SetPreVersion7(value);

        ValueHash256 storedHash = storage.Get(offset);
        storedHash.Should().Be(new ValueHash256(expectedHash));
    }

    [TestCase(0ul)]
    [TestCase(1ul)]
    [TestCase(9ul)]
    [TestCase(100ul)]
    [TestCase(100000ul)]
    [TestCase(ulong.MaxValue)]
    public void SetStorageBackedByBigInteger_Always_SetsAndGetsTheSameValue(ulong value)
    {
        const ulong offset = 0ul;
        using var disposable = TestArbosStorage.Create(out _, out ArbosStorage storage, TestAccount);
        ArbosStorageBackedBigInteger backedStorage = new(storage, offset);

        backedStorage.Set(value);

        BigInteger actual = backedStorage.Get();
        actual.Should().Be(value);
    }
}
