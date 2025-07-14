using Nethermind.Int256;

namespace Nethermind.Arbitrum.Math
{
    public static class Utils
    {
        public const ulong BipsMultiplier = 10_000;

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

        public static UInt256 SaturateMul(this UInt256 @this, UInt256 other)
        {
            bool overflows = UInt256.MultiplyOverflow(@this, other, out other);
            if (overflows)
                other = UInt256.MaxValue;
            return other;
        }

        public static ulong SaturateMul(this ulong @this, ulong other)
        {
            if (@this == 0 || other == 0)
                return 0;

            if (@this > ulong.MaxValue / other)
                return ulong.MaxValue;

            return @this * @other;
        }

        public static ulong SaturateSub(this ulong @this, ulong other)
        {
            if (other >= @this) return 0;
            return @this - other;
        }
        public static long SaturateSub(this long @this, long other)
        {
            if (other >= @this) return 0;
            return @this - other;
        }
        public static ulong SaturateAdd(this ulong @this, ulong other)
        {
            ulong result = @this + other;
            if (result < @this || result < other)
                return ulong.MaxValue;
            return result;
        }

        public static long ToLongSafe(this ulong @this)
        {
            return @this > long.MaxValue ? long.MaxValue : (long)@this;
        }

        public static long ApproxExpBasisPoints(long bips, ulong accuracy)
        {
            var isNegative = bips < 0;
            var inputAbs = (ulong)System.Math.Abs(bips);

            var result = BipsMultiplier + inputAbs / accuracy;

            for (ulong i = 1; i < accuracy; i++)
            {
                result = BipsMultiplier + SaturateMul(result, inputAbs) / ((accuracy - i) * BipsMultiplier);
            }

            if (isNegative)
            {
                return (BipsMultiplier * BipsMultiplier / result).ToLongSafe();
            }
            else
            {
                return result.ToLongSafe();
            }
        }
    }
}
