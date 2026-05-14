using BLEProximity.Models;
using BLEProximity.Services;
using FsCheck;
using FsCheck.Xunit;

namespace BLEProximity.Tests.Properties;

/// <summary>
/// Property-based tests for MultiDevicePolicy.
/// Validates: Requirements 7.1, 7.2, 7.3
///
/// Property 13: Multi-Device All-Out-Of-Range Trigger
/// For any set of 1 to 10 trusted devices where ALL devices have smoothed RSSI below
/// OutOfRangeThreshold for the full OutOfRangeTimeout duration, the Multi_Device_Policy
/// SHALL signal to trigger the out-of-range flow. If ANY device has smoothed RSSI above
/// or equal to OutOfRangeThreshold, the policy SHALL NOT trigger.
///
/// Property 14: Multi-Device Any-In-Range Cancellation
/// For any set of trusted devices in an active out-of-range flow (OutOfRangePending or Countdown),
/// when ANY single device's smoothed RSSI rises above InRangeThreshold, the Multi_Device_Policy
/// SHALL signal cancellation and the system SHALL return to InRange.
/// </summary>
public class MultiDevicePolicyTests
{
    private const int DefaultInRangeThreshold = -70;
    private const int DefaultOutOfRangeThreshold = -75;
    private const int DefaultOutOfRangeTimeoutSec = 10;

    #region Property 13: Multi-Device All-Out-Of-Range Trigger

    /// <summary>
    /// When ALL devices are out of range (IsOutOfRange=true) and have been out of range
    /// for longer than the OutOfRangeTimeout, the policy SHALL trigger.
    /// **Validates: Requirements 7.1, 7.2**
    /// </summary>
    [Property(
        Arbitrary = new[] { typeof(AllDevicesOutOfRangeArbitrary) },
        MaxTest = 100,
        DisplayName = "Feature: ble-proximity-lock, Property 13: Multi-Device All-Out-Of-Range Trigger - All out of range triggers")]
    public Property AllDevicesOutOfRange_ShouldTrigger(AllDevicesOutOfRangeScenario scenario)
    {
        var policy = new MultiDevicePolicy(
            scenario.InRangeThreshold,
            scenario.OutOfRangeThreshold,
            scenario.OutOfRangeTimeoutSec,
            useSingleDevice: false);

        var result = policy.ShouldTriggerOutOfRange(scenario.Devices);

        return result.ToProperty()
            .Label($"DeviceCount={scenario.Devices.Count}, Timeout={scenario.OutOfRangeTimeoutSec}s");
    }

    /// <summary>
    /// When ANY device has smoothed RSSI above or equal to OutOfRangeThreshold,
    /// the policy SHALL NOT trigger.
    /// **Validates: Requirements 7.1, 7.2**
    /// </summary>
    [Property(
        Arbitrary = new[] { typeof(AnyDeviceInRangeArbitrary) },
        MaxTest = 100,
        DisplayName = "Feature: ble-proximity-lock, Property 13: Multi-Device All-Out-Of-Range Trigger - Any device in range prevents trigger")]
    public Property AnyDeviceNotOutOfRange_ShouldNotTrigger(AnyDeviceInRangeScenario scenario)
    {
        var policy = new MultiDevicePolicy(
            scenario.InRangeThreshold,
            scenario.OutOfRangeThreshold,
            scenario.OutOfRangeTimeoutSec,
            useSingleDevice: false);

        var result = policy.ShouldTriggerOutOfRange(scenario.Devices);

        return (!result).ToProperty()
            .Label($"DeviceCount={scenario.Devices.Count}, InRangeDeviceIndex={scenario.InRangeDeviceIndex}");
    }

    #endregion

    #region Property 14: Multi-Device Any-In-Range Cancellation

    /// <summary>
    /// When ANY single device has smoothed RSSI strictly above InRangeThreshold,
    /// the policy SHALL signal cancellation.
    /// **Validates: Requirements 7.3**
    /// </summary>
    [Property(
        Arbitrary = new[] { typeof(AnyDeviceAboveInRangeThresholdArbitrary) },
        MaxTest = 100,
        DisplayName = "Feature: ble-proximity-lock, Property 14: Multi-Device Any-In-Range Cancellation - Any device above InRangeThreshold cancels")]
    public Property AnyDeviceAboveInRangeThreshold_ShouldCancel(AnyDeviceAboveInRangeThresholdScenario scenario)
    {
        var policy = new MultiDevicePolicy(
            scenario.InRangeThreshold,
            scenario.OutOfRangeThreshold,
            scenario.OutOfRangeTimeoutSec,
            useSingleDevice: false);

        var result = policy.ShouldCancelOutOfRange(scenario.Devices);

        return result.ToProperty()
            .Label($"DeviceCount={scenario.Devices.Count}, InRangeDeviceRssi={scenario.InRangeDeviceRssi}");
    }

