# TRnK Big Number

High-performance BigNumber for Unity idle / incremental games.

Stores a normalized `double` mantissa in `[1, 10)` plus a `long` base-10 exponent — roughly **15 digits of precision** and **effectively unlimited magnitude** (up to `long.MaxValue` exponent, around 1e9.2 quintillion).

Designed to match the conventions of `break_infinity.js` and Adventure-Capitalist-style suffix display (K, M, B, T, aa, ab, ac, ...).

---

## Install

### Via Git URL (Unity Package Manager)

```
https://github.com/trnkdev/unity-trnk-big-number.git
```

### Via local path / embedded package

Place under `Packages/com.trnkdev.unitybignumber/` or add to `Packages/manifest.json`:

```json
"com.trnkdev.unitybignumber": "file:../path/to/TRnK.BigNum"
```

Optional: install `com.unity.nuget.newtonsoft-json` to enable the bundled `BigNumberConverter`. The `NEWTONSOFT_JSON` define is set automatically via `versionDefines` when the package is present.

---

## Usage

```csharp
using TRnK.BigNum;

BigNumber money = 1000;          // implicit conversion from int/long/float/double/decimal
BigNumber price = new(1.5, 30);  // 1.5e30
money += price;
money *= 1.15;
money = BigNumberMath.Pow(money, 2);

Debug.Log(money.ToString());                          // Mixed format (default)
Debug.Log(money.ToString(BigNumberFormat.Scientific)); // 1.23e60
Debug.Log(money.ToString(BigNumberFormat.Standard));   // K, M, B, T, then scientific
```

### Display formats

| Format | Example output |
|---|---|
| `Mixed` (default) | `500`, `1.5K`, `2.5M`, `1aa`, `3.14ab` |
| `Standard` | `1.5K`, `1T`, then `1.5e15`, `1.5e30` |
| `Alphabetical` | `1.5a`, `1.5b`, ..., `1.5z`, `1.5aa` |
| `Scientific` | `1.5e30` |
| `Engineering` | `1.5e6`, `15e6`, `150e6` (always multiple-of-3 exponent) |
| `Raw` | `1,500,000` (saturates to ∞ above ~1e308) |

### Math

```csharp
BigNumberMath.Pow(value, 2.5);
BigNumberMath.Sqrt(value);
BigNumberMath.Log10(value);          // returns double
BigNumberMath.Min(a, b);
BigNumberMath.Max(a, b);
BigNumberMath.Clamp(v, min, max);
BigNumberMath.Lerp(a, b, t);
BigNumberMath.Floor(value);
BigNumberMath.Abs(value);
```

### Parsing

```csharp
BigNumber.TryParse("1.5K", out var n);    // exp 3
BigNumber.TryParse("1.5e30", out var n);  // exp 30
BigNumber.TryParse("3.14ab", out var n);  // exp 18 (AC convention)
BigNumber.TryParse("1,500,000", out var n);
BigNumber.TryParse("1_500_000", out var n);
```

### Serialization

**`JsonUtility` works out of the box** — `[SerializeField]` fields produce compact `{"_mantissa":1.5,"_exponent":30}`.

**Newtonsoft (optional) — zero setup.** When `com.unity.nuget.newtonsoft-json`
is installed, `BigNumber` carries a type-level `[JsonConverter]` binding to the
bundled `BigNumberConverter`, so any Newtonsoft serialization (including TRnK
SaveLoad) handles `BigNumber` fields automatically:

```csharp
string json = JsonConvert.SerializeObject(saveData);   // BigNumber fields just work
```

The converter emits `{"m":1.5,"e":30}` to keep save files small, and also reads
bare numbers (`1500000`) for migration from older formats. A per-field
`[JsonConverter]` attribute can override the type-level binding if a game ever
needs a different format.

### Inspector

Drop a `[SerializeField] BigNumber currency;` on any MonoBehaviour. The custom drawer accepts any input format the parser handles (`1.5M`, `1.5e30`, `1500000`) and shows the parsed value plus a foldout with raw mantissa/exponent for debugging.

---

## Precision & limits

| | Value |
|---|---|
| Mantissa precision | ~15 decimal digits (double) |
| Max exponent | `long.MaxValue` ≈ 9.2e18 |
| `ToDouble()` saturates above | exponent ±308 |
| Equality (`==`) | exact (bitwise on mantissa + exponent) |
| Equality (`ApproximatelyEquals`) | tolerant, default ε = 1e-12 |

For idle-game scale this is more than enough — comparable to `break_infinity.js`. For tetration-scale games (Antimatter Dimensions territory), a different library is needed.

---
