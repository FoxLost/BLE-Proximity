using BLEProximity.Helpers;
using FsCheck;
using FsCheck.Xunit;

namespace BLEProximity.Tests.Properties;

/// <summary>
/// Property-based tests for ThresholdValidator.
/// Validates: Requirements 3.1, 3.2, 3.5
/// 
/// Property 8: Threshold Validation
/// For any pair of InRangeThreshold and OutOfRangeThreshold values accepted by the system,
/// the invariant InRangeThreshold - OutOfRangeThreshold >= 5 SHALL hold, and
/// OutOfRangeThreshold SHALL be strictly less than InRangeThreshold.
/// Any pair violating this constraint SHALL be rejected.
/// </summary>
public class ThresholdValidationTests
{
    /// <summary>
    /// Valid pairs (InRange in [-90,-50], OutOfRange in [-95,-55], buffer >= 5, OutOfRange &lt; InRange)
    /// always pass validation.
    /// **Validates: Requirements 3.1, 3.2, 3.5**
    /// </summary>
    [Property(
        Arbitrary = new[] { typeof(ValidThresholdPairArbitrary) },
        MaxTest = 100,
        DisplayName = "Feature: ble-proximity-lock, Property 8: Threshold Validation - Valid pairs always pass")]
    public Property ValidPairs_AlwaysPassValidation(ValidThresholdPair pair)
    {
        var result = ThresholdValidator.Validate(pair.InRange, pair.OutOfRange);

        return result.IsValid.ToProperty()
            .Label($"InRange={pair.InRange}, OutOfRange={pair.OutOfRange}, Buffer={pair.InRange - pair.OutOfRange}");
    }

    /// <summary>
    /// Pairs with buffer &lt; 5 always fail validation.
    /// **Validates: Requirements 3.2, 3.5**
    /// </summary>
    [Property(
        Arbitrary = new[] { typeof(InsufficientBufferPairArbitrary) },
        MaxTest = 100,
        DisplayName = "Feature: ble-proximity-lock, Property 8: Threshold Validation - Insufficient buffer fails")]
    public Property InsufficientBuffer_AlwaysFailsValidation(InsufficientBufferPair pair)
    {
        var result = ThresholdValidator.Validate(pair.InRange, pair.OutOfRange);

        return (!result.IsValid).ToProperty()
            .Label($"InRange={pair.InRange}, OutOfRange={pair.OutOfRange}, Buffer={pair.InRange - pair.OutOfRange}");
    }

    /// <summary>
    /// Pairs where OutOfRange >= InRange always fail validation.
    /// **Validates: Requirements 3.1, 3.5**
    /// </summary>
    [Property(
        Arbitrary = new[] { typeof(OutOfRangeNotLessThanInRangeArbitrary) },
        MaxTest = 100,
        DisplayName = "Feature: ble-proximity-lock, Property 8: Threshold Validation - OutOfRange >= InRange fails")]
    public Property OutOfRangeNotLessThanInRange_AlwaysFailsValidation(OutOfRangeNotLessThanInRangePair pair)
    {
        var result = ThresholdValidator.Validate(pair.InRange, pair.OutOfRange);

        return (!result.IsValid).ToProperty()
            .Label($"InRange={pair.InRange}, OutOfRange={pair.OutOfRange}");
    }

    /// <summary>
    /// Pairs with InRange outside [-90,-50] always fail validation.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(
        Arbitrary = new[] { typeof(InRangeOutOfBoundsArbitrary) },
        MaxTest = 100,
        DisplayName = "Feature: ble-proximity-lock, Property 8: Threshold Validation - InRange out of bounds fails")]
    public Property InRangeOutOfBounds_AlwaysFailsValidation(InRangeOutOfBoundsPair pair)
    {
        var result = ThresholdValidator.Validate(pair.InRange, pair.OutOfRange);

        return (!result.IsValid).ToProperty()
            .Label($"InRange={pair.InRange}, OutOfRange={pair.OutOfRange}");
    }

    /// <summary>
    /// Pairs with OutOfRange outside [-95,-55] always fail validation.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(
        Arbitrary = new[] { typeof(OutOfRangeOutOfBoundsArbitrary) },
        MaxTest = 100,
        DisplayName = "Feature: ble-proximity-lock, Property 8: Threshold Validation - OutOfRange out of bounds fails")]
    public Property OutOfRangeOutOfBounds_AlwaysFailsValidation(OutOfRangeOutOfBoundsPair pair)
    {
        var result = ThresholdValidator.Validate(pair.InRange, pair.OutOfRange);

        return (!result.IsValid).ToProperty()
            .Label($"InRange={pair.InRange}, OutOfRange={pair.OutOfRange}");
    }
}