    /// <summary>
    /// When NO device has smoothed RSSI strictly above InRangeThreshold,
    /// the policy SHALL NOT signal cancellation.
    /// **Validates: Requirements 7.3**
    /// </summary>
    [Property(
        Arbitrary = new[] { typeof(NoDeviceAboveInRangeThresholdArbitrary) },
        MaxTest = 100,
        DisplayName = "Feature: ble-proximity-lock, Property 14: Multi-Device Any-In-Range Cancellation - No device above InRangeThreshold does not cancel")]
    public Property NoDeviceAboveInRangeThreshold_ShouldNotCancel(NoDeviceAboveInRangeThresholdScenario scenario)
    {
        var policy = new MultiDevicePolicy(
            scenario.InRangeThreshold,
            scenario.OutOfRangeThreshold,
            scenario.OutOfRangeTimeoutSec,
            useSingleDevice: false);

        var result = policy.ShouldCancelOutOfRange(scenario.Devices);

        return (!result).ToProperty()
            .Label($"DeviceCount={scenario.Devices.Count}, MaxRssi={scenario.MaxRssi}");
    }

    #endregion
}

#region Custom Types

/// <summary>
/// Scenario where all devices are out of range for longer than the timeout.
/// </summary>
public record AllDevicesOutOfRangeScenario(
    IReadOnlyList<TrustedDeviceStatus> Devices,
    int InRangeThreshold,
    int OutOfRangeThreshold,
    int OutOfRangeTimeoutSec);

/// <summary>
/// Scenario where at least one device is NOT out of range (IsOutOfRange=false).
/// </summary>
public record AnyDeviceInRangeScenario(
    IReadOnlyList<TrustedDeviceStatus> Devices,
    int InRangeThreshold,
    int OutOfRangeThreshold,
    int OutOfRangeTimeoutSec,
    int InRangeDeviceIndex);

/// <summary>
/// Scenario where at least one device has SmoothedRssi strictly above InRangeThreshold.
/// </summary>
public record AnyDeviceAboveInRangeThresholdScenario(
    IReadOnlyList<TrustedDeviceStatus> Devices,
    int InRangeThreshold,
    int OutOfRangeThreshold,
    int OutOfRangeTimeoutSec,
    double InRangeDeviceRssi);

/// <summary>
/// Scenario where no device has SmoothedRssi strictly above InRangeThreshold.
/// </summary>
public record NoDeviceAboveInRangeThresholdScenario(
    IReadOnlyList<TrustedDeviceStatus> Devices,
    int InRangeThreshold,
    int OutOfRangeThreshold,
    int OutOfRangeTimeoutSec,
    double MaxRssi);

#endregion

#region Arbitrary Implementations

public static class AllDevicesOutOfRangeArbitrary
{
    public static Arbitrary<AllDevicesOutOfRangeScenario> AllDevicesOutOfRangeScenario()
    {
        var gen =
            from deviceCount in Gen.Choose(1, 10)
            from inRangeThreshold in Gen.Choose(-80, -60)
            from outOfRangeThreshold in Gen.Choose(-90, -65)
            where outOfRangeThreshold < inRangeThreshold && (inRangeThreshold - outOfRangeThreshold) >= 5
            from timeoutSec in Gen.Choose(5, 30)
            from extraSeconds in Gen.Choose(1, 60)
            let outOfRangeSince = DateTime.UtcNow.AddSeconds(-(timeoutSec + extraSeconds))
            from devices in Gen.ArrayOf(deviceCount, CreateOutOfRangeDevice(outOfRangeThreshold, outOfRangeSince))
            select new AllDevicesOutOfRangeScenario(
                devices.ToList().AsReadOnly(),
                inRangeThreshold,
                outOfRangeThreshold,
                timeoutSec);

        return Arb.From(gen);
    }

