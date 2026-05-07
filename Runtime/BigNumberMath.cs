using System;

namespace NekoBigNum
{
    /// <summary>
    /// Math utilities for <see cref="BigNumber"/>. Mirrors a subset of <see cref="System.Math"/>.
    /// </summary>
    public static class BigNumberMath
    {
        public static BigNumber Abs(BigNumber value) =>
            value.IsNegative ? -value : value;

        /// <summary>
        /// Raises a BigNumber to a real power. Internally uses log10/exp10, which is exact enough
        /// for game math but loses precision for huge bases. <paramref name="exponent"/> may be
        /// fractional or negative.
        /// </summary>
        public static BigNumber Pow(BigNumber baseValue, double exponent)
        {
            if (exponent == 0.0) return BigNumber.One;
            if (baseValue.IsZero) return BigNumber.Zero;
            if (exponent == 1.0) return baseValue;

            if (baseValue.IsNegative)
            {
                // Real-valued only — non-integer exponents on negatives are undefined here.
                if (Math.Floor(exponent) != exponent)
                    throw new ArgumentException(
                        "Cannot raise a negative BigNumber to a non-integer power.",
                        nameof(exponent));

                BigNumber posPow = Pow(-baseValue, exponent);
                return ((long)exponent & 1L) == 1L ? -posPow : posPow;
            }

            // log10(baseValue) = log10(mantissa) + exponent.
            double log10Base = Math.Log10(baseValue.Mantissa) + baseValue.Exponent;
            double newLog10 = log10Base * exponent;

            long newExp = (long)Math.Floor(newLog10);
            double newMantissa = Math.Pow(10.0, newLog10 - newExp);
            return new BigNumber(newMantissa, newExp);
        }

        public static BigNumber Pow(BigNumber baseValue, int exponent) =>
            Pow(baseValue, (double)exponent);

        /// <summary>
        /// Natural logarithm. Throws for non-positive inputs.
        /// </summary>
        public static double Log(BigNumber value)
        {
            if (!value.IsPositive)
                throw new ArgumentException("Logarithm undefined for non-positive values.", nameof(value));
            return Math.Log(value.Mantissa) + value.Exponent * Math.Log(10.0);
        }

        /// <summary>
        /// Base-10 logarithm. Returns a double — the result is small enough to never need BigNumber.
        /// </summary>
        public static double Log10(BigNumber value)
        {
            if (!value.IsPositive)
                throw new ArgumentException("Logarithm undefined for non-positive values.", nameof(value));
            return Math.Log10(value.Mantissa) + value.Exponent;
        }

        public static double Log(BigNumber value, double newBase) =>
            Log10(value) / Math.Log10(newBase);

        public static BigNumber Sqrt(BigNumber value)
        {
            if (value.IsNegative)
                throw new ArgumentException("Square root undefined for negative values.", nameof(value));
            if (value.IsZero) return BigNumber.Zero;

            // sqrt(m * 10^e) = sqrt(m) * 10^(e/2). Handle odd exponents by shifting mantissa.
            double m = value.Mantissa;
            long e = value.Exponent;
            if ((e & 1L) != 0L)
            {
                m *= 10.0;
                e -= 1L;
            }
            return new BigNumber(Math.Sqrt(m), e / 2L);
        }

        public static BigNumber Cbrt(BigNumber value) => Pow(value, 1.0 / 3.0);

        public static BigNumber Min(BigNumber a, BigNumber b) => a < b ? a : b;
        public static BigNumber Max(BigNumber a, BigNumber b) => a > b ? a : b;

        public static BigNumber Clamp(BigNumber value, BigNumber min, BigNumber max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>
        /// Linear interpolation. <paramref name="t"/> is unclamped — use <see cref="LerpClamped"/>
        /// to clamp into [0, 1].
        /// </summary>
        public static BigNumber Lerp(BigNumber a, BigNumber b, double t) =>
            a + (b - a) * t;

        public static BigNumber LerpClamped(BigNumber a, BigNumber b, double t)
        {
            if (t <= 0.0) return a;
            if (t >= 1.0) return b;
            return a + (b - a) * t;
        }

        /// <summary>
        /// Inverse lerp. Returns the fraction t such that Lerp(a, b, t) == value.
        /// Returns 0 when a == b. Result is a double since it's bounded.
        /// </summary>
        public static double InverseLerp(BigNumber a, BigNumber b, BigNumber value)
        {
            if (a == b) return 0.0;
            BigNumber diff = b - a;
            BigNumber rel = value - a;
            return (rel / diff).ToDouble();
        }

        public static BigNumber Floor(BigNumber value)
        {
            // For exponent >= ~15, mantissa precision already exceeds the unit place — no fraction.
            if (value.Exponent >= 15L) return value;
            double d = value.ToDouble();
            return new BigNumber(Math.Floor(d));
        }

        public static BigNumber Ceiling(BigNumber value)
        {
            if (value.Exponent >= 15L) return value;
            double d = value.ToDouble();
            return new BigNumber(Math.Ceiling(d));
        }

        public static BigNumber Round(BigNumber value)
        {
            if (value.Exponent >= 15L) return value;
            double d = value.ToDouble();
            return new BigNumber(Math.Round(d));
        }
    }
}
