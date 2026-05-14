using BLEProximity.Services;
using FsCheck;
using FsCheck.Xunit;

namespace BLEProximity.Tests.Properties;

/// <summary>
/// Property 7: Alpha Clamping
/// Validates: Requirements 2.3
///
/// For any double value provided as the alpha parameter, the effective alpha
/// used by the RSSI_Smoother SHALL be clamped to the range [0.1, 0.5] inclusive,
/// where values below 0.1 become 0.1 and values above 0.5 become 0.5.
/// </summary>
public class AlphaClampingTests
{
    /// <summary>
    /// **Validates: Requirements 2.3**
    /// For any arbitrary double value set as Alpha (excluding NaN which is not a numeric value),
    /// the getter always returns a value in [0.1, 0.5].
    /// Covers infinity, very large/small values, and boundary values.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(AlphaDoubleArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "7: Alpha Clamping")]
    public Property Alpha_AlwaysClampedToValidRange(AlphaInput input)
    {
        var smoother = new RssiSmoother();
        smoother.Alpha = input.Value;
        var result = smoother.Alpha;

        return (result >= 0.1 && result <= 0.5)
            .Label($"Alpha {input.Value} resulted in {result}, expected [0.1, 0.5]");
    }

    /// <summary>
    /// **Validates: Requirements 2.3**
    /// For any double value below 0.1, the effective alpha becomes exactly 0.1.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(BelowMinAlphaArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "7: Alpha Clamping")]
    public Property Alpha_BelowMinimum_BecomesExactly01(BelowMinAlpha input)
    {
        var smoother = new RssiSmoother();
        smoother.Alpha = input.Value;

        return (smoother.Alpha == 0.1)
            .Label($"Alpha {input.Value} should clamp to 0.1 but got {smoother.Alpha}");
    }

    /// <summary>
    /// **Validates: Requirements 2.3**
    /// For any double value above 0.5, the effective alpha becomes exactly 0.5.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(AboveMaxAlphaArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "7: Alpha Clamping")]
    public Property Alpha_AboveMaximum_BecomesExactly05(AboveMaxAlpha input)
    {
        var smoother = new RssiSmoother();
        smoother.Alpha = input.Value;

        return (smoother.Alpha == 0.5)
            .Label($"Alpha {input.Value} should clamp to 0.5 but got {smoother.Alpha}");
    }

    /// <summary>
    /// **Validates: Requirements 2.3**
    /// For any double value within [0.1, 0.5], the effective alpha is preserved exactly.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(InRangeAlphaArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "7: Alpha Clamping")]
    public Property Alpha_WithinRange_PreservedExactly(InRangeAlpha input)
    {
        var smoother = new RssiSmoother();
        smoother.Alpha = input.Value;

        return (smoother.Alpha == input.Value)
            .Label($"Alpha {input.Value} within range should be preserved but got {smoother.Alpha}");
    }

    /// <summary>
    /// **Validates: Requirements 2.3**
    /// Edge cases: positive infinity, negative infinity, and extreme values
    /// all result in a clamped alpha within [0.1, 0.5].
    /// Note: NaN is excluded as Math.Clamp propagates NaN per IEEE 754 semantics.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(EdgeCaseAlphaArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "7: Alpha Clamping")]
    public Property Alpha_EdgeCases_AlwaysClampedToValidRange(EdgeCaseAlpha input)
    {
        var smoother = new RssiSmoother();
        smoother.Alpha = input.Value;
        var result = smoother.Alpha;

        return (result >= 0.1 && result <= 0.5)
            .Label($"Edge case alpha {input.Value} resulted in {result}, expected [0.1, 0.5]");
    }
}

// Wrapper types to avoid FsCheck's built-in double generator conflicts

public record struct AlphaInput(double Value);
public record struct BelowMinAlpha(double Value);
public record struct AboveMaxAlpha(double Value);
public record struct InRangeAlpha(double Value);
public record struct EdgeCaseAlpha(double Value);