    private static Gen<TrustedDeviceStatus> CreateOutOfRangeDevice(int outOfRangeThreshold, DateTime outOfRangeSince)
    {
        return from rssiOffset in Gen.Choose(1, 30)
               let rssi = outOfRangeThreshold - rssiOffset
               from addressSuffix in Gen.Choose(1, 999999)
               select new TrustedDeviceStatus
               {
                   Device = new TrustedDevice
                   {
                       Name = $"Device_{addressSuffix}",
                       BluetoothAddress = (ulong)addressSuffix,
                       MacAddress = addressSuffix.ToString("X12")
                   },
                   SmoothedRssi = rssi,
                   LastSeen = DateTime.UtcNow.AddSeconds(-5),
                   IsOutOfRange = true,
                   OutOfRangeSince = outOfRangeSince
               };
    }
}

public static class AnyDeviceInRangeArbitrary
{
    public static Arbitrary<AnyDeviceInRangeScenario> AnyDeviceInRangeScenario()
    {
        var gen =
            from deviceCount in Gen.Choose(2, 10)
            from inRangeThreshold in Gen.Choose(-80, -60)
            from outOfRangeThreshold in Gen.Choose(-90, -65)
            where outOfRangeThreshold < inRangeThreshold && (inRangeThreshold - outOfRangeThreshold) >= 5
            from timeoutSec in Gen.Choose(5, 30)
            from extraSeconds in Gen.Choose(1, 60)
            let outOfRangeSince = DateTime.UtcNow.AddSeconds(-(timeoutSec + extraSeconds))
            from inRangeIndex in Gen.Choose(0, deviceCount - 1)
            from devices in GenDevicesWithOneInRange(deviceCount, outOfRangeThreshold, outOfRangeSince, inRangeIndex)
            select new AnyDeviceInRangeScenario(
                devices.ToList().AsReadOnly(),
                inRangeThreshold,
                outOfRangeThreshold,
                timeoutSec,
                inRangeIndex);

        return Arb.From(gen);
    }

    private static Gen<TrustedDeviceStatus[]> GenDevicesWithOneInRange(
        int count, int outOfRangeThreshold, DateTime outOfRangeSince, int inRangeIndex)
    {
        return Gen.Sequence(Enumerable.Range(0, count).Select(i =>
        {
            if (i == inRangeIndex)
            {
                // This device is NOT out of range
                return from addressSuffix in Gen.Choose(1, 999999)
                       select new TrustedDeviceStatus
                       {
                           Device = new TrustedDevice
                           {
                               Name = $"Device_{addressSuffix}",
                               BluetoothAddress = (ulong)addressSuffix,
                               MacAddress = addressSuffix.ToString("X12")
                           },
                           SmoothedRssi = outOfRangeThreshold + 5, // above threshold
                           LastSeen = DateTime.UtcNow,
                           IsOutOfRange = false,
                           OutOfRangeSince = null
                       };
            }
            else
            {
                // This device IS out of range
                return from rssiOffset in Gen.Choose(1, 30)
                       let rssi = outOfRangeThreshold - rssiOffset
                       from addressSuffix in Gen.Choose(1, 999999)
                       select new TrustedDeviceStatus
                       {
                           Device = new TrustedDevice
                           {
                               Name = $"Device_{addressSuffix}",
                               BluetoothAddress = (ulong)addressSuffix,
                               MacAddress = addressSuffix.ToString("X12")
                           },
                           SmoothedRssi = rssi,
                           LastSeen = DateTime.UtcNow.AddSeconds(-5),
                           IsOutOfRange = true,
                           OutOfRangeSince = outOfRangeSince
                       };
            }
        })).Select(devices => devices.ToArray());
    }
}

public static class AnyDeviceAboveInRangeThresholdArbitrary
{
    public static Arbitrary<AnyDeviceAboveInRangeThresholdScenario> AnyDeviceAboveInRangeThresholdScenario()
    {
        var gen =
            from deviceCount in Gen.Choose(1, 10)
            from inRangeThreshold in Gen.Choose(-80, -60)
            from outOfRangeThreshold in Gen.Choose(-90, -65)
            where outOfRangeThreshold < inRangeThreshold && (inRangeThreshold - outOfRangeThreshold) >= 5
            from timeoutSec in Gen.Choose(5, 30)
            from inRangeIndex in Gen.Choose(0, deviceCount - 1)
            from rssiAbove in Gen.Choose(1, 20)
            let inRangeRssi = (double)(inRangeThreshold + rssiAbove)
            from devices in GenDevicesWithOneAboveInRange(deviceCount, inRangeThreshold, outOfRangeThreshold, inRangeIndex, inRangeRssi)
            select new AnyDeviceAboveInRangeThresholdScenario(
                devices.ToList().AsReadOnly(),
                inRangeThreshold,
                outOfRangeThreshold,
                timeoutSec,
                inRangeRssi);

        return Arb.From(gen);
    }

