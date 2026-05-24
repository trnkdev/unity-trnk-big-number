using System;
using System.Globalization;
using UnityEngine;

namespace TRnK.BigNum
{
    /// <summary>
    /// High-performance numeric value for idle / incremental games.
    /// Stores a normalized double mantissa in [1, 10) (or 0) and a long base-10 exponent,
    /// giving roughly 15 digits of precision and effectively unlimited magnitude.
    /// </summary>
    /// <remarks>
    /// Although this is a plain (non-<c>readonly</c>) <c>struct</c> for Unity-serialization
    /// compatibility, instances are immutable by design: no setters, no mutating methods.
    /// All operators return new instances.
    /// </remarks>
    [Serializable]
    public struct BigNumber : IComparable<BigNumber>, IEquatable<BigNumber>, IFormattable
    {
        // Sentinel for "not yet normalized"; the constructor always normalizes.
        private const double DoubleExponentLimit = 308.0;
        private const double Epsilon = 1e-12;

        public static readonly BigNumber Zero = new(0.0, 0L, normalize: false);
        public static readonly BigNumber One = new(1.0, 0L, normalize: false);
        public static readonly BigNumber MaxValue = new(9.999999999999999, long.MaxValue, normalize: false);
        public static readonly BigNumber MinValue = new(-9.999999999999999, long.MaxValue, normalize: false);

        [SerializeField] private double _mantissa;
        [SerializeField] private long _exponent;

        public readonly double Mantissa => _mantissa;
        public readonly long Exponent => _exponent;
        public readonly bool IsZero => _mantissa == 0.0;
        public readonly bool IsNegative => _mantissa < 0.0;
        public readonly bool IsPositive => _mantissa > 0.0;
        public readonly int Sign => _mantissa == 0.0 ? 0 : (_mantissa > 0.0 ? 1 : -1);

        public BigNumber(double value)
        {
            if (value == 0.0 || double.IsNaN(value))
            {
                _mantissa = 0.0;
                _exponent = 0L;
                return;
            }

            if (double.IsPositiveInfinity(value))
            {
                _mantissa = MaxValue._mantissa;
                _exponent = MaxValue._exponent;
                return;
            }
            if (double.IsNegativeInfinity(value))
            {
                _mantissa = MinValue._mantissa;
                _exponent = MinValue._exponent;
                return;
            }

            double absValue = Math.Abs(value);
            long exp = (long)Math.Floor(Math.Log10(absValue));
            double m = value / Math.Pow(10.0, exp);

            // Floating-point drift can push mantissa slightly out of [1, 10); fix it.
            if (Math.Abs(m) >= 10.0)
            {
                m /= 10.0;
                exp++;
            }
            else if (Math.Abs(m) < 1.0)
            {
                m *= 10.0;
                exp--;
            }

            _mantissa = m;
            _exponent = exp;
        }

        public BigNumber(double mantissa, long exponent) : this(mantissa, exponent, normalize: true) { }

        private BigNumber(double mantissa, long exponent, bool normalize)
        {
            if (!normalize)
            {
                _mantissa = mantissa;
                _exponent = exponent;
                return;
            }

            if (mantissa == 0.0 || double.IsNaN(mantissa))
            {
                _mantissa = 0.0;
                _exponent = 0L;
                return;
            }

            double abs = Math.Abs(mantissa);
            if (abs >= 1.0 && abs < 10.0)
            {
                _mantissa = mantissa;
                _exponent = exponent;
                return;
            }

            long shift = (long)Math.Floor(Math.Log10(abs));
            double m = mantissa / Math.Pow(10.0, shift);
            double mAbs = Math.Abs(m);
            if (mAbs >= 10.0)
            {
                m /= 10.0;
                shift++;
            }
            else if (mAbs < 1.0 && mAbs > 0.0)
            {
                m *= 10.0;
                shift--;
            }

            _mantissa = m;
            _exponent = exponent + shift;
        }

        // Internal factory for the rare cases where the caller has already normalized.
        internal static BigNumber FromNormalized(double mantissa, long exponent) =>
            new(mantissa, exponent, normalize: false);

        #region Arithmetic

        public static BigNumber operator +(BigNumber a, BigNumber b)
        {
            if (a.IsZero) return b;
            if (b.IsZero) return a;

            // If exponent gap is too large for double precision, the smaller addend vanishes.
            // double has ~15 digits, so anything > 17 is unrecoverable.
            long expDiff = a._exponent - b._exponent;
            if (expDiff > 17) return a;
            if (expDiff < -17) return b;

            double mantissa;
            long exponent;
            if (expDiff >= 0)
            {
                mantissa = a._mantissa + b._mantissa / Math.Pow(10.0, expDiff);
                exponent = a._exponent;
            }
            else
            {
                mantissa = a._mantissa / Math.Pow(10.0, -expDiff) + b._mantissa;
                exponent = b._exponent;
            }
            return new BigNumber(mantissa, exponent);
        }

        public static BigNumber operator -(BigNumber a, BigNumber b)
        {
            if (b.IsZero) return a;
            return a + new BigNumber(-b._mantissa, b._exponent, normalize: false);
        }

        public static BigNumber operator -(BigNumber a) =>
            a.IsZero ? Zero : new BigNumber(-a._mantissa, a._exponent, normalize: false);