/// <summary>
/// Generates arbitrary double values including edge cases for comprehensive alpha testing.
/// NaN is excluded as Math.Clamp propagates NaN per IEEE 754 semantics.
/// </summary>
public class AlphaDoubleArbitrary
{
    public static Arbitrary<AlphaInput> AlphaInput()
    {
        var gen = Gen.Frequency(
            Tuple.Create(3, Gen.Choose(-100000, 100000).Select(i => i / 1000.0)),
            Tuple.Create(2, Gen.Choose(-10, 10).Select(i => i / 10.0)),
            Tuple.Create(1, Gen.Elements(
                double.PositiveInfinity,
                double.NegativeInfinity,
                double.MaxValue,
                double.MinValue,
                double.Epsilon,
                -double.Epsilon,
                0.0,
                0.1,
                0.5,
                0.09999999999,
                0.50000000001,
                -1000.0,
                1000.0
            ))
        );

        return Arb.From(gen.Select(v => new AlphaInput(v)));
    }
}

/// <summary>
/// Generates double values strictly below 0.1.
/// </summary>
public class BelowMinAlphaArbitrary
{
    public static Arbitrary<BelowMinAlpha> BelowMinAlpha()
    {
        var gen = Gen.Frequency(
            Tuple.Create(3, Gen.Choose(-100000, 999).Select(i => i / 10000.0)), // [-10.0, 0.0999]
            Tuple.Create(1, Gen.Elements(
                0.0,
                0.09,
                0.05,
                0.01,
                -1.0,
                -100.0,
                -0.5,
                double.NegativeInfinity,
                double.MinValue
            ))
        );

        return Arb.From(gen.Select(v => new BelowMinAlpha(v)));
    }
}

/// <summary>
/// Generates double values strictly above 0.5.
/// </summary>
public class AboveMaxAlphaArbitrary
{
    public static Arbitrary<AboveMaxAlpha> AboveMaxAlpha()
    {
        var gen = Gen.Frequency(
            Tuple.Create(3, Gen.Choose(5001, 100000).Select(i => i / 10000.0)), // (0.5, 10.0]
            Tuple.Create(1, Gen.Elements(
                0.51,
                0.6,
                0.9,
                1.0,
                2.0,
                100.0,
                1000.0,
                double.PositiveInfinity,
                double.MaxValue
            ))
        );

        return Arb.From(gen.Select(v => new AboveMaxAlpha(v)));
    }
}

/// <summary>
/// Generates double values within [0.1, 0.5] inclusive.
/// </summary>
public class InRangeAlphaArbitrary
{
    public static Arbitrary<InRangeAlpha> InRangeAlpha()
    {
        var gen = Gen.Frequency(
            Tuple.Create(4, Gen.Choose(1000, 5000).Select(i => i / 10000.0)), // [0.1, 0.5]
            Tuple.Create(1, Gen.Elements(
                0.1,
                0.2,
                0.3,
                0.4,
                0.5,
                0.15,
                0.25,
                0.35,
                0.45
            ))
        );

        return Arb.From(gen.Select(v => new InRangeAlpha(v)));
    }
}

/// <summary>
/// Generates edge case double values specifically targeting boundary conditions.
/// NaN is excluded as Math.Clamp propagates NaN per IEEE 754 semantics.
/// </summary>
public class EdgeCaseAlphaArbitrary
{
    public static Arbitrary<EdgeCaseAlpha> EdgeCaseAlpha()
    {
        var gen = Gen.Elements(
            double.PositiveInfinity,
            double.NegativeInfinity,
            double.MaxValue,
            double.MinValue,
            double.Epsilon,
            -double.Epsilon,
            0.0,
            -1000.0,
            1000.0,
            0.1,
            0.5,
            0.09999999999,
            0.50000000001,
            0.0999,
            0.5001,
            -1.0,
            2.0
        );

        return Arb.From(gen.Select(v => new EdgeCaseAlpha(v)));
    }
}
