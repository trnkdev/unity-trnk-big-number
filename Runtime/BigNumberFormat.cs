namespace NekoBigNum
{
    /// <summary>
    /// Display format options for <see cref="BigNumber"/>.
    /// </summary>
    public enum BigNumberFormat
    {
        /// <summary>K/M/B/T up to 1e12, then alphabetical (1.50aa, 1.50ab, ...).</summary>
        Mixed,

        /// <summary>K/M/B/T up to 1e12, then scientific (1.50e15).</summary>
        Standard,

        /// <summary>Pure alphabetical from 1e3 onward (1.50K via "a"-style is not used; this uses 1.50a, 1.50b, ...).</summary>
        Alphabetical,

        /// <summary>Scientific notation (1.50e30).</summary>
        Scientific,

        /// <summary>Engineering notation: mantissa scaled to a multiple-of-3 exponent (150e3, 1.5e6, ...).</summary>
        Engineering,

        /// <summary>Raw decimal expansion. Saturates to ±∞ when out of double range.</summary>
        Raw
    }
}