        public static BigNumber operator *(BigNumber a, BigNumber b)
        {
            if (a.IsZero || b.IsZero) return Zero;
            return new BigNumber(a._mantissa * b._mantissa, a._exponent + b._exponent);
        }

        public static BigNumber operator /(BigNumber a, BigNumber b)
        {
            if (b.IsZero) throw new DivideByZeroException();
            if (a.IsZero) return Zero;
            return new BigNumber(a._mantissa / b._mantissa, a._exponent - b._exponent);
        }

        public static BigNumber operator %(BigNumber a, BigNumber b)
        {
            if (b.IsZero) throw new DivideByZeroException();
            if (a.IsZero) return Zero;
            // a % b = a - (a / b).Floor() * b — only meaningful when result fits in double.
            BigNumber q = a / b;
            double qd = q.ToDouble();
            if (double.IsInfinity(qd)) return Zero;
            return a - new BigNumber(Math.Floor(qd)) * b;
        }

        public static BigNumber operator ++(BigNumber a) => a + One;
        public static BigNumber operator --(BigNumber a) => a - One;

        #endregion

        #region Comparison

        public readonly int CompareTo(BigNumber other)
        {
            int signSelf = Sign;
            int signOther = other.Sign;
            if (signSelf != signOther) return signSelf.CompareTo(signOther);
            if (signSelf == 0) return 0;

            // Same sign: compare exponents, but flip ordering for negatives.
            int expCmp = _exponent.CompareTo(other._exponent);
            if (expCmp != 0) return signSelf > 0 ? expCmp : -expCmp;
            return _mantissa.CompareTo(other._mantissa);
        }

        public readonly bool Equals(BigNumber other) =>
            _mantissa.Equals(other._mantissa) && _exponent == other._exponent;

        public override readonly bool Equals(object obj) => obj is BigNumber other && Equals(other);

        public override readonly int GetHashCode() => HashCode.Combine(_mantissa, _exponent);

        /// <summary>
        /// Tolerant equality for values produced by arithmetic. Treats two BigNumbers as equal
        /// when their mantissas agree within <paramref name="epsilon"/> after exponent alignment.
        /// </summary>
        public readonly bool ApproximatelyEquals(BigNumber other, double epsilon = Epsilon)
        {
            if (Sign != other.Sign) return false;
            if (IsZero && other.IsZero) return true;
            if (_exponent != other._exponent) return false;
            return Math.Abs(_mantissa - other._mantissa) <= epsilon;
        }

        public static bool operator >(BigNumber a, BigNumber b) => a.CompareTo(b) > 0;
        public static bool operator <(BigNumber a, BigNumber b) => a.CompareTo(b) < 0;
        public static bool operator >=(BigNumber a, BigNumber b) => a.CompareTo(b) >= 0;
        public static bool operator <=(BigNumber a, BigNumber b) => a.CompareTo(b) <= 0;
        public static bool operator ==(BigNumber a, BigNumber b) => a.Equals(b);
        public static bool operator !=(BigNumber a, BigNumber b) => !a.Equals(b);

        #endregion

        #region Conversions

        public static implicit operator BigNumber(int value) => new(value);
        public static implicit operator BigNumber(uint value) => new(value);
        public static implicit operator BigNumber(long value) => new(value);
        public static implicit operator BigNumber(ulong value) => new(value);
        public static implicit operator BigNumber(float value) => new(value);
        public static implicit operator BigNumber(double value) => new(value);
        public static implicit operator BigNumber(decimal value) => new((double)value);

        /// <summary>
        /// Returns the value as a double. Saturates to ±Infinity when the magnitude exceeds
        /// double's representable range (~1e308).
        /// </summary>
        public readonly double ToDouble()
        {
            if (IsZero) return 0.0;
            if (_exponent > DoubleExponentLimit)
                return _mantissa > 0.0 ? double.PositiveInfinity : double.NegativeInfinity;
            if (_exponent < -DoubleExponentLimit) return 0.0;
            return _mantissa * Math.Pow(10.0, _exponent);
        }

        public readonly float ToFloat() => (float)ToDouble();

        public readonly int ToInt()
        {
            double d = ToDouble();
            if (d >= int.MaxValue) return int.MaxValue;
            if (d <= int.MinValue) return int.MinValue;
            return (int)d;
        }

        public readonly long ToLong()
        {
            double d = ToDouble();
            if (d >= long.MaxValue) return long.MaxValue;
            if (d <= long.MinValue) return long.MinValue;
            return (long)d;
        }

        #endregion

        #region Formatting

        public override readonly string ToString() => BigNumberFormatter.Format(this, BigNumberFormat.Mixed);

        public readonly string ToString(BigNumberFormat format) => BigNumberFormatter.Format(this, format);

        public readonly string ToString(string format, IFormatProvider formatProvider)
        {
            if (string.IsNullOrEmpty(format)) return ToString();
            if (Enum.TryParse(format, ignoreCase: true, out BigNumberFormat parsed))
                return BigNumberFormatter.Format(this, parsed);
            return ToDouble().ToString(format, formatProvider ?? CultureInfo.InvariantCulture);
        }

        #endregion

        #region Parsing

        public static BigNumber Parse(string input)
        {
            if (TryParse(input, out BigNumber result)) return result;
            throw new FormatException($"Could not parse '{input}' as BigNumber.");
        }

        public static bool TryParse(string input, out BigNumber result) =>
            BigNumberParser.TryParse(input, out result);

        #endregion
    }
}
