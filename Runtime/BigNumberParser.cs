using System;
using System.Globalization;

namespace TRnK.BigNum
{
    /// <summary>
    /// Parses strings produced by <see cref="BigNumberFormatter"/> back into a <see cref="BigNumber"/>.
    /// Accepts: raw decimals ("1500000"), scientific ("1.5e30", "1.5E+30"), engineering,
    /// standard suffixes ("1.5K", "2M", "3.14B", "1T"), and alphabetical suffixes ("1.5a", "2ab").
    /// Whitespace and underscores are ignored. Leading "+" / "-" is honored.
    /// </summary>
    /// <remarks>
    /// Single-letter ambiguity: K/M/B/T are always interpreted as Standard suffixes (case-insensitive),
    /// never as pure-alphabetical. The letter E (case-insensitive) is reserved for the scientific
    /// exponent marker. As a result, pure-alphabetical mode output for those five letters cannot
    /// round-trip through the parser — use Mixed/Standard/Scientific output if round-trip matters.
    /// Multi-letter suffixes (aa, ab, ...) always use Adventure-Capitalist convention where
    /// "aa" = exp 15.
    /// </remarks>
    public static class BigNumberParser
    {
        private const NumberStyles MantissaStyle =
            NumberStyles.AllowDecimalPoint
            | NumberStyles.AllowLeadingSign
            | NumberStyles.AllowExponent;

        public static bool TryParse(string input, out BigNumber result)
        {
            result = BigNumber.Zero;
            if (string.IsNullOrWhiteSpace(input)) return false;

            string s = Strip(input);
            if (s.Length == 0) return false;

            // Pure scientific or pure number — let double do the work.
            if (TryParseDoubleStyle(s, out result)) return true;

            // Suffix-based: separate digit/sign/decimal/exponent prefix from trailing letters.
            int splitIndex = FindSuffixStart(s);
            if (splitIndex < 0 || splitIndex >= s.Length) return false;

            string mantissaText = s.Substring(0, splitIndex);
            string suffix = s.Substring(splitIndex);

            if (!double.TryParse(mantissaText, MantissaStyle, CultureInfo.InvariantCulture, out double mantissa))
                return false;

            if (TryStandardSuffix(suffix, out long exponent))
            {
                result = new BigNumber(mantissa, exponent);
                return true;
            }

            // Letter suffix. We support two conventions:
            //   - Single letter (a..z): pure-alphabetical style, exp = bucket * 3 where a=1, b=2, ...
            //     Note "K/M/B/T" already covers a/b/c/d-equivalent positions, but pure-alpha mode
            //     exists for display so we accept its output here too.
            //   - Multi letter (aa, ab, ...): Adventure-Capitalist convention, exp = (bucket - 26 + 5) * 3
            //     where "aa" = exp 15, "ab" = exp 18, ..., "az" = exp 90, "ba" = exp 93, ...
            long pureBucket = BigNumberFormatter.AlphabeticalSuffixToBucket(suffix);
            if (pureBucket < 0L) return false;

            long exp;
            if (suffix.Length == 1)
            {
                // Pure alphabetical: bucket 0 ("a") -> exp 3
                exp = (pureBucket + 1L) * 3L;
            }
            else
            {
                // AC convention: pure bucket 26 ("aa") -> exp 15
                long acBucket = pureBucket - 26L;
                if (acBucket < 0L) return false;
                exp = (acBucket + 5L) * 3L;
            }

            result = new BigNumber(mantissa, exp);
            return true;
        }

        private static bool TryParseDoubleStyle(string s, out BigNumber result)
        {
            result = BigNumber.Zero;

            // Reject if it ends with a letter we recognize as a unit suffix; those go through the suffix path.
            // Exception: "e"/"E" exponent markers are mid-string only.
            char last = s[^1];
            if (char.IsLetter(last) && last != 'e' && last != 'E')
            {
                // Could still be "1e30" with valid exponent, but if last is a letter other than e/E it's a suffix.
                return false;
            }

            // Symbol shortcut: ∞.
            if (s == "∞" || s == "+∞")
            {
                result = BigNumber.MaxValue;
                return true;
            }
            if (s == "-∞")
            {
                result = BigNumber.MinValue;
                return true;
            }

            // Try direct double parse first (covers "1500000", "1.5e30", etc.).
            if (double.TryParse(s, MantissaStyle, CultureInfo.InvariantCulture, out double direct))
            {
                if (double.IsInfinity(direct) || double.IsNaN(direct)) return false;
                result = new BigNumber(direct);
                return true;
            }

            // Manual scientific split for exponents that overflow double (e.g. "1.5e500").
            int eIndex = IndexOfExponentMarker(s);
            if (eIndex < 0) return false;

            string mantissaText = s[..eIndex];
            string exponentText = s[(eIndex + 1)..];
            if (mantissaText.Length == 0 || exponentText.Length == 0) return false;

            if (!double.TryParse(mantissaText, MantissaStyle, CultureInfo.InvariantCulture, out double mantissa))
                return false;
            if (!long.TryParse(exponentText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long exponent))
                return false;

            result = new BigNumber(mantissa, exponent);
            return true;
        }

        private static bool TryStandardSuffix(string suffix, out long exponent)
        {
            switch (suffix.ToLowerInvariant())
            {
                case "k": exponent = 3L; return true;
                case "m": exponent = 6L; return true;
                case "b": exponent = 9L; return true;
                case "t": exponent = 12L; return true;
                default: exponent = 0L; return false;
            }
        }

        private static int FindSuffixStart(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsDigit(c) || c == '.' || c == ',' || c == '+' || c == '-') continue;
                if ((c == 'e' || c == 'E') && i + 1 < s.Length)
                {
                    // Only treat as exponent marker if followed by a digit/sign — else it's a suffix.
                    char next = s[i + 1];
                    if (char.IsDigit(next) || next == '+' || next == '-') continue;
                }
                return i;
            }
            return -1;
        }

        private static int IndexOfExponentMarker(string s)
        {
            for (int i = 1; i < s.Length - 1; i++)
            {
                if (s[i] == 'e' || s[i] == 'E') return i;
            }
            return -1;
        }

        private static string Strip(string input)
        {
            // Remove whitespace, underscores, and N0-style thousands commas.
            // Keep sign characters and decimal points.
            var chars = new char[input.Length];
            int n = 0;
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (char.IsWhiteSpace(c) || c == '_' || c == ',') continue;
                chars[n++] = c;
            }
            return new string(chars, 0, n);
        }
    }
}
