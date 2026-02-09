// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Numerics;
using FluentAssertions;
using Nethermind.Arbitrum.Math;

namespace Nethermind.Arbitrum.Test.Data;

[TestFixture]
public class MathExtensionsTests
{
    [TestCase(10, 3, 3)]
    [TestCase(10, -3, -3)]
    [TestCase(-10, 3, -4)]
    [TestCase(-10, -3, 4)]
    [TestCase(0, 3, 0)]
    [TestCase(0, -3, 0)]
    [TestCase(10, 1, 10)]
    [TestCase(10, -1, -10)]
    [TestCase(-10, 1, -10)]
    [TestCase(-10, -1, 10)]
    [TestCase(-7, 3, -3)]
    [TestCase(7, 3, 2)]
    public void FloorDiv_Always_ReturnsExpectedResult(long x, long y, long expected)
    {
        var result = Utils.FloorDiv(new BigInteger(x), new BigInteger(y));
        result.Should().Be(new BigInteger(expected));
    }

    [Test]
    public void FloorDiv_WhenYIsZero_ThrowsException()
    {
        Action act = () => Utils.FloorDiv(new BigInteger(10), BigInteger.Zero);
        act.Should().Throw<DivideByZeroException>();
    }
}