#region Custom Types and Generators

/// <summary>
/// Represents a valid threshold pair where all constraints are satisfied.
/// </summary>
public record ValidThresholdPair(int InRange, int OutOfRange);

/// <summary>
/// Represents a pair where the buffer between InRange and OutOfRange is less than 5.
/// </summary>
public record InsufficientBufferPair(int InRange, int OutOfRange);

/// <summary>
/// Represents a pair where OutOfRange >= InRange.
/// </summary>
public record OutOfRangeNotLessThanInRangePair(int InRange, int OutOfRange);

/// <summary>
/// Represents a pair where InRange is outside the valid range [-90, -50].
/// </summary>
public record InRangeOutOfBoundsPair(int InRange, int OutOfRange);

/// <summary>
/// Represents a pair where OutOfRange is outside the valid range [-95, -55].
/// </summary>
public record OutOfRangeOutOfBoundsPair(int InRange, int OutOfRange);

#endregion

#region Arbitrary Implementations

public static class ValidThresholdPairArbitrary
{
    public static Arbitrary<ValidThresholdPair> ValidThresholdPair()
    {
        // InRange in [-90, -50], OutOfRange in [-95, -55]
        // buffer >= 5, OutOfRange < InRange
        var gen = from inRange in Gen.Choose(-90, -50)
                  from outOfRange in Gen.Choose(-95, -55)
                  where outOfRange < inRange && (inRange - outOfRange) >= 5
                  select new ValidThresholdPair(inRange, outOfRange);

        return Arb.From(gen);
    }
}

public static class InsufficientBufferPairArbitrary
{
    public static Arbitrary<InsufficientBufferPair> InsufficientBufferPair()
    {
        // Both values within valid ranges, OutOfRange < InRange, but buffer < 5
        var gen = from inRange in Gen.Choose(-90, -50)
                  from buffer in Gen.Choose(1, 4)
                  let outOfRange = inRange - buffer
                  where outOfRange >= -95 && outOfRange <= -55
                  select new InsufficientBufferPair(inRange, outOfRange);

        return Arb.From(gen);
    }
}

public static class OutOfRangeNotLessThanInRangeArbitrary
{
    public static Arbitrary<OutOfRangeNotLessThanInRangePair> OutOfRangeNotLessThanInRangePair()
    {
        // Both values within valid ranges, but OutOfRange >= InRange
        var gen = from inRange in Gen.Choose(-90, -50)
                  from outOfRange in Gen.Choose(-95, -55)
                  where outOfRange >= inRange
                  select new OutOfRangeNotLessThanInRangePair(inRange, outOfRange);

        return Arb.From(gen);
    }
}

public static class InRangeOutOfBoundsArbitrary
{
    public static Arbitrary<InRangeOutOfBoundsPair> InRangeOutOfBoundsPair()
    {
        // InRange outside [-90, -50], OutOfRange within valid range
        var gen = Gen.OneOf(
            // InRange too high (above -50)
            from inRange in Gen.Choose(-49, 0)
            from outOfRange in Gen.Choose(-95, -55)
            select new InRangeOutOfBoundsPair(inRange, outOfRange),
            // InRange too low (below -90)
            from inRange in Gen.Choose(-150, -91)
            from outOfRange in Gen.Choose(-95, -55)
            select new InRangeOutOfBoundsPair(inRange, outOfRange)
        );

        return Arb.From(gen);
    }
}

public static class OutOfRangeOutOfBoundsArbitrary
{
    public static Arbitrary<OutOfRangeOutOfBoundsPair> OutOfRangeOutOfBoundsPair()
    {
        // InRange within valid range, OutOfRange outside [-95, -55]
        var gen = Gen.OneOf(
            // OutOfRange too high (above -55)
            from inRange in Gen.Choose(-90, -50)
            from outOfRange in Gen.Choose(-54, 0)
            select new OutOfRangeOutOfBoundsPair(inRange, outOfRange),
            // OutOfRange too low (below -95)
            from inRange in Gen.Choose(-90, -50)
            from outOfRange in Gen.Choose(-150, -96)
            select new OutOfRangeOutOfBoundsPair(inRange, outOfRange)
        );

        return Arb.From(gen);
    }
}

#endregion
