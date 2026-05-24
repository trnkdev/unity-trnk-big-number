using System;
using System.Globalization;
using System.Text;

namespace TRnK.BigNum
{
    /// <summary>
    /// Formats <see cref="BigNumber"/> values for display.
    /// Internally everything is base-10, so suffix selection is just <c>exponent / 3</c>.
    /// </summary>
    public static class BigNumberFormatter
    {
        // Index = exponent / 3. "" reserved for exp < 3 (raw value path handles it).
        private static readonly string[] StandardSuffixes = { "", "K", "M", "B", "T" };

        // Threshold for switching from standard suffixes to alphabetical / scientific.
        private const long StandardSuffixMaxExponent = 12L; // up to T

        // Thousands separator culture for raw output. Idle games typically want invariant.
        private static readonly CultureInfo OutputCulture = CultureInfo.InvariantCulture;

        public static string Format(BigNumber number, BigNumberFormat format)
        {
            if (number.IsZero) return "0";

            return format switch
            {
                BigNumberFormat.Mixed => FormatMixed(number),
                BigNumberFormat.Standard => FormatStandard(number),
                BigNumberFormat.Alphabetical => FormatAlphabetical(number),
                BigNumberFormat.Scientific => FormatScientific(number),
                BigNumberFormat.Engineering => FormatEngineering(number),
                BigNumberFormat.Raw => FormatRaw(number),
                _ => FormatMixed(number),
            };
        }

        /// <summary>
        /// Picks a sensible default format based on magnitude.
        /// </summary>
        public static BigNumberFormat GetRecommendedFormat(BigNumber number)
        {
            long exp = Math.Abs(number.Exponent);
            if (exp <= StandardSuffixMaxExponent) return BigNumberFormat.Standard;
            if (exp <= 78L) return BigNumberFormat.Alphabetical; // ~1e78 = end of single-letter range
            return BigNumberFormat.Scientific;
        }

        private static string FormatMixed(BigNumber number)
        {
            if (number.Exponent < 3L) return FormatSmallRaw(number);
            if (number.Exponent <= StandardSuffixMaxExponent) return FormatWithStandardSuffix(number);
            return FormatMixedAlphabetical(number);
        }

        // Adventure-Capitalist convention: past T (exp 12), use double-letter starting at "aa".
        // exp 15 -> "aa", 18 -> "ab", ..., 90 -> "az", 93 -> "ba", ...
        private static string FormatMixedAlphabetical(BigNumber number)
        {
            long bucket = number.Exponent / 3L;
            long remainder = number.Exponent - bucket * 3L;
            double scaled = number.Mantissa * Math.Pow(10.0, remainder);

            // Bucket 5 (exp 15) is the first AC-alpha bucket -> "aa".
            // Pure-alphabet bucket index for "aa" is 26, so add 21 to the offset from 5.
            long acOffset = bucket - 5L; // 0 -> aa, 1 -> ab, ...
            long pureBucket = acOffset + 26L;

            return scaled.ToString("0.##", OutputCulture) + AlphabeticalSuffix(pureBucket);
        }

        private static string FormatStandard(BigNumber number)
        {
            if (number.Exponent < 3L) return FormatSmallRaw(number);
            if (number.Exponent <= StandardSuffixMaxExponent) return FormatWithStandardSuffix(number);
            return FormatScientific(number);
        }

        private static string FormatAlphabetical(BigNumber number)
        {
            if (number.Exponent < 3L) return FormatSmallRaw(number);

            // Bucket by groups of 3 (matching engineering); index 0 = "a" at exp 3.
            long bucket = number.Exponent / 3L;
            long remainder = number.Exponent - bucket * 3L;
            double scaled = number.Mantissa * Math.Pow(10.0, remainder);

            string suffix = AlphabeticalSuffix(bucket - 1L); // bucket 1 -> "a"
            return scaled.ToString("0.##", OutputCulture) + suffix;
        }

        private static string FormatScientific(BigNumber number)
        {
            // Mantissa is normalized to [1, 10); long.ToString handles negative exponents.
            return number.Mantissa.ToString("0.##", OutputCulture)
                + "e" + number.Exponent.ToString(OutputCulture);
        }

        private static string FormatEngineering(BigNumber number)
        {
            long bucket = number.Exponent < 0
                ? -((-number.Exponent + 2L) / 3L) // floor toward -infinity for negatives
                : number.Exponent / 3L;
            long engExp = bucket * 3L;
            long remainder = number.Exponent - engExp;
            double scaled = number.Mantissa * Math.Pow(10.0, remainder);

            return scaled.ToString("0.##", OutputCulture)
                + "e" + engExp.ToString(OutputCulture);
        }

        private static string FormatRaw(BigNumber number)
        {
            double value = number.ToDouble();
            if (double.IsPositiveInfinity(value)) return "∞";
            if (double.IsNegativeInfinity(value)) return "-∞";
            return value.ToString("N0", OutputCulture);
        }

        // --- Helpers ---

        private static string FormatSmallRaw(BigNumber number)
        {
            // exponent < 3; safe to call ToDouble.
            return number.ToDouble().ToString("0.##", OutputCulture);
        }

        private static string FormatWithStandardSuffix(BigNumber number)
        {
            long bucket = number.Exponent / 3L;
            long remainder = number.Exponent - bucket * 3L;
            double scaled = number.Mantissa * Math.Pow(10.0, remainder);
            string suffix = StandardSuffixes[bucket];
            return scaled.ToString("0.##", OutputCulture) + suffix;
        }

        // bucket 0 -> "a", 1 -> "b", ..., 25 -> "z", 26 -> "aa", 27 -> "ab", ...
        private static string AlphabeticalSuffix(long bucket)
        {
            if (bucket < 0L) return string.Empty;

            var sb = new StringBuilder(4);
            long n = bucket;
            do
            {
                sb.Insert(0, (char)('a' + (int)(n % 26L)));
                n = n / 26L - 1L;
            } while (n >= 0L);
            return sb.ToString();
        }

        internal static long AlphabeticalSuffixToBucket(string suffix)
        {
            if (string.IsNullOrEmpty(suffix)) return -1L;
            long bucket = 0L;
            for (int i = 0; i < suffix.Length; i++)
            {
                char c = char.ToLowerInvariant(suffix[i]);
                if (c < 'a' || c > 'z') return -1L;
                bucket = bucket * 26L + (c - 'a' + 1L);
            }
            return bucket - 1L;
        }
    }
}