    private static Gen<TrustedDeviceStatus[]> GenDevicesWithOneAboveInRange(
        int count, int inRangeThreshold, int outOfRangeThreshold, int inRangeIndex, double inRangeRssi)
    {
        return Gen.Sequence(Enumerable.Range(0, count).Select(i =>
        {
            if (i == inRangeIndex)
            {
                // This device has RSSI strictly above InRangeThreshold
                return from addressSuffix in Gen.Choose(1, 999999)
                       select new TrustedDeviceStatus
                       {
                           Device = new TrustedDevice
                           {
                               Name = $"Device_{addressSuffix}",
                               BluetoothAddress = (ulong)addressSuffix,
                               MacAddress = addressSuffix.ToString("X12")
                           },
                           SmoothedRssi = inRangeRssi,
                           LastSeen = DateTime.UtcNow,
                           IsOutOfRange = false,
                           OutOfRangeSince = null
                       };
            }
            else
            {
                // Other devices have RSSI below or at InRangeThreshold
                return from rssiOffset in Gen.Choose(0, 30)
                       let rssi = (double)(outOfRangeThreshold - rssiOffset)
                       from addressSuffix in Gen.Choose(1, 999999)
                       select new TrustedDeviceStatus
                       {
                           Device = new TrustedDevice
                           {
                               Name = $"Device_{addressSuffix}",
                               BluetoothAddress = (ulong)addressSuffix,
                               MacAddress = addressSuffix.ToString("X12")
                           },
                           SmoothedRssi = rssi,
                           LastSeen = DateTime.UtcNow.AddSeconds(-5),
                           IsOutOfRange = true,
                           OutOfRangeSince = DateTime.UtcNow.AddSeconds(-30)
                       };
            }
        })).Select(devices => devices.ToArray());
    }
}

public static class NoDeviceAboveInRangeThresholdArbitrary
{
    public static Arbitrary<NoDeviceAboveInRangeThresholdScenario> NoDeviceAboveInRangeThresholdScenario()
    {
        var gen =
            from deviceCount in Gen.Choose(1, 10)
            from inRangeThreshold in Gen.Choose(-80, -60)
            from outOfRangeThreshold in Gen.Choose(-90, -65)
            where outOfRangeThreshold < inRangeThreshold && (inRangeThreshold - outOfRangeThreshold) >= 5
            from timeoutSec in Gen.Choose(5, 30)
            from devices in Gen.ArrayOf(deviceCount, CreateDeviceBelowOrAtInRange(inRangeThreshold, outOfRangeThreshold))
            let maxRssi = devices.Max(d => d.SmoothedRssi ?? double.MinValue)
            select new NoDeviceAboveInRangeThresholdScenario(
                devices.ToList().AsReadOnly(),
                inRangeThreshold,
                outOfRangeThreshold,
                timeoutSec,
                maxRssi);

        return Arb.From(gen);
    }

    private static Gen<TrustedDeviceStatus> CreateDeviceBelowOrAtInRange(int inRangeThreshold, int outOfRangeThreshold)
    {
        // Generate RSSI values at or below InRangeThreshold (never strictly above)
        return from rssiOffset in Gen.Choose(0, 40)
               let rssi = (double)(inRangeThreshold - rssiOffset)
               from addressSuffix in Gen.Choose(1, 999999)
               select new TrustedDeviceStatus
               {
                   Device = new TrustedDevice
                   {
                       Name = $"Device_{addressSuffix}",
                       BluetoothAddress = (ulong)addressSuffix,
                       MacAddress = addressSuffix.ToString("X12")
                   },
                   SmoothedRssi = rssi,
                   LastSeen = DateTime.UtcNow.AddSeconds(-5),
                   IsOutOfRange = true,
                   OutOfRangeSince = DateTime.UtcNow.AddSeconds(-30)
               };
    }
}

#endregion
