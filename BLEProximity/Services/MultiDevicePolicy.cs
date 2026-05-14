using BLEProximity.Models;

namespace BLEProximity.Services;

/// <summary>
/// Evaluates proximity status across multiple trusted devices.
/// Triggers out-of-range only when ALL devices are out of range for the full timeout duration.
/// Cancels out-of-range when ANY single device returns to in-range.
/// </summary>
public class MultiDevicePolicy : IMultiDevicePolicy
{
    private const int MaxTrustedDevices = 10;

    private int _inRangeThreshold;
    private int _outOfRangeThreshold;
    private int _outOfRangeTimeoutSec;
    private bool _useSingleDevice;

    public MultiDevicePolicy(int inRangeThreshold, int outOfRangeThreshold, int outOfRangeTimeoutSec, bool useSingleDevice = false)
    {
        _inRangeThreshold = inRangeThreshold;
        _outOfRangeThreshold = outOfRangeThreshold;
        _outOfRangeTimeoutSec = outOfRangeTimeoutSec;
        _useSingleDevice = useSingleDevice;
    }

    /// <summary>
    /// Configures the policy with updated threshold and timeout values.
    /// </summary>
    public void Configure(int inRangeThreshold, int outOfRangeThreshold, int outOfRangeTimeoutSec, bool useSingleDevice = false)
    {
        _inRangeThreshold = inRangeThreshold;
        _outOfRangeThreshold = outOfRangeThreshold;
        _outOfRangeTimeoutSec = outOfRangeTimeoutSec;
        _useSingleDevice = useSingleDevice;
    }

    /// <summary>
    /// Gets the maximum number of trusted devices allowed based on the current mode.
    /// In UseSingleDevice mode, returns 1. Otherwise, returns 10.
    /// </summary>
    public int MaxAllowedDevices => _useSingleDevice ? 1 : MaxTrustedDevices;

    /// <summary>
    /// Returns true only when ALL devices in the trusted list have smoothed RSSI below
    /// OutOfRangeThreshold for the full OutOfRangeTimeout duration.
    /// Returns false if the trusted list is empty.
    /// </summary>
    public bool ShouldTriggerOutOfRange(IReadOnlyList<TrustedDeviceStatus> devices)
    {
        // Empty list: never trigger (remain InRange)
        if (devices == null || devices.Count == 0)
            return false;

        // Enforce device limit based on mode
        var effectiveDevices = GetEffectiveDevices(devices);

        // If no effective devices after limiting, never trigger
        if (effectiveDevices.Count == 0)
            return false;

        var now = DateTime.UtcNow;

        // ALL devices must have IsOutOfRange=true AND OutOfRangeSince older than OutOfRangeTimeout
        foreach (var device in effectiveDevices)
        {
            // If any device is not out of range, don't trigger
            if (!device.IsOutOfRange)
                return false;

            // If any device doesn't have an OutOfRangeSince timestamp, don't trigger
            if (device.OutOfRangeSince == null)
                return false;

            // If any device hasn't been out of range long enough, don't trigger
            var outOfRangeDuration = now - device.OutOfRangeSince.Value;
            if (outOfRangeDuration.TotalSeconds < _outOfRangeTimeoutSec)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Returns true when ANY single device has smoothed RSSI strictly above InRangeThreshold.
    /// Returns false if the trusted list is empty.
    /// </summary>
    public bool ShouldCancelOutOfRange(IReadOnlyList<TrustedDeviceStatus> devices)
    {
        if (devices == null || devices.Count == 0)
            return false;

        var effectiveDevices = GetEffectiveDevices(devices);

        // ANY device with SmoothedRssi above InRangeThreshold cancels the out-of-range flow
        foreach (var device in effectiveDevices)
        {
            if (device.SmoothedRssi.HasValue && device.SmoothedRssi.Value > _inRangeThreshold)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the effective device list, limited by the current mode constraints.
    /// In UseSingleDevice mode, only the first device is considered.
    /// In multi-device mode, up to MaxTrustedDevices (10) are considered.
    /// </summary>
    private IReadOnlyList<TrustedDeviceStatus> GetEffectiveDevices(IReadOnlyList<TrustedDeviceStatus> devices)
    {
        if (_useSingleDevice)
        {
            // In single device mode, only consider the first device
            return devices.Count > 0 ? new List<TrustedDeviceStatus> { devices[0] } : new List<TrustedDeviceStatus>();
        }

        // In multi-device mode, enforce maximum of 10 devices
        if (devices.Count <= MaxTrustedDevices)
            return devices;

        return devices.Take(MaxTrustedDevices).ToList();
    }
}
