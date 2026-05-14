using BLEProximity.Services;
using FsCheck;
using FsCheck.Xunit;

namespace BLEProximity.Tests.Properties;

/// <summary>
/// Property-based tests for EMA Smoothing Formula Correctness.
/// Validates: Requirements 2.1, 2.4
/// </summary>
public class RssiSmootherTests
{
    /// <summary>
    /// Generates valid RSSI values (negative doubles between -100 and -1).
    /// </summary>
    private static Arbitrary<double> ValidRssiArbitrary()
    {
        return Arb.From(
            Gen.Choose(-10000, -100)
               .Select(i => i / 100.0));
    }

    /// <summary>
    /// Generates valid alpha values in [0.1, 0.5].
    /// </summary>
    private static Arbitrary<double> ValidAlphaArbitrary()
    {
        return Arb.From(
            Gen.Choose(10, 50)
               .Select(i => i / 100.0));
    }

    /// <summary>
    /// Generates a non-empty list of valid RSSI values (between 1 and 50 readings).
    /// </summary>
    private static Arbitrary<double[]> RssiSequenceArbitrary()
    {
        return Arb.From(
            Gen.Choose(-10000, -100)
               .Select(i => i / 100.0)
               .ArrayOf()
               .Where(arr => arr.Length > 0 && arr.Length <= 50));
    }

    /// <summary>
    /// Property 6: EMA Smoothing Formula Correctness - First Reading Initialization
    /// 
    /// For any valid RSSI reading and any valid alpha, the first reading for a device
    /// SHALL produce a SmoothedRssi equal to the raw value.
    /// 
    /// **Validates: Requirements 2.1, 2.4**
    /// </summary>
    [Property(
        Arbitrary = new[] { typeof(RssiSmootherTests) },
        MaxTest = 100,
        DisplayName = "Feature: ble-proximity-lock, Property 6: EMA Smoothing Formula Correctness - First reading returns raw value")]
    public bool FirstReading_ReturnsRawValue(ValidRssi rssi, ValidAlpha alpha)
    {
        var smoother = new RssiSmoother();
        smoother.Alpha = alpha.Value;

        ulong address = 0xAABBCCDDEEFF;
        double result = smoother.Smooth(address, rssi.Value);

        return result == rssi.Value;
    }

    /// <summary>
    /// Property 6: EMA Smoothing Formula Correctness - Subsequent Readings
    /// 
    /// For any sequence of RSSI readings and any valid alpha, each subsequent reading
    /// SHALL produce SmoothedRssi = alpha × NewRSSI + (1 - alpha) × PreviousSmoothedRSSI.
    /// 
    /// **Validates: Requirements 2.1, 2.4**
    /// </summary>
    [Property(
        Arbitrary = new[] { typeof(RssiSmootherTests) },
        MaxTest = 100,
        DisplayName = "Feature: ble-proximity-lock, Property 6: EMA Smoothing Formula Correctness - Subsequent readings follow EMA formula")]
    public bool SubsequentReadings_FollowEmaFormula(RssiSequence sequence, ValidAlpha alpha)
    {
        if (sequence.Values.Length < 2)
            return true; // Need at least 2 readings to test subsequent behavior

        var smoother = new RssiSmoother();
        smoother.Alpha = alpha.Value;

        ulong address = 0xAABBCCDDEEFF;

        // First reading initializes directly
        double previousSmoothed = smoother.Smooth(address, sequence.Values[0]);
        if (previousSmoothed != sequence.Values[0])
            return false;

        // Each subsequent reading must follow EMA formula
        for (int i = 1; i < sequence.Values.Length; i++)
        {
            double newRssi = sequence.Values[i];
            double actual = smoother.Smooth(address, newRssi);
            double expected = alpha.Value * newRssi + (1 - alpha.Value) * previousSmoothed;

            // Use tolerance for floating-point comparison
            if (Math.Abs(actual - expected) > 1e-10)
                return false;

            previousSmoothed = actual;
        }

        return true;
    }

    /// <summary>
    /// Property 6: EMA Smoothing Formula Correctness - Per-Device Independence
    /// 
    /// For any two different devices with their own RSSI sequences and a shared alpha,
    /// the EMA computation for one device SHALL NOT affect the other device's smoothed values.
    /// 
    /// **Validates: Requirements 2.1, 2.4**
    /// </summary>
    [Property(
        Arbitrary = new[] { typeof(RssiSmootherTests) },
        MaxTest = 100,
        DisplayName = "Feature: ble-proximity-lock, Property 6: EMA Smoothing Formula Correctness - Per-device independence")]
    public bool PerDeviceIndependence_EmaIsIndependent(ValidRssi rssi1, ValidRssi rssi2, ValidRssi rssi3, ValidAlpha alpha)
    {
        var smoother = new RssiSmoother();
        smoother.Alpha = alpha.Value;

        ulong address1 = 0xAABBCCDDEEFF;
        ulong address2 = 0x112233445566;

        // Initialize both devices
        double smoothed1 = smoother.Smooth(address1, rssi1.Value);
        double smoothed2 = smoother.Smooth(address2, rssi2.Value);

        // Apply second reading to device 1 only
        double result1 = smoother.Smooth(address1, rssi3.Value);
        double expected1 = alpha.Value * rssi3.Value + (1 - alpha.Value) * smoothed1;

        // Device 2 should still return its initial value when given a new reading
        double result2 = smoother.Smooth(address2, rssi3.Value);
        double expected2 = alpha.Value * rssi3.Value + (1 - alpha.Value) * smoothed2;

        return Math.Abs(result1 - expected1) < 1e-10
            && Math.Abs(result2 - expected2) < 1e-10;
    }

    #region Custom Types for FsCheck Generators

    /// <summary>
    /// Wrapper type for valid RSSI values (negative doubles between -100 and -1).
    /// </summary>
    public class ValidRssi
    {
        public double Value { get; }
        public ValidRssi(double value) => Value = value;
        public override string ToString() => $"RSSI({Value:F2} dBm)";
    }

    /// <summary>
    /// Wrapper type for valid alpha values in [0.1, 0.5].
    /// </summary>
    public class ValidAlpha
    {
        public double Value { get; }
        public ValidAlpha(double value) => Value = value;
        public override string ToString() => $"Alpha({Value:F2})";
    }

    /// <summary>
    /// Wrapper type for a non-empty sequence of valid RSSI values.
    /// </summary>
    public class RssiSequence
    {
        public double[] Values { get; }
        public RssiSequence(double[] values) => Values = values;
        public override string ToString() => $"RssiSequence(Length={Values.Length}, First={Values[0]:F2})";
    }

    #endregion

    #region FsCheck Arbitraries

    public static Arbitrary<ValidRssi> ArbitraryValidRssi()
    {
        return Arb.From(
            Gen.Choose(-10000, -100)
               .Select(i => new ValidRssi(i / 100.0)));
    }

    public static Arbitrary<ValidAlpha> ArbitraryValidAlpha()
    {
        return Arb.From(
            Gen.Choose(10, 50)
               .Select(i => new ValidAlpha(i / 100.0)));
    }

    public static Arbitrary<RssiSequence> ArbitraryRssiSequence()
    {
        return Arb.From(
            Gen.Choose(2, 50).SelectMany(length =>
                Gen.Choose(-10000, -100)
                   .Select(i => i / 100.0)
                   .ArrayOf(length))
               .Select(arr => new RssiSequence(arr)));
    }

    #endregion
}
