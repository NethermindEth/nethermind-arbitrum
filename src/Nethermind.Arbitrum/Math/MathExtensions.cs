// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Numerics;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Math
{
    public static class Utils
    {
        public const ulong BipsMultiplier = 10_000;
        public const uint MaxUint24 = (1 << 24) - 1; // 2^24 - 1

        public static ulong Div32Ceiling(ulong value)
        {
            ulong rem = value & 31;
            value >>= 5;
            if (rem > 0)
            {
                value++;
            }

            return value;
        }

        public static T DivCeiling<T>(T @this, T other)
            where T : IUnsignedNumber<T>, IDivisionOperators<T, T, T>, IModulusOperators<T, T, T>
        {
            if (T.IsZero(other))
                throw new DivideByZeroException();

            T remainder = @this % other;
            T quotient = @this / other;

            return T.IsZero(remainder) ? quotient : quotient + T.One;
        }

        public static UInt256 SaturateMul(this UInt256 @this, UInt256 other)
        {
            bool overflows = UInt256.MultiplyOverflow(@this, other, out other);
            if (overflows)
                other = UInt256.MaxValue;
            return other;
        }

        public static T SaturateMul<T>(this T @this, T other)
            where T : IUnsignedNumber<T>, IMinMaxValue<T>, IComparisonOperators<T, T, bool>
        {
            if (T.IsZero(@this) || T.IsZero(other))
                return T.Zero;

            if (@this > T.MaxValue / other)
                return T.MaxValue;

            return @this * other;
        }

        public static T SaturateSub<T>(this T @this, T other)
            where T : INumber<T>, ISubtractionOperators<T, T, T>, IComparisonOperators<T, T, bool>
        {
            if (other >= @this)
                return T.Zero;

            return @this - other;
        }

        public static T SaturateAdd<T>(this T @this, T other)
            where T : IUnsignedNumber<T>, IMinMaxValue<T>, IAdditionOperators<T, T, T>, IComparisonOperators<T, T, bool>
        {
            T sum = @this + other;
            if (sum < @this || sum < other)
                return T.MaxValue;

            return sum;
        }

        public static long ToLongSafe(this ulong @this)
        {
            return @this > long.MaxValue ? long.MaxValue : (long)@this;
        }

        public static ulong ToULongSafe(this UInt256 @this)
        {
            return @this > ulong.MaxValue ? ulong.MaxValue : (ulong)@this;
        }

        public static long ApproxExpBasisPoints(long bips, ulong accuracy)
        {
            bool isNegative = bips < 0;
            ulong inputAbs = (ulong)System.Math.Abs(bips);

            ulong result = BipsMultiplier + inputAbs / accuracy;

            for (ulong i = 1; i < accuracy; i++)
            {
                result = BipsMultiplier + SaturateMul(result, inputAbs) / ((accuracy - i) * BipsMultiplier);
            }

            return isNegative ? (BipsMultiplier * BipsMultiplier / result).ToLongSafe() : result.ToLongSafe();
        }

        public static ulong UlongMulByBips(ulong value, ulong bips) => value * bips / BipsMultiplier;

        public static UInt256 UInt256MulByBips(UInt256 value, ulong bips) => value * bips / BipsMultiplier;

        public static ulong ULongSaturatingMulByBips(ulong value, long bips)
        {
            return bips <= 0 ? 0 : SaturateMul(value, (ulong)bips) / BipsMultiplier;
        }

        /// <summary>
        /// Implements Euclidean division for BigInteger. Adjusts the quotient to ensure the remainder is non-negative.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        /// <exception cref="DivideByZeroException"></exception>
        public static BigInteger FloorDiv(BigInteger x, BigInteger y)
        {
            if (y.IsZero)
                throw new DivideByZeroException();

            BigInteger q = BigInteger.DivRem(x, y, out BigInteger r);

            if (r.Sign < 0)
            {
                // Adjust so remainder is always non-negative
                if (y.Sign < 0)
                    q += 1;
                else
                    q -= 1;
            }

            return q;
        }

        public static long SaturatingSignedAdd(long a, long b)
        {
            return b switch
            {
                > 0 when a > long.MaxValue - b => long.MaxValue,
                < 0 when a < long.MinValue - b => long.MinValue,
                _ => a + b
            };
        }
    }
}
