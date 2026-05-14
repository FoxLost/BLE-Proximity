using BLEProximity.Helpers;
using FsCheck;
using FsCheck.Xunit;

namespace BLEProximity.Tests.Properties;

/// <summary>
/// Property 16: RSSI Color Classification
/// Validates: Requirements 13.1, 13.2, 13.3, 13.5
///
/// For any smoothed RSSI value, the color classification SHALL be:
/// green when RSSI > -70 dBm, orange when -80 <= RSSI <= -70 dBm,
/// red when RSSI < -80 dBm, and no color when RSSI is null/uninitialized.
/// These categories SHALL be mutually exclusive and exhaustive for all valid RSSI values.
/// </summary>
public class ColorClassificationTests
{
    /// <summary>
    /// **Validates: Requirements 13.1, 13.2, 13.3, 13.5**
    /// Null input always returns None.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "16: RSSI Color Classification")]
    public Property NullInput_AlwaysReturnsNone()
    {
        // This is a deterministic property (null always -> None), but we still
        // express it as a property that holds for any number of invocations.
        var result = RssiColorClassifier.Classify(null);

        return (result == RssiColorCategory.None)
            .Label($"Null input should return None but got {result}");
    }

    /// <summary>
    /// **Validates: Requirements 13.1**
    /// For any RSSI value strictly greater than -70, classification returns Green.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(GreenRssiArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "16: RSSI Color Classification")]
    public Property RssiAboveMinus70_AlwaysReturnsGreen(GreenRssi input)
    {
        var result = RssiColorClassifier.Classify(input.Value);

        return (result == RssiColorCategory.Green)
            .Label($"RSSI {input.Value} (> -70) should return Green but got {result}");
    }

    /// <summary>
    /// **Validates: Requirements 13.2**
    /// For any RSSI value in [-80, -70], classification returns Orange.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(OrangeRssiArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "16: RSSI Color Classification")]
    public Property RssiInOrangeRange_AlwaysReturnsOrange(OrangeRssi input)
    {
        var result = RssiColorClassifier.Classify(input.Value);

        return (result == RssiColorCategory.Orange)
            .Label($"RSSI {input.Value} ([-80, -70]) should return Orange but got {result}");
    }

    /// <summary>
    /// **Validates: Requirements 13.3**
    /// For any RSSI value strictly less than -80, classification returns Red.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(RedRssiArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "16: RSSI Color Classification")]
    public Property RssiBelowMinus80_AlwaysReturnsRed(RedRssi input)
    {
        var result = RssiColorClassifier.Classify(input.Value);

        return (result == RssiColorCategory.Red)
            .Label($"RSSI {input.Value} (< -80) should return Red but got {result}");
    }

    /// <summary>
    /// **Validates: Requirements 13.5**
    /// For any non-null RSSI value, exactly one category is returned (mutually exclusive
    /// and exhaustive). The result is always one of Green, Orange, or Red.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(AnyRssiArbitrary) })]
    [Trait("Feature", "ble-proximity-lock")]
    [Trait("Property", "16: RSSI Color Classification")]
    public Property AnyRssi_ExactlyOneCategoryReturned(AnyRssi input)
    {
        var result = RssiColorClassifier.Classify(input.Value);

        var isGreen = result == RssiColorCategory.Green;
        var isOrange = result == RssiColorCategory.Orange;
        var isRed = result == RssiColorCategory.Red;

        // Exactly one must be true (mutually exclusive and exhaustive for non-null)
        var exactlyOne = (isGreen ? 1 : 0) + (isOrange ? 1 : 0) + (isRed ? 1 : 0) == 1;
        var notNone = result != RssiColorCategory.None;

        return (exactlyOne && notNone)
            .Label($"RSSI {input.Value} should return exactly one of Green/Orange/Red but got {result}");
    }
}

// Wrapper types for FsCheck generators

public record struct GreenRssi(double Value);
public record struct OrangeRssi(double Value);
public record struct RedRssi(double Value);
public record struct AnyRssi(double Value);

/// <summary>
/// Generates RSSI values strictly greater than -70 dBm (Green zone).
/// </summary>
public class GreenRssiArbitrary
{
    public static Arbitrary<GreenRssi> GreenRssi()
    {
        var gen = Gen.Frequency(
            Tuple.Create(4, Gen.Choose(-699, 0).Select(i => i / 10.0)),  // (-69.9, 0.0]
            Tuple.Create(2, Gen.Choose(-6999, -1).Select(i => i / 100.0)), // (-69.99, -0.01)
            Tuple.Create(1, Gen.Elements(
                -69.9,
                -69.99,
                -69.0,
                -60.0,
                -50.0,
                -40.0,
                -30.0,
                -20.0,
                -10.0,
                -1.0,
                -0.1
            ))
        );

        return Arb.From(gen.Select(v => new GreenRssi(v)));
    }
}

/// <summary>
/// Generates RSSI values in the range [-80, -70] dBm (Orange zone).
/// </summary>
public class OrangeRssiArbitrary
{
    public static Arbitrary<OrangeRssi> OrangeRssi()
    {
        var gen = Gen.Frequency(
            Tuple.Create(4, Gen.Choose(-800, -700).Select(i => i / 10.0)), // [-80.0, -70.0]
            Tuple.Create(2, Gen.Choose(-8000, -7000).Select(i => i / 100.0)), // [-80.00, -70.00]
            Tuple.Create(1, Gen.Elements(
                -80.0,
                -70.0,
                -75.0,
                -72.0,
                -78.0,
                -71.0,
                -79.0,
                -74.5,
                -76.3
            ))
        );

        return Arb.From(gen.Select(v => new OrangeRssi(v)));
    }
}

/// <summary>
/// Generates RSSI values strictly less than -80 dBm (Red zone).
/// </summary>
public class RedRssiArbitrary
{
    public static Arbitrary<RedRssi> RedRssi()
    {
        var gen = Gen.Frequency(
            Tuple.Create(4, Gen.Choose(-1200, -801).Select(i => i / 10.0)), // [-120.0, -80.1]
            Tuple.Create(2, Gen.Choose(-12000, -8001).Select(i => i / 100.0)), // [-120.00, -80.01]
            Tuple.Create(1, Gen.Elements(
                -80.1,
                -80.01,
                -81.0,
                -85.0,
                -90.0,
                -95.0,
                -100.0,
                -110.0,
                -120.0
            ))
        );

        return Arb.From(gen.Select(v => new RedRssi(v)));
    }
}

/// <summary>
/// Generates any valid RSSI value across the full range for exhaustiveness testing.
/// </summary>
public class AnyRssiArbitrary
{
    public static Arbitrary<AnyRssi> AnyRssi()
    {
        var gen = Gen.Frequency(
            Tuple.Create(3, Gen.Choose(-1200, 0).Select(i => i / 10.0)),   // Full range [-120, 0]
            Tuple.Create(2, Gen.Choose(-12000, 0).Select(i => i / 100.0)), // Finer granularity
            Tuple.Create(1, Gen.Elements(
                -120.0,
                -100.0,
                -90.0,
                -80.1,
                -80.0,
                -75.0,
                -70.0,
                -69.9,
                -60.0,
                -50.0,
                -30.0,
                -10.0,
                -1.0,
                -0.1
            ))
        );

        return Arb.From(gen.Select(v => new AnyRssi(v)));
    }
}
